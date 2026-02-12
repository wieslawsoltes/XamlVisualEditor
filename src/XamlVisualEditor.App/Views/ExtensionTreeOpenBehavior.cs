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

public sealed class ExtensionTreeOpenBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ExtensionTreeOpenBehavior, Control, bool>("IsEnabled");

    static ExtensionTreeOpenBehavior()
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
            control.AddHandler(InputElement.DoubleTappedEvent, OnDoubleTapped, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        }
        else if (e.NewValue is false && e.OldValue is true)
        {
            control.RemoveHandler(InputElement.DoubleTappedEvent, OnDoubleTapped);
        }
    }

    private static void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        ExtensionTreeNodeViewModel? node = GetNodeFromEvent(e);
        if (node?.OpenCommand is null)
        {
            return;
        }

        if (node.OpenCommand is ICommand command && command.CanExecute(null))
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    private static ExtensionTreeNodeViewModel? GetNodeFromEvent(RoutedEventArgs e)
    {
        if (e.Source is not Visual source)
        {
            return null;
        }

        DataGridRow? row = source.GetVisualAncestors().OfType<DataGridRow>().FirstOrDefault();
        if (row?.DataContext is HierarchicalNode node)
        {
            return node.Item as ExtensionTreeNodeViewModel;
        }

        return null;
    }
}
