using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.DataGridHierarchical;
using ReactiveUI;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.TreeInspectorExtension;

public enum TreeKind
{
    Visual,
    Logical
}

public sealed class TreeInspectorNodeViewModel : ReactiveObject
{
    private bool _isExpanded;

    public TreeInspectorNodeViewModel(
        string nodeId,
        string nodeGlyph,
        string typeName,
        string? displayName,
        string? parentNodeId,
        int childCount)
    {
        NodeId = nodeId;
        NodeGlyph = nodeGlyph;
        TypeName = typeName;
        DisplayName = displayName;
        ParentNodeId = parentNodeId;
        ChildCount = childCount;
    }

    public string NodeId { get; }
    public string NodeGlyph { get; }
    public string TypeName { get; }
    public string? DisplayName { get; }
    public string? ParentNodeId { get; }
    public int ChildCount { get; }

    public ObservableCollection<TreeInspectorNodeViewModel> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public string DisplayText => string.IsNullOrWhiteSpace(DisplayName)
        ? TypeName
        : $"{TypeName} ({DisplayName})";
}

public sealed class TreeInspectorGridViewModel : ReactiveObject
{
    private HierarchicalNode? _selectedRow;
    private TreeInspectorNodeViewModel? _selectedNode;

    public TreeInspectorGridViewModel()
    {
        var options = new HierarchicalOptions
        {
            ChildrenSelector = item => ((TreeInspectorNodeViewModel)item).Children,
            IsLeafSelector = item => ((TreeInspectorNodeViewModel)item).Children.Count == 0,
            IsExpandedSelector = item => ((TreeInspectorNodeViewModel)item).IsExpanded,
            IsExpandedSetter = (item, value) => ((TreeInspectorNodeViewModel)item).IsExpanded = value,
            AutoExpandRoot = false,
            MaxAutoExpandDepth = 0,
            VirtualizeChildren = true
        };

        Model = new HierarchicalModel(options);
        Model.SetRoots(RootItems);

        this.WhenAnyValue(x => x.SelectedRow)
            .Subscribe(row => SelectedNode = row?.Item as TreeInspectorNodeViewModel);
    }

    public ObservableCollection<TreeInspectorNodeViewModel> RootItems { get; } = new();

    public HierarchicalModel Model { get; }

    public HierarchicalNode? SelectedRow
    {
        get => _selectedRow;
        set => this.RaiseAndSetIfChanged(ref _selectedRow, value);
    }

    public TreeInspectorNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        private set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
    }

    public void SetRoots(IEnumerable<TreeInspectorNodeViewModel> roots)
    {
        RootItems.Clear();
        foreach (TreeInspectorNodeViewModel root in roots)
        {
            RootItems.Add(root);
        }
    }

    public void SelectNode(TreeInspectorNodeViewModel? node)
    {
        SelectedNode = node;
        if (node is null)
        {
            SelectedRow = null;
            return;
        }

        Model.TryExpandToItem(node, out HierarchicalNode? found);
        SelectedRow = found ?? Model.FindNode(node);
    }
}

public sealed class TreeInspectorPanelViewModel : ReactiveObject, IDisposable
{
    private readonly IDesignerHost _designer;
    private readonly TreeKind _kind;
    private readonly CompositeDisposable _disposables = new();
    private readonly HashSet<string> _expandedNodeIds = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<DesignerNodeSummary> _snapshot = Array.Empty<DesignerNodeSummary>();
    private Dictionary<string, TreeInspectorNodeViewModel> _nodeIndex = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _loadCts;
    private string? _searchText;
    private bool _suspendSelection;
    private string? _lastActiveDocumentPath;
    private string? _lastSelectedNodeId;
    private string? _lastEmptyTreeRefreshPath;
    private bool _isPolling;
    private readonly string _nodeGlyph;
    private DateTime _lastSelectionUpdateUtc = DateTime.MinValue;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public TreeInspectorPanelViewModel(IDesignerHost designer, TreeKind kind)
    {
        _designer = designer;
        _kind = kind;
        _nodeGlyph = GetNodeGlyph(kind);
        TreeModel = new TreeInspectorGridViewModel();

        _disposables.Add(this.WhenAnyValue(x => x.SearchText)
            .Subscribe(_ => RebuildTree()));

        _disposables.Add(TreeModel.WhenAnyValue(x => x.SelectedNode)
            .Subscribe(node =>
            {
                if (_suspendSelection || node is null)
                {
                    return;
                }

                RunBackground(SelectNodeAsync(node.NodeId));
            }));

        _disposables.Add(Observable.Interval(PollInterval, RxApp.TaskpoolScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => { RunBackground(PollDesignerAsync()); }));
    }

    public TreeInspectorGridViewModel TreeModel { get; }
    public string PanelTitle => _kind == TreeKind.Visual ? "Visual Tree" : "Logical Tree";

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
    }

    public async Task HandleSelectionChangedAsync(
        IReadOnlyList<DesignerNodeSummary> selectedNodes,
        CancellationToken cancellationToken)
    {
        _lastSelectionUpdateUtc = DateTime.UtcNow;
        string? activeDocumentPath = _designer.ActiveDocumentPath;
        bool activeDocumentChanged = !string.Equals(
            _lastActiveDocumentPath,
            activeDocumentPath,
            StringComparison.OrdinalIgnoreCase);
        if (activeDocumentChanged)
        {
            await RefreshAsync(cancellationToken);
            return;
        }

        if (selectedNodes.Count == 0 && !string.IsNullOrWhiteSpace(activeDocumentPath))
        {
            try
            {
                IReadOnlyList<DesignerNodeSummary> refreshed = await _designer.GetSelectedNodesAsync(cancellationToken);
                if (refreshed.Count > 0)
                {
                    selectedNodes = refreshed;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        UpdateSelection(selectedNodes);
    }

    public Task HandleDocumentChangedAsync(string? _, CancellationToken cancellationToken)
    {
        return RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? previous = _loadCts;
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        previous?.Cancel();
        previous?.Dispose();

        CancellationToken token = _loadCts.Token;
        IReadOnlyList<DesignerNodeSummary> nodes = _kind == TreeKind.Visual
            ? await _designer.GetVisualTreeAsync(token)
            : await _designer.GetLogicalTreeAsync(token);

        _lastActiveDocumentPath = _designer.ActiveDocumentPath;
        _lastSelectionUpdateUtc = DateTime.UtcNow;
        if (!AreSnapshotsEquivalent(_snapshot, nodes))
        {
            _snapshot = nodes;
            RebuildTree();
        }

        IReadOnlyList<DesignerNodeSummary> selectedNodes = await _designer.GetSelectedNodesAsync(token);
        UpdateSelection(selectedNodes, refreshWhenMissing: false);
        if (nodes.Count > 0)
        {
            _lastEmptyTreeRefreshPath = null;
        }
    }

    public void UpdateSelection(IReadOnlyList<DesignerNodeSummary> selectedNodes, bool refreshWhenMissing = true)
    {
        _lastSelectionUpdateUtc = DateTime.UtcNow;
        if (selectedNodes.Count == 0)
        {
            if (TreeModel.SelectedNode is null && string.IsNullOrWhiteSpace(_lastSelectedNodeId))
            {
                return;
            }

            _lastSelectedNodeId = null;
            _suspendSelection = true;
            TreeModel.SelectNode(null);
            _suspendSelection = false;
            return;
        }

        string selectedId = selectedNodes[0].NodeId;
        if (string.Equals(_lastSelectedNodeId, selectedId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(TreeModel.SelectedNode?.NodeId, selectedId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastSelectedNodeId = selectedId;
        if (!_nodeIndex.TryGetValue(selectedId, out TreeInspectorNodeViewModel? match))
        {
            if (refreshWhenMissing && string.IsNullOrWhiteSpace(SearchText))
            {
                RunBackground(RefreshAsync(CancellationToken.None));
            }
            return;
        }

        _suspendSelection = true;
        TreeModel.SelectNode(match);
        _suspendSelection = false;
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _disposables.Dispose();
    }

    private async Task SelectNodeAsync(string nodeId)
    {
        try
        {
            CancellationToken token = _loadCts?.Token ?? CancellationToken.None;
            await _designer.SelectNodeAsync(nodeId, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    private async Task PollDesignerAsync()
    {
        if (_isPolling)
        {
            return;
        }

        _isPolling = true;
        try
        {
            string? activeDocumentPath = _designer.ActiveDocumentPath;
            if (string.IsNullOrWhiteSpace(activeDocumentPath))
            {
                if (TreeModel.RootItems.Count > 0)
                {
                    _snapshot = Array.Empty<DesignerNodeSummary>();
                    _nodeIndex.Clear();
                    _suspendSelection = true;
                    TreeModel.SetRoots(Array.Empty<TreeInspectorNodeViewModel>());
                    TreeModel.SelectNode(null);
                    _suspendSelection = false;
                }

                _lastActiveDocumentPath = null;
                _lastSelectedNodeId = null;
                _lastEmptyTreeRefreshPath = null;
                return;
            }

            bool documentChanged = !string.Equals(
                _lastActiveDocumentPath,
                activeDocumentPath,
                StringComparison.OrdinalIgnoreCase);
            if (documentChanged)
            {
                _lastEmptyTreeRefreshPath = null;
            }

            bool refreshEmptyTree = !string.IsNullOrWhiteSpace(activeDocumentPath)
                && TreeModel.RootItems.Count == 0
                && !string.Equals(_lastEmptyTreeRefreshPath, activeDocumentPath, StringComparison.OrdinalIgnoreCase);
            if (documentChanged || refreshEmptyTree)
            {
                await RefreshAsync(CancellationToken.None);
                if (TreeModel.RootItems.Count == 0 && !string.IsNullOrWhiteSpace(activeDocumentPath))
                {
                    _lastEmptyTreeRefreshPath = activeDocumentPath;
                }
            }

            if (DateTime.UtcNow - _lastSelectionUpdateUtc < PollInterval)
            {
                return;
            }

            IReadOnlyList<DesignerNodeSummary> selectedNodes = await _designer.GetSelectedNodesAsync(CancellationToken.None);
            string? selectedId = selectedNodes.Count > 0 ? selectedNodes[0].NodeId : null;
            if (!string.Equals(_lastSelectedNodeId, selectedId, StringComparison.OrdinalIgnoreCase))
            {
                UpdateSelection(selectedNodes);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
        finally
        {
            _isPolling = false;
        }
    }

    private void RebuildTree()
    {
        if (_snapshot.Count == 0 && TreeModel.RootItems.Count == 0)
        {
            if (_nodeIndex.Count > 0)
            {
                _nodeIndex = new Dictionary<string, TreeInspectorNodeViewModel>(StringComparer.OrdinalIgnoreCase);
            }

            return;
        }

        string? selectedId = TreeModel.SelectedNode?.NodeId;
        CollectExpandedIds();

        (List<TreeInspectorNodeViewModel> roots, Dictionary<string, TreeInspectorNodeViewModel> index) =
            BuildTree(_snapshot, SearchText, _expandedNodeIds, _nodeGlyph);

        TreeModel.SetRoots(roots);
        _nodeIndex = index;

        if (!string.IsNullOrWhiteSpace(selectedId) && _nodeIndex.TryGetValue(selectedId, out TreeInspectorNodeViewModel? selected))
        {
            _suspendSelection = true;
            TreeModel.SelectNode(selected);
            _suspendSelection = false;
        }
    }

    private void CollectExpandedIds()
    {
        _expandedNodeIds.Clear();
        foreach (TreeInspectorNodeViewModel root in TreeModel.RootItems)
        {
            CollectExpandedIds(root, _expandedNodeIds);
        }
    }

    private static void CollectExpandedIds(TreeInspectorNodeViewModel node, ISet<string> expandedIds)
    {
        if (node.IsExpanded)
        {
            expandedIds.Add(node.NodeId);
        }

        foreach (TreeInspectorNodeViewModel child in node.Children)
        {
            CollectExpandedIds(child, expandedIds);
        }
    }

    private static (List<TreeInspectorNodeViewModel> Roots, Dictionary<string, TreeInspectorNodeViewModel> Index) BuildTree(
        IReadOnlyList<DesignerNodeSummary> nodes,
        string? filter,
        ISet<string> expandedIds,
        string nodeGlyph)
    {
        Dictionary<string, TreeInspectorNodeViewModel> entries = new(nodes.Count, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<TreeInspectorNodeViewModel>> children = new(nodes.Count, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, TreeInspectorNodeViewModel> index = new(nodes.Count, StringComparer.OrdinalIgnoreCase);

        foreach (DesignerNodeSummary node in nodes)
        {
            TreeInspectorNodeViewModel entry = new(
                node.NodeId,
                nodeGlyph,
                node.TypeName,
                node.DisplayName,
                node.ParentNodeId,
                node.ChildCount);
            entries[node.NodeId] = entry;
        }

        foreach (TreeInspectorNodeViewModel entry in entries.Values)
        {
            if (string.IsNullOrWhiteSpace(entry.ParentNodeId))
            {
                continue;
            }

            if (!children.TryGetValue(entry.ParentNodeId, out List<TreeInspectorNodeViewModel>? list))
            {
                list = new List<TreeInspectorNodeViewModel>();
                children[entry.ParentNodeId] = list;
            }

            list.Add(entry);
        }

        foreach (List<TreeInspectorNodeViewModel> list in children.Values)
        {
            list.Sort((left, right) => string.Compare(
                left.DisplayName ?? left.TypeName,
                right.DisplayName ?? right.TypeName,
                StringComparison.OrdinalIgnoreCase));
        }

        List<TreeInspectorNodeViewModel> roots = new(entries.Count);
        foreach (TreeInspectorNodeViewModel entry in entries.Values)
        {
            if (string.IsNullOrWhiteSpace(entry.ParentNodeId)
                || !entries.ContainsKey(entry.ParentNodeId))
            {
                roots.Add(entry);
            }
        }

        roots.Sort((left, right) => string.Compare(
            left.DisplayName ?? left.TypeName,
            right.DisplayName ?? right.TypeName,
            StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(filter))
        {
            foreach (TreeInspectorNodeViewModel root in roots)
            {
                PopulateChildren(root, children, expandedIds, index);
            }

            return (roots, index);
        }

        string search = filter.Trim();
        List<TreeInspectorNodeViewModel> filteredRoots = new(roots.Count);
        foreach (TreeInspectorNodeViewModel root in roots)
        {
            TreeInspectorNodeViewModel? filtered = FilterTree(root, children, search, index);
            if (filtered is not null)
            {
                filteredRoots.Add(filtered);
            }
        }

        return (filteredRoots, index);
    }

    private static void PopulateChildren(
        TreeInspectorNodeViewModel node,
        IReadOnlyDictionary<string, List<TreeInspectorNodeViewModel>> children,
        ISet<string> expandedIds,
        IDictionary<string, TreeInspectorNodeViewModel> index)
    {
        index[node.NodeId] = node;
        node.IsExpanded = expandedIds.Contains(node.NodeId)
            || (expandedIds.Count == 0 && string.IsNullOrWhiteSpace(node.ParentNodeId));
        if (children.TryGetValue(node.NodeId, out List<TreeInspectorNodeViewModel>? list))
        {
            foreach (TreeInspectorNodeViewModel child in list)
            {
                node.Children.Add(child);
                PopulateChildren(child, children, expandedIds, index);
            }
        }
    }

    private static TreeInspectorNodeViewModel? FilterTree(
        TreeInspectorNodeViewModel node,
        IReadOnlyDictionary<string, List<TreeInspectorNodeViewModel>> children,
        string search,
        IDictionary<string, TreeInspectorNodeViewModel> index)
    {
        bool selfMatch = Matches(node, search);
        List<TreeInspectorNodeViewModel> filteredChildren = new();
        if (children.TryGetValue(node.NodeId, out List<TreeInspectorNodeViewModel>? list))
        {
            foreach (TreeInspectorNodeViewModel child in list)
            {
                TreeInspectorNodeViewModel? filtered = FilterTree(child, children, search, index);
                if (filtered is not null)
                {
                    filteredChildren.Add(filtered);
                }
            }
        }

        if (!selfMatch && filteredChildren.Count == 0)
        {
            return null;
        }

        TreeInspectorNodeViewModel clone = new(
            node.NodeId,
            node.NodeGlyph,
            node.TypeName,
            node.DisplayName,
            node.ParentNodeId,
            node.ChildCount)
        {
            IsExpanded = filteredChildren.Count > 0
        };

        foreach (TreeInspectorNodeViewModel child in filteredChildren)
        {
            clone.Children.Add(child);
        }

        index[clone.NodeId] = clone;
        return clone;
    }

    private static bool Matches(TreeInspectorNodeViewModel node, string search)
    {
        return node.DisplayText.Contains(search, StringComparison.OrdinalIgnoreCase)
            || node.TypeName.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AreSnapshotsEquivalent(
        IReadOnlyList<DesignerNodeSummary> current,
        IReadOnlyList<DesignerNodeSummary> next)
    {
        if (ReferenceEquals(current, next))
        {
            return true;
        }

        if (current.Count != next.Count)
        {
            return false;
        }

        for (int i = 0; i < current.Count; i++)
        {
            DesignerNodeSummary left = current[i];
            DesignerNodeSummary right = next[i];
            if (!string.Equals(left.NodeId, right.NodeId, StringComparison.Ordinal)
                || !string.Equals(left.TypeName, right.TypeName, StringComparison.Ordinal)
                || !string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
                || !string.Equals(left.ParentNodeId, right.ParentNodeId, StringComparison.Ordinal)
                || left.ChildCount != right.ChildCount)
            {
                return false;
            }
        }

        return true;
    }

    private static string GetNodeGlyph(TreeKind kind)
    {
        return kind == TreeKind.Visual ? "◆" : "○";
    }

    private static void RunBackground(Task task)
    {
        _ = task.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
