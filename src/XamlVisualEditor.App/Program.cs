using System;
using Avalonia;
using ReactiveUI.Avalonia;

namespace XamlVisualEditor.App;

/// <summary>
/// Application entry point.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }
}
