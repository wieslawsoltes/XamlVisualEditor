using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

public sealed class SolutionExplorerDragBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<SolutionExplorerDragBehavior, Control, bool>("IsEnabled");

    private static readonly AttachedProperty<DragState?> DragStateProperty =
        AvaloniaProperty.RegisterAttached<SolutionExplorerDragBehavior, Control, DragState?>("DragState");

    static SolutionExplorerDragBehavior()
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
            control.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            control.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            control.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        }
        else if (e.NewValue is false && e.OldValue is true)
        {
            control.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            control.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
            control.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        }
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(control);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        SolutionExplorerNodeViewModel? node = GetNodeFromEvent(e);
        if (node is null || string.IsNullOrWhiteSpace(node.FullPath))
        {
            return;
        }

        if (node.Kind is not (SolutionExplorerNodeKind.File or SolutionExplorerNodeKind.XamlFile))
        {
            return;
        }

        DragState state = new()
        {
            Start = e.GetPosition(control),
            FilePath = node.FullPath!
        };

        control.SetValue(DragStateProperty, state);
    }

    private static async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        DragState? state = control.GetValue(DragStateProperty);
        if (state is null || string.IsNullOrWhiteSpace(state.FilePath))
        {
            return;
        }

        Point current = e.GetPosition(control);
        if (Math.Abs(current.X - state.Start.X) < 4 && Math.Abs(current.Y - state.Start.Y) < 4)
        {
            return;
        }

        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            control.ClearValue(DragStateProperty);
            return;
        }

        e.Pointer.Capture(control);

        DataTransfer data = new();
        data.Add(DataTransferItem.CreateText(state.FilePath));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);

        if (e.Pointer.Captured == control)
        {
            e.Pointer.Capture(null);
        }

        control.ClearValue(DragStateProperty);
        e.Handled = true;
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        if (e.Pointer.Captured == control)
        {
            e.Pointer.Capture(null);
        }

        control.ClearValue(DragStateProperty);
    }

    private static SolutionExplorerNodeViewModel? GetNodeFromEvent(PointerEventArgs e)
    {
        if (e.Source is not Visual source)
        {
            return null;
        }

        DataGridRow? row = source.GetVisualAncestors().OfType<DataGridRow>().FirstOrDefault();
        if (row?.DataContext is HierarchicalNode node)
        {
            return node.Item as SolutionExplorerNodeViewModel;
        }

        return null;
    }

    private sealed class DragState
    {
        public Point Start { get; init; }
        public string FilePath { get; init; } = string.Empty;
    }
}
