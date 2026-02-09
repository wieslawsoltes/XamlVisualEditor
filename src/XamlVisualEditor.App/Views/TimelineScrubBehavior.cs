using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace XamlVisualEditor.App.Views;

public sealed class TimelineScrubBehavior
{
    public static readonly AttachedProperty<ICommand?> ScrubCommandProperty =
        AvaloniaProperty.RegisterAttached<TimelineScrubBehavior, Control, ICommand?>("ScrubCommand");

    public static readonly AttachedProperty<double> PixelsPerSecondProperty =
        AvaloniaProperty.RegisterAttached<TimelineScrubBehavior, Control, double>("PixelsPerSecond", 1.0);

    private static readonly AttachedProperty<bool> IsScrubbingProperty =
        AvaloniaProperty.RegisterAttached<TimelineScrubBehavior, Control, bool>("IsScrubbing");

    static TimelineScrubBehavior()
    {
        ScrubCommandProperty.Changed.AddClassHandler<Control>(OnScrubCommandChanged);
    }

    public static ICommand? GetScrubCommand(Control control)
    {
        return control.GetValue(ScrubCommandProperty);
    }

    public static void SetScrubCommand(Control control, ICommand? value)
    {
        control.SetValue(ScrubCommandProperty, value);
    }

    public static double GetPixelsPerSecond(Control control)
    {
        return control.GetValue(PixelsPerSecondProperty);
    }

    public static void SetPixelsPerSecond(Control control, double value)
    {
        control.SetValue(PixelsPerSecondProperty, value);
    }

    private static void OnScrubCommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is ICommand && e.OldValue is null)
        {
            control.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            control.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            control.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        }
        else if (e.NewValue is null && e.OldValue is ICommand)
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

        control.SetValue(IsScrubbingProperty, true);
        e.Pointer.Capture(control);
        ScrubToPosition(control, e.GetPosition(control).X);
        e.Handled = true;
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        if (!control.GetValue(IsScrubbingProperty) || e.Pointer.Captured != control)
        {
            return;
        }

        ScrubToPosition(control, e.GetPosition(control).X);
        e.Handled = true;
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        control.SetValue(IsScrubbingProperty, false);
        if (e.Pointer.Captured == control)
        {
            e.Pointer.Capture(null);
        }
    }

    private static void ScrubToPosition(Control control, double x)
    {
        ICommand? command = GetScrubCommand(control);
        if (command is null)
        {
            return;
        }

        double pixelsPerSecond = GetPixelsPerSecond(control);
        double time = pixelsPerSecond <= 0 ? 0 : x / pixelsPerSecond;
        if (command.CanExecute(time))
        {
            command.Execute(time);
        }
    }
}
