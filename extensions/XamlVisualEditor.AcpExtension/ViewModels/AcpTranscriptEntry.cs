using System;

namespace XamlVisualEditor.AcpExtension;

public abstract record AcpTranscriptEntry(string Kind, string Timestamp);

public sealed record AcpTranscriptMessageEntry(string Role, string Text, string Timestamp)
    : AcpTranscriptEntry("message", Timestamp)
{
    public string RoleLabel => string.IsNullOrWhiteSpace(Role) ? "Message" : Role;
}

public sealed record AcpTranscriptToolEntry(
    string ToolCallId,
    string Title,
    string Status,
    string ToolKind,
    string Timestamp,
    string? DiffPath,
    string? DiffOldText,
    string? DiffNewText,
    string? TerminalId,
    string? TerminalOutput,
    bool TerminalTruncated,
    int? TerminalExitCode,
    string? TerminalSignal)
    : AcpTranscriptEntry("tool", Timestamp)
{
    public string TitleDisplay => string.IsNullOrWhiteSpace(Title) ? "Tool call" : Title;

    public string StatusDisplay => string.IsNullOrWhiteSpace(Status) ? "status: unknown" : "status: " + Status;

    public string ToolKindDisplay => string.IsNullOrWhiteSpace(ToolKind) ? "kind: other" : "kind: " + ToolKind;

    public string ToolCallIdDisplay => string.IsNullOrWhiteSpace(ToolCallId) ? "" : "id: " + ToolCallId;

    public bool HasDiff => !string.IsNullOrWhiteSpace(DiffPath);

    public bool HasTerminal => !string.IsNullOrWhiteSpace(TerminalId);

    public string DiffPathDisplay => string.IsNullOrWhiteSpace(DiffPath) ? "" : DiffPath;

    public string DiffOldDisplay => string.IsNullOrWhiteSpace(DiffOldText) ? "" : DiffOldText;

    public string DiffNewDisplay => string.IsNullOrWhiteSpace(DiffNewText) ? "" : DiffNewText;

    public string TerminalDisplay => string.IsNullOrWhiteSpace(TerminalId) ? "" : "terminal: " + TerminalId;

    public bool HasTerminalOutput => !string.IsNullOrWhiteSpace(TerminalOutput);

    public string TerminalOutputDisplay => TerminalOutput ?? string.Empty;

    public string TerminalStatusDisplay
        => TerminalExitCode is null && string.IsNullOrWhiteSpace(TerminalSignal)
            ? string.Empty
            : "exit: " + (TerminalExitCode?.ToString() ?? "?") + (string.IsNullOrWhiteSpace(TerminalSignal) ? "" : " signal: " + TerminalSignal);

    public string TerminalTruncatedDisplay => TerminalTruncated ? "output truncated" : string.Empty;
}

public sealed record AcpTranscriptStatusEntry(string Title, string Detail, string Timestamp)
    : AcpTranscriptEntry("status", Timestamp)
{
    public string TitleDisplay => string.IsNullOrWhiteSpace(Title) ? "Update" : Title;

    public string DetailDisplay => string.IsNullOrWhiteSpace(Detail) ? "" : Detail;
}
