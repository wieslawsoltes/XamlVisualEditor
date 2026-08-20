using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Avalonia;
using ReactiveUI.Avalonia;
using Serilog;
using XamlVisualEditor.App.Services;
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
        WireDependencyInjectionResolver();

        string? capturePath = TerminalCaptureArgs.ResolveCapturePath(args, Environment.CurrentDirectory);
        if (!string.IsNullOrWhiteSpace(capturePath))
        {
            Environment.SetEnvironmentVariable("XVE_TERMINAL_LOG", capturePath);
        }

        Log.Logger = FileLoggingSetup.CreateLogger(panelSink: null);
        FileLoggingSetup.HookUnhandledExceptionLogging();
        Log.Information("XamlVisualEditor starting; log directory: {LogDirectory}", FileLoggingSetup.LogDirectory);

        Trace.AutoFlush = true;
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application crashed");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void WireDependencyInjectionResolver()
    {
        // Ensure DI assemblies resolve from the app base directory at runtime.
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string? assemblyName = name.Name;
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return null;
            }

            if (!assemblyName.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal))
            {
                return null;
            }

            string candidate = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
            if (!File.Exists(candidate))
            {
                return null;
            }

            return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
        };
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(_ => { });
    }
}
