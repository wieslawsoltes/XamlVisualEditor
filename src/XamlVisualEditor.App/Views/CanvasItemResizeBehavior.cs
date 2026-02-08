using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

public enum CanvasResizeHandle
{
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left
}

public sealed class CanvasItemResizeBehavior
{
    private const double MinWidth = 240;
    private const double MinHeight = 160;
    private static readonly double GridSize = 20;

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<CanvasItemResizeBehavior, Control, bool>("IsEnabled");

    public static readonly AttachedProperty<CanvasResizeHandle> HandleProperty =
        AvaloniaProperty.RegisterAttached<CanvasItemResizeBehavior, Control, CanvasResizeHandle>("Handle");

    private static readonly AttachedProperty<ResizeState?> ResizeStateProperty =
        AvaloniaProperty.RegisterAttached<CanvasItemResizeBehavior, Control, ResizeState?>("ResizeState");

    static CanvasItemResizeBehavior()
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

    public static CanvasResizeHandle GetHandle(Control control)
    {
        return control.GetValue(HandleProperty);
    }

    public static void SetHandle(Control control, CanvasResizeHandle value)
    {
        control.SetValue(HandleProperty, value);
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

        ResizeState state = new()
        {
            Canvas = canvas,
            Start = e.GetPosition(canvas),
            StartX = item.X,
            StartY = item.Y,
            StartWidth = item.Width,
            StartHeight = item.Height,
            Handle = GetHandle(control)
        };

        control.SetValue(ResizeStateProperty, state);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not CanvasEditorItemViewModel item)
        {
            return;
        }

        ResizeState? state = control.GetValue(ResizeStateProperty);
        if (state is null || state.Canvas is null)
        {
            return;
        }

        if (e.Pointer.Captured != control)
        {
            return;
        }

        var current = e.GetPosition(state.Canvas);
        double dx = current.X - state.Start.X;
        double dy = current.Y - state.Start.Y;

        ApplyResize(state, item, dx, dy);
        e.Handled = true;
    }

    private static void ApplyResize(ResizeState state, CanvasEditorItemViewModel item, double dx, double dy)
    {
        double newX = state.StartX;
        double newY = state.StartY;
        double newWidth = state.StartWidth;
        double newHeight = state.StartHeight;
        bool resizeLeft = false;
        bool resizeTop = false;

        switch (state.Handle)
        {
            case CanvasResizeHandle.Left:
                resizeLeft = true;
                newWidth = Math.Max(MinWidth, state.StartWidth - dx);
                break;
            case CanvasResizeHandle.Right:
                newWidth = Math.Max(MinWidth, state.StartWidth + dx);
                break;
            case CanvasResizeHandle.Top:
                resizeTop = true;
                newHeight = Math.Max(MinHeight, state.StartHeight - dy);
                break;
            case CanvasResizeHandle.Bottom:
                newHeight = Math.Max(MinHeight, state.StartHeight + dy);
                break;
            case CanvasResizeHandle.TopLeft:
                resizeLeft = true;
                resizeTop = true;
                newWidth = Math.Max(MinWidth, state.StartWidth - dx);
                newHeight = Math.Max(MinHeight, state.StartHeight - dy);
                break;
            case CanvasResizeHandle.TopRight:
                resizeTop = true;
                newWidth = Math.Max(MinWidth, state.StartWidth + dx);
                newHeight = Math.Max(MinHeight, state.StartHeight - dy);
                break;
            case CanvasResizeHandle.BottomLeft:
                resizeLeft = true;
                newWidth = Math.Max(MinWidth, state.StartWidth - dx);
                newHeight = Math.Max(MinHeight, state.StartHeight + dy);
                break;
            case CanvasResizeHandle.BottomRight:
                newWidth = Math.Max(MinWidth, state.StartWidth + dx);
                newHeight = Math.Max(MinHeight, state.StartHeight + dy);
                break;
        }

        newWidth = Snap(newWidth);
        newHeight = Snap(newHeight);

        if (resizeLeft)
        {
            newX = state.StartX + (state.StartWidth - newWidth);
        }

        if (resizeTop)
        {
            newY = state.StartY + (state.StartHeight - newHeight);
        }

        if (newX < 0)
        {
            newWidth = Math.Max(MinWidth, newWidth + newX);
            newX = 0;
        }

        if (newY < 0)
        {
            newHeight = Math.Max(MinHeight, newHeight + newY);
            newY = 0;
        }

        item.X = newX;
        item.Y = newY;
        item.Width = newWidth;
        item.Height = newHeight;
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

        control.ClearValue(ResizeStateProperty);
    }

    private sealed class ResizeState
    {
        public Canvas? Canvas { get; init; }
        public Point Start { get; init; }
        public double StartX { get; init; }
        public double StartY { get; init; }
        public double StartWidth { get; init; }
        public double StartHeight { get; init; }
        public CanvasResizeHandle Handle { get; init; }
    }
}
