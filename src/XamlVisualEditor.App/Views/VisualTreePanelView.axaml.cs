using Avalonia.Controls;
using XamlVisualEditor.TreeView;

namespace XamlVisualEditor.App.Views;

/// <summary>
/// Code-behind for the visual tree panel view.
/// Populates the TreeView with the root node wrapped in a single-item array
/// so the root is visible as the first expandable node.
/// </summary>
public sealed partial class VisualTreePanelView : UserControl
{
    public VisualTreePanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Avalonia.Controls.TreeView? tree = this.FindControl<Avalonia.Controls.TreeView>("VisualTree");
        if (tree is null)
        {
            return;
        }

        if (DataContext is VisualTreeNodeViewModel root)
        {
            tree.ItemsSource = new[] { root };
        }
        else
        {
            tree.ItemsSource = null;
        }
    }
}
