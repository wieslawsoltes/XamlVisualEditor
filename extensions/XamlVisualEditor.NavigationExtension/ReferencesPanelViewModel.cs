using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using ReactiveUI;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.NavigationExtension;

public sealed class ReferenceLocationItemViewModel : ReactiveObject
{
    public ReferenceLocationItemViewModel(LanguageLocation location, string? label = null)
    {
        Location = location;
        FilePath = location.FilePath;
        Line = location.Range.Start.Line;
        Column = location.Range.Start.Column;
        string fileName = System.IO.Path.GetFileName(FilePath);
        DisplayText = label ?? $"{fileName} ({Line},{Column})";
    }

    public LanguageLocation Location { get; }
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }
    public string DisplayText { get; }
}

public sealed class ReferencesGroupViewModel : ReactiveObject
{
    private bool _isExpanded;

    public ReferencesGroupViewModel(string filePath)
    {
        FilePath = filePath;
        FileName = System.IO.Path.GetFileName(filePath);
    }

    public string FilePath { get; }
    public string FileName { get; }
    public ObservableCollection<ReferenceLocationItemViewModel> Items { get; } = new();
    public string DisplayText => FileName;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }
}

public sealed class ReferencesPanelViewModel : ReactiveObject
{
    private readonly ILanguageNavigationService _navigation;
    private readonly IEditorServices _editor;
    private readonly IWindow _window;
    private readonly CompositeDisposable _groupDisposables = new();
    private readonly HashSet<string> _expandedFiles = new(StringComparer.OrdinalIgnoreCase);
    private const string FilterPropertyPath = "Item.DisplayText";
    private HierarchicalNode? _selectedRow;
    private object? _selectedItem;
    private int _totalCount;
    private string? _filterText;

    public ObservableCollection<ReferencesGroupViewModel> Groups { get; } = new();

    public HierarchicalModel Model { get; }

    public HierarchicalNode? SelectedRow
    {
        get => _selectedRow;
        set => this.RaiseAndSetIfChanged(ref _selectedRow, value);
    }

    public object? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => this.RaiseAndSetIfChanged(ref _totalCount, value);
    }

    public string? FilterText
    {
        get => _filterText;
        set => this.RaiseAndSetIfChanged(ref _filterText, value);
    }

    public SortingModel SortingModel { get; }

    public FilteringModel FilteringModel { get; }

    public SearchModel SearchModel { get; }

    public ReactiveCommand<ReferenceLocationItemViewModel, Unit> OpenLocationCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenSelectedCommand { get; }

    public ReferencesPanelViewModel(
        ILanguageNavigationService navigation,
        IEditorServices editor,
        IWindow window)
    {
        _navigation = navigation;
        _editor = editor;
        _window = window;

        OpenLocationCommand = ReactiveCommand.CreateFromTask<ReferenceLocationItemViewModel>(OpenLocationAsync);
        OpenSelectedCommand = ReactiveCommand.CreateFromTask(OpenSelectedAsync);

        SortingModel = new SortingModel();
        FilteringModel = new FilteringModel();
        SearchModel = new SearchModel
        {
            HighlightMode = SearchHighlightMode.TextAndCell,
            HighlightCurrent = true,
            WrapNavigation = true,
            UpdateSelectionOnNavigate = true
        };

        var options = new HierarchicalOptions
        {
            ChildrenSelector = item => item is ReferencesGroupViewModel group ? group.Items : null,
            IsLeafSelector = item => item is ReferenceLocationItemViewModel,
            IsExpandedSelector = item => item is ReferencesGroupViewModel group && group.IsExpanded,
            IsExpandedSetter = (item, value) =>
            {
                if (item is ReferencesGroupViewModel group)
                {
                    group.IsExpanded = value;
                }
            },
            VirtualizeChildren = true
        };

        Model = new HierarchicalModel(options);
        Model.SetRoots(Groups);

        this.WhenAnyValue(x => x.SelectedRow)
            .Subscribe(row => SelectedItem = row?.Item);

        this.WhenAnyValue(x => x.FilterText)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ApplyFilterAndSearch);
    }

    public async Task FindReferencesAsync(CancellationToken cancellationToken)
    {
        LanguagePositionContext? context = await BuildPositionContextAsync(cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return;
        }

        IReadOnlyList<LanguageLocation> locations = await _navigation.FindReferencesAsync(context, cancellationToken).ConfigureAwait(false);
        if (locations.Count == 0)
        {
            await _window.ShowInformationMessageAsync("No references found.", cancellationToken);
        }

        ReplaceItems(BuildReferenceItems(locations));
    }

    public async Task GoToDefinitionAsync(CancellationToken cancellationToken)
    {
        LanguagePositionContext? context = await BuildPositionContextAsync(cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return;
        }

        IReadOnlyList<LanguageLocation> locations = await _navigation.FindDefinitionsAsync(context, cancellationToken).ConfigureAwait(false);
        if (locations.Count == 0)
        {
            await _window.ShowInformationMessageAsync("No definitions found.", cancellationToken);
            return;
        }

        if (locations.Count == 1)
        {
            await _editor.OpenLocationAsync(locations[0], cancellationToken).ConfigureAwait(false);
            return;
        }

        List<QuickPickItem> items = new();
        Dictionary<string, LanguageLocation> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (LanguageLocation location in locations)
        {
            string fileName = System.IO.Path.GetFileName(location.FilePath);
            string label = $"{fileName} ({location.Range.Start.Line},{location.Range.Start.Column})";
            items.Add(new QuickPickItem(label, location.FilePath, null));
            map[label] = location;
        }

        QuickPickItem? selection = await _window.ShowQuickPickAsync(
            items,
            new QuickPickOptions("Select Definition", CanPickMany: false),
            cancellationToken);
        if (selection is null)
        {
            return;
        }

        if (map.TryGetValue(selection.Label, out LanguageLocation? chosen))
        {
            await _editor.OpenLocationAsync(chosen, cancellationToken).ConfigureAwait(false);
        }
    }

    public void ReplaceItems(IEnumerable<ReferenceLocationItemViewModel> items)
    {
        _groupDisposables.Clear();
        Groups.Clear();
        TotalCount = 0;

        foreach (IGrouping<string, ReferenceLocationItemViewModel> group in items.GroupBy(i => i.FilePath))
        {
            ReferencesGroupViewModel groupVm = new(group.Key)
            {
                IsExpanded = _expandedFiles.Contains(group.Key)
            };
            IDisposable expandedSubscription = groupVm.WhenAnyValue(x => x.IsExpanded)
                .Subscribe(isExpanded => UpdateExpanded(group.Key, isExpanded));
            _groupDisposables.Add(expandedSubscription);
            foreach (ReferenceLocationItemViewModel item in group)
            {
                groupVm.Items.Add(item);
                TotalCount++;
            }

            Groups.Add(groupVm);
        }

        Model.Refresh();
        ApplyFilterAndSearch(FilterText);
    }

    private void UpdateExpanded(string filePath, bool isExpanded)
    {
        if (isExpanded)
        {
            _expandedFiles.Add(filePath);
        }
        else
        {
            _expandedFiles.Remove(filePath);
        }
    }

    private async Task OpenSelectedAsync()
    {
        if (SelectedItem is ReferenceLocationItemViewModel location)
        {
            await OpenLocationAsync(location);
        }
    }

    private Task OpenLocationAsync(ReferenceLocationItemViewModel location)
    {
        return _editor.OpenLocationAsync(location.Location, CancellationToken.None);
    }

    private void ApplyFilterAndSearch(string? text)
    {
        ApplyFiltering(text);
        ApplySearch(text);
    }

    private void ApplyFiltering(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            FilteringModel.Remove(FilterPropertyPath);
            return;
        }

        string query = text.Trim();
        HashSet<object> matches = BuildMatchSet(Groups, query);
        FilteringModel.SetOrUpdate(new FilteringDescriptor(
            columnId: FilterPropertyPath,
            @operator: FilteringOperator.Custom,
            propertyPath: FilterPropertyPath,
            predicate: item => MatchesFilter(item, matches)));
    }

    private void ApplySearch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            SearchModel.Clear();
            return;
        }

        string query = text.Trim();
        SearchModel.SetOrUpdate(new SearchDescriptor(
            query,
            matchMode: SearchMatchMode.Contains,
            termMode: SearchTermCombineMode.Any,
            scope: SearchScope.VisibleColumns,
            comparison: StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesFilter(object? item, HashSet<object> matches)
    {
        return item is not null && matches.Contains(item);
    }

    private static HashSet<object> BuildMatchSet(
        IEnumerable<ReferencesGroupViewModel> groups,
        string text)
    {
        HashSet<object> matches = new();
        foreach (ReferencesGroupViewModel group in groups)
        {
            CollectMatches(group, text, matches);
        }

        return matches;
    }

    private static bool CollectMatches(
        ReferencesGroupViewModel group,
        string text,
        HashSet<object> matches)
    {
        bool groupMatch = group.DisplayText.Contains(text, StringComparison.OrdinalIgnoreCase)
            || group.FilePath.Contains(text, StringComparison.OrdinalIgnoreCase);
        bool childMatch = false;

        foreach (ReferenceLocationItemViewModel item in group.Items)
        {
            if (item.DisplayText.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(item);
                childMatch = true;
            }
        }

        if (groupMatch)
        {
            matches.Add(group);
            foreach (ReferenceLocationItemViewModel item in group.Items)
            {
                matches.Add(item);
            }

            return true;
        }

        if (childMatch)
        {
            matches.Add(group);
            return true;
        }

        return false;
    }

    private async Task<LanguagePositionContext?> BuildPositionContextAsync(CancellationToken cancellationToken)
    {
        IEditorDocument? document = _editor.ActiveDocument;
        if (document is null)
        {
            await _window.ShowWarningMessageAsync(
                "No active document. Open a document and place the caret on a symbol first.",
                cancellationToken);
            return null;
        }

        string text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return new LanguagePositionContext
        {
            FilePath = document.FilePath,
            Text = text,
            Offset = document.CaretOffset
        };
    }

    private static IEnumerable<ReferenceLocationItemViewModel> BuildReferenceItems(
        IReadOnlyList<LanguageLocation> locations)
    {
        foreach (LanguageLocation location in locations)
        {
            yield return new ReferenceLocationItemViewModel(location);
        }
    }
}
