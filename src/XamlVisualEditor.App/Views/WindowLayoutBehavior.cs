using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using XamlVisualEditor.App.Services;

namespace XamlVisualEditor.App.Views;

/// <summary>Persists and restores the size, position and maximized state of an Avalonia window.</summary>
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

        if (state.WindowX is int x && state.WindowY is int y)
        {
            PixelPoint position = new(x, y);
            if (IsOnAnyScreen(window, position))
            {
                window.Position = position;
            }
        }

        if (state.WindowMaximized)
        {
            // Maximize only after the restored normal bounds reached the platform window,
            // so leaving the maximized state returns to them instead of the XAML defaults.
            Dispatcher.UIThread.Post(
                () => window.WindowState = WindowState.Maximized,
                DispatcherPriority.Background);
        }
    }

    private static void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (sender is not Window window || window.WindowState == WindowState.Minimized)
        {
            return;
        }

        if (window.WindowState != WindowState.Normal)
        {
            UiLayoutStateStore.Update(state => state.WindowMaximized = true);
            return;
        }

        double width = window.ClientSize.Width;
        double height = window.ClientSize.Height;
        PixelPoint position = window.Position;
        UiLayoutStateStore.Update(state =>
        {
            state.WindowWidth = width;
            state.WindowHeight = height;
            state.WindowX = position.X;
            state.WindowY = position.Y;
            state.WindowMaximized = false;
        });
    }

    private static bool IsOnAnyScreen(Window window, PixelPoint position)
    {
        Screens? screens = window.Screens;
        if (screens is null || screens.ScreenCount == 0)
        {
            return true;
        }

        // Accept a position when its title bar area still touches a screen, so a
        // window near a monitor edge is restored while one on a detached monitor is not.
        return screens.ScreenFromPoint(position) is not null
            || screens.ScreenFromPoint(new PixelPoint(position.X + 100, position.Y + 20)) is not null;
    }
}
