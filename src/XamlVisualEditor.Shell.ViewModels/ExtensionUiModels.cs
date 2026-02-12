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
    public ExtensionMenuItemViewModel(
        string commandId,
        string title,
        string? location,
        string? group,
        int priority,
        ReactiveCommand<Unit, Unit> command)
    {
        CommandId = commandId;
        Title = title;
        Location = location;
        Group = group;
        Priority = priority;
        Command = command;
    }

    public string CommandId { get; }

    public string Title { get; }

    public string? Location { get; }

    public string? Group { get; }

    public int Priority { get; }

    public ReactiveCommand<Unit, Unit> Command { get; }
}


public sealed class ExtensionToolbarItemViewModel : ReactiveObject
{
    public ExtensionToolbarItemViewModel(
        string commandId,
        string title,
        string? tooltip,
        string? location,
        string? group,
        int priority,
        ReactiveCommand<Unit, Unit> command)
    {
        CommandId = commandId;
        Title = title;
        Tooltip = tooltip;
        Location = location;
        Group = group;
        Priority = priority;
        Command = command;
    }

    public string CommandId { get; }

    public string Title { get; }

    public string? Tooltip { get; }

    public string? Location { get; }

    public string? Group { get; }

    public int Priority { get; }

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

        RefreshCommand = ReactiveCommand.CreateFromTask(ct => ReloadAsync(ct));
        CollapseAllCommand = ReactiveCommand.Create(CollapseAll);
        NewFileCommand = ReactiveCommand.CreateFromTask(_ => InvokeSelectedAsync(node => node.NewFileCommand));
        NewFolderCommand = ReactiveCommand.CreateFromTask(_ => InvokeSelectedAsync(node => node.NewFolderCommand));

        this.WhenAnyValue(x => x.SelectedRow)
            .Select(row => row?.Item as ExtensionTreeNodeViewModel)
            .BindTo(this, x => x.SelectedNode);

        _provider.Changed += OnProviderChanged;
    }

    public ObservableCollection<ExtensionTreeNodeViewModel> RootItems { get; } = new();

    public HierarchicalModel Model { get; }

    public SortingModel SortingModel { get; }

    public FilteringModel FilteringModel { get; }

    public SearchModel SearchModel { get; }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public ReactiveCommand<Unit, Unit> CollapseAllCommand { get; }

    public ReactiveCommand<Unit, Unit> NewFileCommand { get; }

    public ReactiveCommand<Unit, Unit> NewFolderCommand { get; }

    [Reactive]
    public HierarchicalNode? SelectedRow { get; set; }

    [Reactive]
    public ExtensionTreeNodeViewModel? SelectedNode { get; set; }

    [Reactive]
    public string? FilterText { get; set; }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        HashSet<string> expandedKeys = CollectExpandedKeys(RootItems);
        IReadOnlyList<object> items = await _provider.GetChildrenAsync(null, cancellationToken).ConfigureAwait(false);
        List<ExtensionTreeNodeViewModel> nodes = new(items.Count);

        foreach (object item in items)
        {
            TreeItem treeItem = await _provider.GetTreeItemAsync(item, cancellationToken).ConfigureAwait(false);
            nodes.Add(new ExtensionTreeNodeViewModel(item, treeItem, _provider, RefreshModel));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await ScheduleOnMainThreadAsync(() =>
        {
            RootItems.Clear();
            foreach (ExtensionTreeNodeViewModel node in nodes)
            {
                RootItems.Add(node);
            }

            Model.Refresh();
            ApplyFilterAndSearch(FilterText);
        });

        if (expandedKeys.Count > 0)
        {
            await ScheduleOnMainThreadAsync(() =>
            {
                _ = RestoreExpandedNodesAsync(RootItems, expandedKeys, cancellationToken);
            });
        }
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

    private static Task ScheduleOnMainThreadAsync(Action action)
    {
        TaskCompletionSource<Unit> tcs = new();
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            try
            {
                action();
                tcs.SetResult(Unit.Default);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    private static HashSet<string> CollectExpandedKeys(IEnumerable<ExtensionTreeNodeViewModel> roots)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (ExtensionTreeNodeViewModel root in roots)
        {
            CollectExpandedKeys(root, keys);
        }

        return keys;
    }

    private static void CollectExpandedKeys(ExtensionTreeNodeViewModel node, HashSet<string> keys)
    {
        if (node.IsPlaceholder)
        {
            return;
        }

        if (node.IsExpanded)
        {
            string? key = GetExpansionKey(node);
            if (!string.IsNullOrWhiteSpace(key))
            {
                keys.Add(key);
            }
        }

        foreach (ExtensionTreeNodeViewModel child in node.Children)
        {
            CollectExpandedKeys(child, keys);
        }
    }

    private static string? GetExpansionKey(ExtensionTreeNodeViewModel node)
    {
        return string.IsNullOrWhiteSpace(node.ContextValue) ? null : node.ContextValue;
    }

    private async Task RestoreExpandedNodesAsync(
        IEnumerable<ExtensionTreeNodeViewModel> roots,
        HashSet<string> expandedKeys,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (ExtensionTreeNodeViewModel root in roots)
            {
                await RestoreExpandedNodeAsync(root, expandedKeys, cancellationToken);
            }

            ApplyFilterAndSearch(FilterText);
        }
        catch
        {
        }
    }

    private async Task RestoreExpandedNodeAsync(
        ExtensionTreeNodeViewModel node,
        HashSet<string> expandedKeys,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || node.IsPlaceholder)
        {
            return;
        }

        string? key = GetExpansionKey(node);
        if (!string.IsNullOrWhiteSpace(key) && expandedKeys.Contains(key))
        {
            node.IsExpanded = true;
            await node.EnsureChildrenAsync(cancellationToken);
        }

        if (!node.IsExpanded)
        {
            return;
        }

        foreach (ExtensionTreeNodeViewModel child in node.Children)
        {
            await RestoreExpandedNodeAsync(child, expandedKeys, cancellationToken);
        }
    }

    private void CollapseAll()
    {
        foreach (ExtensionTreeNodeViewModel node in RootItems)
        {
            CollapseNode(node);
        }

        Model.Refresh();
    }

    private async Task InvokeSelectedAsync(Func<ExtensionTreeNodeViewModel, ReactiveCommand<Unit, Unit>?> selector)
    {
        ExtensionTreeNodeViewModel? target = SelectedNode ?? RootItems.FirstOrDefault();
        ReactiveCommand<Unit, Unit>? command = target is null ? null : selector(target);
        if (command is null)
        {
            return;
        }

        await command.Execute().FirstAsync();
    }

    private static void CollapseNode(ExtensionTreeNodeViewModel node)
    {
        node.IsExpanded = false;
        foreach (ExtensionTreeNodeViewModel child in node.Children)
        {
            CollapseNode(child);
        }
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
        if (item is null)
        {
            return false;
        }

        if (item is HierarchicalNode hierarchical)
        {
            return hierarchical.Item is not null && matches.Contains(hierarchical.Item);
        }

        return matches.Contains(item);
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
        if (node.IsPlaceholder)
        {
            return false;
        }

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
    private readonly IExtensionTreeItemActionProvider? _actionProvider;
    private readonly IExtensionTreeItemOperationsProvider? _operationsProvider;
    private readonly IExtensionTreeItemChildrenProvider? _childrenProvider;
    private readonly IExtensionTreeItemWorkspaceProvider? _workspaceProvider;

    public ExtensionTreeNodeViewModel(
        object element,
        TreeItem item,
        IExtensionTreeDataProvider provider,
        Action refresh,
        bool isPlaceholder = false)
    {
        Element = element;
        Label = item.Label;
        Description = item.Description;
        ContextValue = item.ContextValue;
        Icon = item.Icon;
        _provider = provider;
        _refresh = refresh;
        IsPlaceholder = isPlaceholder;
        _actionProvider = element as IExtensionTreeItemActionProvider;
        _operationsProvider = element as IExtensionTreeItemOperationsProvider;
        _childrenProvider = element as IExtensionTreeItemChildrenProvider;
        _workspaceProvider = element as IExtensionTreeItemWorkspaceProvider;

        if (_childrenProvider is not null)
        {
            HasChildren = _childrenProvider.HasChildren;
        }
        else if (IsPlaceholder)
        {
            HasChildren = false;
        }

        if (_operationsProvider is not null)
        {
            OpenCommand = CreateCommand(_operationsProvider.CanOpen, ct => _operationsProvider.OpenAsync(ct));
            RenameCommand = CreateCommand(_operationsProvider.CanRename, ct => _operationsProvider.RenameAsync(ct));
            DeleteCommand = CreateCommand(_operationsProvider.CanDelete, ct => _operationsProvider.DeleteAsync(ct));
            NewFileCommand = CreateCommand(_operationsProvider.CanCreateFile, ct => _operationsProvider.CreateFileAsync(ct));
            NewFolderCommand = CreateCommand(_operationsProvider.CanCreateFolder, ct => _operationsProvider.CreateFolderAsync(ct));
        }
        else if (_actionProvider is not null)
        {
            OpenCommand = CreateCommand(_actionProvider.CanOpen, ct => _actionProvider.OpenAsync(ct));
        }

        if (_workspaceProvider is not null)
        {
            OpenWorkspaceCommand = CreateCommand(_workspaceProvider.CanOpenWorkspace, ct => _workspaceProvider.OpenWorkspaceAsync(ct));
            CanOpenWorkspace = _workspaceProvider.CanOpenWorkspace;
        }
    }

    public object Element { get; }

    public string Label { get; }

    public string? Description { get; }

    public string? ContextValue { get; }

    public object? Icon { get; }

    public bool IsPlaceholder { get; }

    public ReactiveCommand<Unit, Unit>? OpenCommand { get; }

    public ReactiveCommand<Unit, Unit>? NewFileCommand { get; }

    public ReactiveCommand<Unit, Unit>? NewFolderCommand { get; }

    public ReactiveCommand<Unit, Unit>? RenameCommand { get; }

    public ReactiveCommand<Unit, Unit>? DeleteCommand { get; }

    public ReactiveCommand<Unit, Unit>? OpenWorkspaceCommand { get; }

    public bool CanOpenWorkspace { get; }

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

        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            if (_isLoaded)
            {
                return;
            }

            if (Children.Count == 0)
            {
                Children.Add(CreateLoadingPlaceholder(_provider, _refresh));
                _refresh();
            }

            IReadOnlyList<object> items = await _provider.GetChildrenAsync(Element, cancellationToken);
            List<ExtensionTreeNodeViewModel> nodes = new(items.Count);

            foreach (object item in items)
            {
                TreeItem treeItem = await _provider.GetTreeItemAsync(item, cancellationToken);
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

    private static ExtensionTreeNodeViewModel CreateLoadingPlaceholder(
        IExtensionTreeDataProvider provider,
        Action refresh)
    {
        TreeItem item = new("Loading...", null, null);
        return new ExtensionTreeNodeViewModel(new LoadingPlaceholder(), item, provider, refresh, isPlaceholder: true);
    }

    private sealed class LoadingPlaceholder
    {
    }

    private static ReactiveCommand<Unit, Unit> CreateCommand(bool canExecute, Func<CancellationToken, Task> execute)
    {
        IObservable<bool> canRun = Observable.Return(canExecute);
        return ReactiveCommand.CreateFromTask(execute, canRun);
    }
}
