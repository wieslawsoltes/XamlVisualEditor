using Avalonia.Controls;
using XamlVisualEditor.TreeView;

namespace XamlVisualEditor.App.Views;

/// <summary>
/// Code-behind for the logical tree panel view.
/// Populates the TreeView with the root node wrapped in a single-item array
/// so the root is visible as the first expandable node.
/// </summary>
public sealed partial class LogicalTreePanelView : UserControl
{
    public LogicalTreePanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Avalonia.Controls.TreeView? tree = this.FindControl<Avalonia.Controls.TreeView>("LogicalTree");
        if (tree is null)
        {
            return;
        }

        if (DataContext is LogicalTreeNodeViewModel root)
        {
            tree.ItemsSource = new[] { root };
        }
        else
        {
            tree.ItemsSource = null;
        }
    }
}
