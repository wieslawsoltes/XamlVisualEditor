using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

public sealed class SolutionExplorerOpenBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<SolutionExplorerOpenBehavior, Control, bool>("IsEnabled");

    static SolutionExplorerOpenBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Control control)
    {
        return control.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(Control control, bool value)
    {
        control.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && e.OldValue is false)
        {
            control.AddHandler(
                InputElement.PointerPressedEvent,
                OnPointerPressed,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
        }
        else if (e.NewValue is false && e.OldValue is true)
        {
            control.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        }
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        if (!IsPrimaryDoubleClick(dataGrid, e))
        {
            return;
        }

        SolutionExplorerNodeViewModel? node = GetNodeFromEvent(dataGrid, e);
        if (node?.OpenCommand is null)
        {
            return;
        }

        if (node.Kind is not (SolutionExplorerNodeKind.File or SolutionExplorerNodeKind.XamlFile))
        {
            return;
        }

        if (node.OpenCommand is ICommand command && command.CanExecute(null))
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    private static SolutionExplorerNodeViewModel? GetNodeFromEvent(DataGrid dataGrid, PointerPressedEventArgs e)
    {
        Visual? source = e.Source as Visual;
        DataGridRow? row = source?.GetVisualAncestors().OfType<DataGridRow>().FirstOrDefault();
        if (row is null)
        {
            Point position = e.GetPosition(dataGrid);
            if (dataGrid.InputHitTest(position) is Visual hit)
            {
                row = hit.GetVisualAncestors().OfType<DataGridRow>().FirstOrDefault();
            }
        }

        if (row?.DataContext is null)
        {
            return null;
        }

        if (!Equals(dataGrid.SelectedItem, row.DataContext))
        {
            dataGrid.SelectedItem = row.DataContext;
        }

        return ResolveNode(row.DataContext);
    }

    private static SolutionExplorerNodeViewModel? ResolveNode(object? dataContext)
    {
        return dataContext switch
        {
            SolutionExplorerNodeViewModel node => node,
            HierarchicalNode<SolutionExplorerNodeViewModel> node => node.Item,
            HierarchicalNode node => node.Item as SolutionExplorerNodeViewModel,
            _ => null
        };
    }

    private static bool IsPrimaryDoubleClick(DataGrid dataGrid, PointerPressedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(dataGrid);
        return point.Properties.IsLeftButtonPressed && e.ClickCount == 2;
    }
}
