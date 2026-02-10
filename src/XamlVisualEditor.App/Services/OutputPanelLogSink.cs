using System;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using XamlVisualEditor.Core.Logging;

namespace XamlVisualEditor.App.Services;

public sealed class OutputPanelLogSink : ILogEventSink
{
    private readonly IOutputLogSinkAccessor _accessor;

    public OutputPanelLogSink(IOutputLogSinkAccessor accessor)
    {
        _accessor = accessor;
    }

    public void Emit(LogEvent logEvent)
    {
        IOutputLogSink? sink = _accessor.Sink;
        if (sink is null)
        {
            return;
        }

        string message = logEvent.RenderMessage();
        if (logEvent.Exception is not null)
        {
            message = message + Environment.NewLine + logEvent.Exception;
        }

        sink.Write(new LogEntry(
            MapLevel(logEvent.Level),
            message,
            logEvent.Exception));
    }

    private static LogLevel MapLevel(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }
}
