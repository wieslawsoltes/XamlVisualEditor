using Avalonia;
using Avalonia.Controls;
using XamlVisualEditor.App.Services;

namespace XamlVisualEditor.App.Views;

/// <summary>Persists and restores the size of an Avalonia window.</summary>
public sealed class WindowLayoutBehavior
{
    /// <summary>Identifies the attached property that enables window layout persistence.</summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<WindowLayoutBehavior, Window, bool>("IsEnabled");

    private static readonly AttachedProperty<bool> HandlersAttachedProperty =
        AvaloniaProperty.RegisterAttached<WindowLayoutBehavior, Window, bool>("HandlersAttached");

    static WindowLayoutBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Window>(OnIsEnabledChanged);
    }

    /// <summary>Gets whether window layout persistence is enabled.</summary>
    public static bool GetIsEnabled(Window window)
    {
        return window.GetValue(IsEnabledProperty);
    }

    /// <summary>Sets whether window layout persistence is enabled.</summary>
    public static void SetIsEnabled(Window window, bool value)
    {
        window.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(Window window, AvaloniaPropertyChangedEventArgs e)
    {
        bool enabled = e.NewValue is true;
        bool attached = window.GetValue(HandlersAttachedProperty);

        if (enabled && !attached)
        {
            window.Opened += OnOpened;
            window.Closing += OnClosing;
            window.SetValue(HandlersAttachedProperty, true);
        }
        else if (!enabled && attached)
        {
            window.Opened -= OnOpened;
            window.Closing -= OnClosing;
            window.SetValue(HandlersAttachedProperty, false);
        }
    }

    private static void OnOpened(object? sender, System.EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        UiLayoutState state = UiLayoutStateStore.Load();
        window.Width = state.WindowWidth;
        window.Height = state.WindowHeight;
    }

    private static void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (sender is not Window window || window.WindowState != WindowState.Normal)
        {
            return;
        }

        double width = window.ClientSize.Width;
        double height = window.ClientSize.Height;
        UiLayoutStateStore.Update(state =>
        {
            state.WindowWidth = width;
            state.WindowHeight = height;
        });
    }
}
