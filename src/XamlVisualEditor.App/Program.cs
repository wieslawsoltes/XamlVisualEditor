using System;
using System.Diagnostics;
using Avalonia;
using ReactiveUI.Avalonia;
using XamlVisualEditor.Terminal;

namespace XamlVisualEditor.App;

/// <summary>
/// Application entry point.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string? capturePath = TerminalCaptureArgs.ResolveCapturePath(args, Environment.CurrentDirectory);
        if (!string.IsNullOrWhiteSpace(capturePath))
        {
            Environment.SetEnvironmentVariable("XVE_TERMINAL_LOG", capturePath);
        }

        Trace.AutoFlush = true;
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
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
