using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

public sealed class TimelineKeyframeDragBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TimelineKeyframeDragBehavior, Control, bool>("IsEnabled");

    public static readonly AttachedProperty<double> PixelsPerSecondProperty =
        AvaloniaProperty.RegisterAttached<TimelineKeyframeDragBehavior, Control, double>("PixelsPerSecond", 1.0);

    public static readonly AttachedProperty<double> DurationSecondsProperty =
        AvaloniaProperty.RegisterAttached<TimelineKeyframeDragBehavior, Control, double>("DurationSeconds", 1.0);

    public static readonly AttachedProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.RegisterAttached<TimelineKeyframeDragBehavior, Control, ICommand?>("SelectCommand");

    public static readonly AttachedProperty<ICommand?> CommitMoveCommandProperty =
        AvaloniaProperty.RegisterAttached<TimelineKeyframeDragBehavior, Control, ICommand?>("CommitMoveCommand");

    public static readonly AttachedProperty<double> SnapIntervalSecondsProperty =
        AvaloniaProperty.RegisterAttached<TimelineKeyframeDragBehavior, Control, double>("SnapIntervalSeconds", 0.0);

    private static readonly AttachedProperty<DragState?> DragStateProperty =
        AvaloniaProperty.RegisterAttached<TimelineKeyframeDragBehavior, Control, DragState?>("DragState");

    static TimelineKeyframeDragBehavior()
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

    public static double GetPixelsPerSecond(Control control)
    {
        return control.GetValue(PixelsPerSecondProperty);
    }

    public static void SetPixelsPerSecond(Control control, double value)
    {
        control.SetValue(PixelsPerSecondProperty, value);
    }

    public static double GetDurationSeconds(Control control)
    {
        return control.GetValue(DurationSecondsProperty);
    }

    public static void SetDurationSeconds(Control control, double value)
    {
        control.SetValue(DurationSecondsProperty, value);
    }

    public static ICommand? GetSelectCommand(Control control)
    {
        return control.GetValue(SelectCommandProperty);
    }

    public static void SetSelectCommand(Control control, ICommand? value)
    {
        control.SetValue(SelectCommandProperty, value);
    }

    public static ICommand? GetCommitMoveCommand(Control control)
    {
        return control.GetValue(CommitMoveCommandProperty);
    }

    public static void SetCommitMoveCommand(Control control, ICommand? value)
    {
        control.SetValue(CommitMoveCommandProperty, value);
    }

    public static double GetSnapIntervalSeconds(Control control)
    {
        return control.GetValue(SnapIntervalSecondsProperty);
    }

    public static void SetSnapIntervalSeconds(Control control, double value)
    {
        control.SetValue(SnapIntervalSecondsProperty, value);
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
        if (sender is not Control control || control.DataContext is not AnimationKeyframeViewModel keyframe)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(control);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        ICommand? selectCommand = GetSelectCommand(control);
        if (selectCommand is not null)
        {
            KeyframeSelectionMode mode = KeyframeSelectionMode.Replace;
            bool additive = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                mode = KeyframeSelectionMode.Range;
            }
            else if (additive)
            {
                mode = KeyframeSelectionMode.Add;
            }

            KeyframeSelectionRequest request = new(keyframe, mode, additive);
            if (selectCommand.CanExecute(request))
            {
                selectCommand.Execute(request);
            }
        }

        double pixelsPerSecond = GetPixelsPerSecond(control);
        double durationSeconds = GetDurationSeconds(control);

        DragState state = new()
        {
            Start = e.GetPosition(control),
            StartTimeSeconds = keyframe.TimeSeconds,
            PixelsPerSecond = Math.Max(1.0, pixelsPerSecond),
            DurationSeconds = Math.Max(0.0, durationSeconds)
        };

        control.SetValue(DragStateProperty, state);
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AnimationKeyframeViewModel keyframe)
        {
            return;
        }

        DragState? state = control.GetValue(DragStateProperty);
        if (state is null)
        {
            return;
        }

        if (e.Pointer.Captured != control)
        {
            return;
        }

        var current = e.GetPosition(control);
        double deltaX = current.X - state.Start.X;
        double deltaSeconds = deltaX / state.PixelsPerSecond;
        double newTime = state.StartTimeSeconds + deltaSeconds;
        double snapped = ApplySnap(control, newTime);
        keyframe.TimeSeconds = Math.Clamp(snapped, 0.0, state.DurationSeconds);
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

        DragState? state = control.GetValue(DragStateProperty);
        if (state is not null && control.DataContext is AnimationKeyframeViewModel keyframe)
        {
            ICommand? commitCommand = GetCommitMoveCommand(control);
            if (commitCommand is not null)
            {
                var payload = new KeyframeMoveCommit(keyframe, state.StartTimeSeconds, keyframe.TimeSeconds);
                if (commitCommand.CanExecute(payload))
                {
                    commitCommand.Execute(payload);
                }
            }
        }

        control.ClearValue(DragStateProperty);
    }

    private sealed class DragState
    {
        public Point Start { get; init; }
        public double StartTimeSeconds { get; init; }
        public double PixelsPerSecond { get; init; }
        public double DurationSeconds { get; init; }
    }

    private static double ApplySnap(Control control, double timeSeconds)
    {
        double interval = GetSnapIntervalSeconds(control);
        if (interval <= 0.0)
        {
            return timeSeconds;
        }

        return Math.Round(timeSeconds / interval) * interval;
    }
}
