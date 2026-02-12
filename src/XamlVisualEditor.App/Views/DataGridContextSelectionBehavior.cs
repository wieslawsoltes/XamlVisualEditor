using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace XamlVisualEditor.App.Views;

public sealed class DataGridContextSelectionBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<DataGridContextSelectionBehavior, Control, bool>("IsEnabled");

    static DataGridContextSelectionBehavior()
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
            control.AddHandler(
                Control.ContextRequestedEvent,
                OnContextRequested,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
        }
        else if (e.NewValue is false && e.OldValue is true)
        {
            control.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            control.RemoveHandler(Control.ContextRequestedEvent, OnContextRequested);
        }
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(dataGrid);
        if (!IsContextClick(point, e))
        {
            return;
        }

        TrySelectRow(dataGrid, e.Source as Visual);
    }

    private static void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        if (TrySelectRow(dataGrid, e.Source as Visual))
        {
            return;
        }

        if (e.TryGetPosition(dataGrid, out Point position))
        {
            if (dataGrid.InputHitTest(position) is Visual hit)
            {
                _ = TrySelectRow(dataGrid, hit);
            }
        }
    }

    private static bool TrySelectRow(DataGrid dataGrid, Visual? source)
    {
        if (source is null)
        {
            return false;
        }

        DataGridRow? row = source.GetVisualAncestors().OfType<DataGridRow>().FirstOrDefault();
        if (row?.DataContext is null)
        {
            return false;
        }

        if (!Equals(dataGrid.SelectedItem, row.DataContext))
        {
            dataGrid.SelectedItem = row.DataContext;
        }

        return true;
    }

    private static bool IsContextClick(PointerPoint point, PointerPressedEventArgs e)
    {
        if (point.Properties.IsRightButtonPressed)
        {
            return true;
        }

        return OperatingSystem.IsMacOS()
               && point.Properties.IsLeftButtonPressed
               && e.KeyModifiers.HasFlag(KeyModifiers.Control);
    }
}
