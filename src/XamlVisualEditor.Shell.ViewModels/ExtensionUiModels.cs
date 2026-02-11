using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

public sealed class ExtensionMenuItemViewModel : ReactiveObject
{
    public ExtensionMenuItemViewModel(string commandId, string title, string? group, ReactiveCommand<Unit, Unit> command)
    {
        CommandId = commandId;
        Title = title;
        Group = group;
        Command = command;
    }

    public string CommandId { get; }

    public string Title { get; }

    public string? Group { get; }

    public ReactiveCommand<Unit, Unit> Command { get; }
}

public sealed class ExtensionToolbarItemViewModel : ReactiveObject
{
    public ExtensionToolbarItemViewModel(string commandId, string title, string? tooltip, string? group, ReactiveCommand<Unit, Unit> command)
    {
        CommandId = commandId;
        Title = title;
        Tooltip = tooltip;
        Group = group;
        Command = command;
    }

    public string CommandId { get; }

    public string Title { get; }

    public string? Tooltip { get; }

    public string? Group { get; }

    public ReactiveCommand<Unit, Unit> Command { get; }
}

public sealed class ExtensionCommandPaletteItemViewModel : ReactiveObject
{
    public ExtensionCommandPaletteItemViewModel(string commandId, string title, string? category)
    {
        CommandId = commandId;
        Title = title;
        Category = category;
    }

    public string CommandId { get; }

    public string Title { get; }

    public string? Category { get; }
}

public sealed record CommandPaletteRequest(string Title, IReadOnlyList<ExtensionCommandPaletteItemViewModel> Items);

public sealed class CommandPaletteDialogViewModel : ReactiveObject
{
    public CommandPaletteDialogViewModel(CommandPaletteRequest request)
    {
        Title = request.Title;
        foreach (ExtensionCommandPaletteItemViewModel item in request.Items)
        {
            Items.Add(item);
            FilteredItems.Add(item);
        }

        if (FilteredItems.Count > 0)
        {
            SelectedItem = FilteredItems[0];
        }

        ApplyCommand = ReactiveCommand.CreateFromTask(async () =>
            await CloseInteraction.Handle(SelectedItem));
        CancelCommand = ReactiveCommand.CreateFromTask(async () =>
            await CloseInteraction.Handle(null));

        this.WhenAnyValue(x => x.FilterText)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ApplyFilter);
    }

    public string Title { get; }

    public ObservableCollection<ExtensionCommandPaletteItemViewModel> Items { get; } = new();

    public ObservableCollection<ExtensionCommandPaletteItemViewModel> FilteredItems { get; } = new();

    [Reactive]
    public ExtensionCommandPaletteItemViewModel? SelectedItem { get; set; }

    [Reactive]
    public string? FilterText { get; set; }

    public Interaction<ExtensionCommandPaletteItemViewModel?, Unit> CloseInteraction { get; } = new();

    public ReactiveCommand<Unit, Unit> ApplyCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private void ApplyFilter(string? text)
    {
        FilteredItems.Clear();
        IEnumerable<ExtensionCommandPaletteItemViewModel> filtered = Items;

        if (!string.IsNullOrWhiteSpace(text))
        {
            string query = text.Trim();
            filtered = Items.Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (item.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        foreach (ExtensionCommandPaletteItemViewModel item in filtered)
        {
            FilteredItems.Add(item);
        }

        SelectedItem = FilteredItems.Count > 0 ? FilteredItems[0] : null;
    }
}

public abstract class ExtensionViewModel : ReactiveObject
{
    protected ExtensionViewModel(ExtensionViewContribution contribution)
    {
        ViewId = contribution.ViewId;
        Title = contribution.Title;
        ViewType = contribution.Type;
        Location = contribution.Location;
        Priority = contribution.Priority;
    }

    public string ViewId { get; }

    public string Title { get; }

    public ExtensionViewType ViewType { get; }

    public ExtensionViewLocation Location { get; }

    public int Priority { get; }
}

public sealed class ExtensionWebviewViewModel : ExtensionViewModel
{
    public ExtensionWebviewViewModel(ExtensionViewContribution contribution, string placeholderMessage)
        : base(contribution)
    {
        PlaceholderMessage = placeholderMessage;
    }

    public string PlaceholderMessage { get; }
}

public sealed class ExtensionCustomViewModel : ExtensionViewModel
{
    public ExtensionCustomViewModel(ExtensionViewContribution contribution, object? viewModel)
        : base(contribution)
    {
        ViewModel = viewModel;
    }

    public object? ViewModel { get; }
}

public sealed class ExtensionTreeViewModel : ExtensionViewModel, IDisposable
{
    private const string FilterPropertyPath = "Item.Label";
    private readonly IExtensionTreeDataProvider _provider;
    private bool _isDisposed;

    public ExtensionTreeViewModel(ExtensionViewContribution contribution, IExtensionTreeDataProvider provider)
        : base(contribution)
    {
        _provider = provider;

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
            ChildrenSelector = item => ((ExtensionTreeNodeViewModel)item).Children,
            IsLeafSelector = item => item is ExtensionTreeNodeViewModel node && !node.HasChildren,
            IsExpandedSelector = item => ((ExtensionTreeNodeViewModel)item).IsExpanded,
            IsExpandedSetter = (item, value) => SetExpanded(item, value),
            VirtualizeChildren = true
        };

        Model = new HierarchicalModel(options);
        Model.SetRoots(RootItems);

        this.WhenAnyValue(x => x.FilterText)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ApplyFilterAndSearch);

        _provider.Changed += OnProviderChanged;
    }

    public ObservableCollection<ExtensionTreeNodeViewModel> RootItems { get; } = new();

    public HierarchicalModel Model { get; }

    public SortingModel SortingModel { get; }

    public FilteringModel FilteringModel { get; }

    public SearchModel SearchModel { get; }

    [Reactive]
    public HierarchicalNode? SelectedRow { get; set; }

    [Reactive]
    public string? FilterText { get; set; }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<object> items = await _provider.GetChildrenAsync(null, cancellationToken).ConfigureAwait(false);
        List<ExtensionTreeNodeViewModel> nodes = new(items.Count);

        foreach (object item in items)
        {
            TreeItem treeItem = await _provider.GetTreeItemAsync(item, cancellationToken).ConfigureAwait(false);
            nodes.Add(new ExtensionTreeNodeViewModel(item, treeItem, _provider, RefreshModel));
        }

        RootItems.Clear();
        foreach (ExtensionTreeNodeViewModel node in nodes)
        {
            RootItems.Add(node);
        }

        Model.Refresh();
        ApplyFilterAndSearch(FilterText);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _provider.Changed -= OnProviderChanged;
        _isDisposed = true;
    }

    private void OnProviderChanged(object? sender, EventArgs e)
    {
        RxApp.MainThreadScheduler.Schedule(() => _ = ReloadAsync(CancellationToken.None));
    }

    private void SetExpanded(object item, bool value)
    {
        if (item is ExtensionTreeNodeViewModel node)
        {
            node.IsExpanded = value;
            if (value)
            {
                _ = node.EnsureChildrenAsync(CancellationToken.None);
            }
        }
    }

    private void RefreshModel()
    {
        Model.Refresh();
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
            RefreshModel();
            return;
        }

        string query = text.Trim();
        HashSet<object> matches = BuildMatchSet(RootItems, query);
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
            RefreshModel();
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
        IEnumerable<ExtensionTreeNodeViewModel> roots,
        string text)
    {
        HashSet<object> matches = new();
        foreach (ExtensionTreeNodeViewModel root in roots)
        {
            CollectMatches(root, text, matches);
        }

        return matches;
    }

    private static bool CollectMatches(
        ExtensionTreeNodeViewModel node,
        string text,
        HashSet<object> matches)
    {
        bool matchesSelf = node.Label.Contains(text, StringComparison.OrdinalIgnoreCase)
            || (node.Description?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);

        bool matchesChild = false;
        foreach (ExtensionTreeNodeViewModel child in node.Children)
        {
            if (CollectMatches(child, text, matches))
            {
                matchesChild = true;
            }
        }

        if (matchesSelf || matchesChild)
        {
            matches.Add(node);
        }

        return matchesSelf || matchesChild;
    }
}

public sealed class ExtensionTreeNodeViewModel : ReactiveObject
{
    private readonly IExtensionTreeDataProvider _provider;
    private readonly Action _refresh;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private bool _isLoaded;

    public ExtensionTreeNodeViewModel(
        object element,
        TreeItem item,
        IExtensionTreeDataProvider provider,
        Action refresh)
    {
        Element = element;
        Label = item.Label;
        Description = item.Description;
        ContextValue = item.ContextValue;
        _provider = provider;
        _refresh = refresh;
    }

    public object Element { get; }

    public string Label { get; }

    public string? Description { get; }

    public string? ContextValue { get; }

    public ObservableCollection<ExtensionTreeNodeViewModel> Children { get; } = new();

    [Reactive]
    public bool IsExpanded { get; set; }

    [Reactive]
    public bool HasChildren { get; private set; } = true;

    public async Task EnsureChildrenAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded)
        {
            return;
        }

        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isLoaded)
            {
                return;
            }

            IReadOnlyList<object> items = await _provider.GetChildrenAsync(Element, cancellationToken)
                .ConfigureAwait(false);
            List<ExtensionTreeNodeViewModel> nodes = new(items.Count);

            foreach (object item in items)
            {
                TreeItem treeItem = await _provider.GetTreeItemAsync(item, cancellationToken).ConfigureAwait(false);
                nodes.Add(new ExtensionTreeNodeViewModel(item, treeItem, _provider, _refresh));
            }

            Children.Clear();
            foreach (ExtensionTreeNodeViewModel node in nodes)
            {
                Children.Add(node);
            }

            HasChildren = Children.Count > 0;
            _isLoaded = true;
            _refresh();
        }
        finally
        {
            _loadGate.Release();
        }
    }
}
