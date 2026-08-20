using System;
using System.IO;
using Serilog;
using Serilog.Core;

namespace XamlVisualEditor.App.Services;

/// <summary>
/// Central logging bootstrap: builds the shared Serilog logger (console, rolling
/// file, optional output panel) and hooks process-wide crash logging.
/// </summary>
public static class FileLoggingSetup
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Gets the directory that receives rolling log files.
    /// </summary>
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XamlVisualEditor",
        "logs");

    /// <summary>
    /// Creates the application logger. Pass the panel sink when the UI is available;
    /// early bootstrap (before DI) may pass <c>null</c>.
    /// </summary>
    public static Logger CreateLogger(ILogEventSink? panelSink)
    {
        Directory.CreateDirectory(LogDirectory);

        LoggerConfiguration configuration = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .WriteTo.File(
                Path.Combine(LogDirectory, "xve-.log"),
                outputTemplate: OutputTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true);

        if (panelSink is not null)
        {
            configuration = configuration.WriteTo.Sink(panelSink);
        }

        return configuration.CreateLogger();
    }

    /// <summary>
    /// Replaces the global logger, flushing and disposing the previous one.
    /// </summary>
    public static void ReplaceGlobalLogger(Logger logger)
    {
        Log.CloseAndFlush();
        Log.Logger = logger;
    }

    /// <summary>
    /// Hooks process-wide handlers so unhandled exceptions reach the log file
    /// before the process dies.
    /// </summary>
    public static void HookUnhandledExceptionLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception,
                "Unhandled exception (terminating: {IsTerminating})", e.IsTerminating);
            Log.CloseAndFlush();
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception");
        };
    }
}
