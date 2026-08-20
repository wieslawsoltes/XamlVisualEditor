using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridHierarchical;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace XamlVisualEditor.TreeView;

/// <summary>
/// ViewModel for the visual tree grid panel.
/// </summary>
public sealed partial class VisualTreeGridViewModel : ReactiveObject
{
    /// <summary>
    /// Gets the root items for the hierarchical model.
    /// </summary>
    public ObservableCollection<VisualTreeNodeViewModel> RootItems { get; } = new();

    /// <summary>
    /// Gets the hierarchical model backing the grid.
    /// </summary>
    public HierarchicalModel Model { get; }

    /// <summary>
    /// Gets or sets the selected row in the grid.
    /// </summary>
    [Reactive]
    public partial HierarchicalNode? SelectedRow { get; set; }

    /// <summary>
    /// Gets the selected node item.
    /// </summary>
    [Reactive]
    public partial VisualTreeNodeViewModel? SelectedNode { get; private set; }

    /// <summary>
    /// Gets the current root node.
    /// </summary>
    [Reactive]
    public partial VisualTreeNodeViewModel? Root { get; private set; }

    public VisualTreeGridViewModel()
    {
        var options = new HierarchicalOptions
        {
            ChildrenSelector = item => ((VisualTreeNodeViewModel)item).Children,
            IsLeafSelector = item => ((VisualTreeNodeViewModel)item).Children.Count == 0,
            IsExpandedSelector = item => ((VisualTreeNodeViewModel)item).IsExpanded,
            IsExpandedSetter = (item, value) => ((VisualTreeNodeViewModel)item).IsExpanded = value,
            AutoExpandRoot = false,
            MaxAutoExpandDepth = 0,
            VirtualizeChildren = true
        };

        Model = new HierarchicalModel(options);
        Model.SetRoots(RootItems);

        this.WhenAnyValue(x => x.SelectedRow)
            .Subscribe(row => SelectedNode = row?.Item as VisualTreeNodeViewModel);
    }

    /// <summary>
    /// Sets the root node for the grid.
    /// </summary>
    public void SetRoot(VisualTreeNodeViewModel? root)
    {
        Root = root;
        RootItems.Clear();
        if (root is not null)
        {
            RootItems.Add(root);
        }
    }

    /// <summary>
    /// Selects a specific node in the grid.
    /// </summary>
    public void SelectNode(VisualTreeNodeViewModel? node)
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

/// <summary>
/// ViewModel for the logical tree grid panel.
/// </summary>
public sealed partial class LogicalTreeGridViewModel : ReactiveObject
{
    /// <summary>
    /// Gets the root items for the hierarchical model.
    /// </summary>
    public ObservableCollection<LogicalTreeNodeViewModel> RootItems { get; } = new();

    /// <summary>
    /// Gets the hierarchical model backing the grid.
    /// </summary>
    public HierarchicalModel Model { get; }

    /// <summary>
    /// Gets or sets the selected row in the grid.
    /// </summary>
    [Reactive]
    public partial HierarchicalNode? SelectedRow { get; set; }

    /// <summary>
    /// Gets the selected node item.
    /// </summary>
    [Reactive]
    public partial LogicalTreeNodeViewModel? SelectedNode { get; private set; }

    /// <summary>
    /// Gets the current root node.
    /// </summary>
    [Reactive]
    public partial LogicalTreeNodeViewModel? Root { get; private set; }

    public LogicalTreeGridViewModel()
    {
        var options = new HierarchicalOptions
        {
            ChildrenSelector = item => ((LogicalTreeNodeViewModel)item).Children,
            IsLeafSelector = item => ((LogicalTreeNodeViewModel)item).Children.Count == 0,
            IsExpandedSelector = item => ((LogicalTreeNodeViewModel)item).IsExpanded,
            IsExpandedSetter = (item, value) => ((LogicalTreeNodeViewModel)item).IsExpanded = value,
            AutoExpandRoot = false,
            MaxAutoExpandDepth = 0,
            VirtualizeChildren = true
        };

        Model = new HierarchicalModel(options);
        Model.SetRoots(RootItems);

        this.WhenAnyValue(x => x.SelectedRow)
            .Subscribe(row => SelectedNode = row?.Item as LogicalTreeNodeViewModel);
    }

    /// <summary>
    /// Sets the root node for the grid.
    /// </summary>
    public void SetRoot(LogicalTreeNodeViewModel? root)
    {
        Root = root;
        RootItems.Clear();
        if (root is not null)
        {
            RootItems.Add(root);
        }
    }

    /// <summary>
    /// Selects a specific node in the grid.
    /// </summary>
    public void SelectNode(LogicalTreeNodeViewModel? node)
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
