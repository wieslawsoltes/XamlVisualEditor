using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

public sealed class CanvasItemDragBehavior
{
    private static readonly double GridSize = 20;
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<CanvasItemDragBehavior, Control, bool>("IsEnabled");

    private static readonly AttachedProperty<DragState?> DragStateProperty =
        AvaloniaProperty.RegisterAttached<CanvasItemDragBehavior, Control, DragState?>("DragState");

    static CanvasItemDragBehavior()
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
        if (sender is not Control control || control.DataContext is not CanvasEditorItemViewModel item)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(control);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        Canvas? canvas = control.GetVisualAncestors().OfType<Canvas>().FirstOrDefault();
        if (canvas is null)
        {
            return;
        }

        DragState state = new()
        {
            Start = e.GetPosition(canvas),
            StartX = item.X,
            StartY = item.Y,
            Canvas = canvas
        };

        control.SetValue(DragStateProperty, state);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not CanvasEditorItemViewModel item)
        {
            return;
        }

        DragState? state = control.GetValue(DragStateProperty);
        if (state is null || state.Canvas is null)
        {
            return;
        }

        if (e.Pointer.Captured != control)
        {
            return;
        }

        var current = e.GetPosition(state.Canvas);
        double deltaX = current.X - state.Start.X;
        double deltaY = current.Y - state.Start.Y;

        double newX = Math.Max(0, state.StartX + deltaX);
        double newY = Math.Max(0, state.StartY + deltaY);
        item.X = Snap(newX);
        item.Y = Snap(newY);
        e.Handled = true;
    }

    private static double Snap(double value)
    {
        if (GridSize <= 1)
        {
            return value;
        }

        return Math.Round(value / GridSize) * GridSize;
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

    private sealed class DragState
    {
        public Point Start { get; init; }
        public double StartX { get; init; }
        public double StartY { get; init; }
        public Canvas? Canvas { get; init; }
    }
}
