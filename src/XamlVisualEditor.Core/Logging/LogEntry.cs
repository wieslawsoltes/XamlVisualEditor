using System;
using Microsoft.Extensions.Logging;

namespace XamlVisualEditor.Core.Logging;

public sealed record LogEntry(
    LogLevel Level,
    string Message,
    Exception? Exception = null,
    string? FilePath = null,
    int Line = 0,
    int Column = 0);

public interface IOutputLogSink
{
    void Write(LogEntry entry);
}

public interface IOutputLogSinkAccessor
{
    IOutputLogSink? Sink { get; set; }
}
