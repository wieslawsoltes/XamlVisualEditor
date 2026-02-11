namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Captures crash diagnostics for an extension.</summary>
public sealed record ExtensionCrashInfo(
    string ExtensionId,
    int ExitCode,
    DateTimeOffset Timestamp,
    string? ErrorOutputTail);
