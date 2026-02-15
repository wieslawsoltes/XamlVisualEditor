namespace XamlVisualEditor.Extensions;

/// <summary>Provides terminal access for external tooling.</summary>
public interface ITerminalBridge
{
    /// <summary>Raised when a terminal is created.</summary>
    event EventHandler<TerminalChangedEventArgs>? TerminalCreated;

    /// <summary>Raised when a terminal is closed.</summary>
    event EventHandler<TerminalChangedEventArgs>? TerminalClosed;

    /// <summary>Raised when the active terminal changes.</summary>
    event EventHandler<ActiveTerminalChangedEventArgs>? ActiveTerminalChanged;

    /// <summary>Raised when a terminal emits output.</summary>
    event EventHandler<TerminalOutputEventArgs>? TerminalOutput;

    /// <summary>Raised when a terminal exits.</summary>
    event EventHandler<TerminalExitEventArgs>? TerminalExited;

    /// <summary>Raised when terminal dimensions change.</summary>
    event EventHandler<TerminalDimensionsChangedEventArgs>? TerminalDimensionsChanged;

    /// <summary>Creates a terminal session.</summary>
    Task<TerminalInfo> CreateAsync(TerminalCreateRequest request, CancellationToken ct);

    /// <summary>Sends text to a terminal session.</summary>
    Task SendTextAsync(Guid terminalId, string text, CancellationToken ct);

    /// <summary>Gets current terminal sessions.</summary>
    Task<IReadOnlyList<TerminalInfo>> GetTerminalsAsync(CancellationToken ct);

    /// <summary>Gets the active terminal id.</summary>
    Task<Guid?> GetActiveTerminalIdAsync(CancellationToken ct);

    /// <summary>Closes a terminal session.</summary>
    Task<bool> CloseAsync(Guid terminalId, CancellationToken ct);

    /// <summary>Runs a task and applies problem matchers to task output.</summary>
    Task<TaskExecutionResult> RunTaskAsync(TaskExecutionRequest request, CancellationToken ct);
}

/// <summary>Terminal creation request.</summary>
public sealed record TerminalCreateRequest(
    string? Title,
    string? WorkingDirectory,
    string? ShellPath,
    IReadOnlyList<string>? Arguments);

/// <summary>Terminal descriptor.</summary>
public sealed record TerminalInfo(Guid Id, string Title, int Columns = 0, int Rows = 0);

/// <summary>Terminal event args with terminal descriptor.</summary>
public sealed class TerminalChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public TerminalChangedEventArgs(TerminalInfo terminal)
    {
        Terminal = terminal;
    }

    /// <summary>Gets terminal descriptor.</summary>
    public TerminalInfo Terminal { get; }
}

/// <summary>Active terminal change payload.</summary>
public sealed class ActiveTerminalChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public ActiveTerminalChangedEventArgs(Guid? terminalId)
    {
        TerminalId = terminalId;
    }

    /// <summary>Gets active terminal id.</summary>
    public Guid? TerminalId { get; }
}

/// <summary>Terminal output payload.</summary>
public sealed class TerminalOutputEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public TerminalOutputEventArgs(Guid terminalId, string text)
    {
        TerminalId = terminalId;
        Text = text;
    }

    /// <summary>Gets terminal id.</summary>
    public Guid TerminalId { get; }

    /// <summary>Gets output text.</summary>
    public string Text { get; }
}

/// <summary>Terminal exit payload.</summary>
public sealed class TerminalExitEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public TerminalExitEventArgs(Guid terminalId, int? exitCode)
    {
        TerminalId = terminalId;
        ExitCode = exitCode;
    }

    /// <summary>Gets terminal id.</summary>
    public Guid TerminalId { get; }

    /// <summary>Gets process exit code when available.</summary>
    public int? ExitCode { get; }
}

/// <summary>Terminal dimensions payload.</summary>
public sealed class TerminalDimensionsChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public TerminalDimensionsChangedEventArgs(Guid terminalId, int columns, int rows)
    {
        TerminalId = terminalId;
        Columns = columns;
        Rows = rows;
    }

    /// <summary>Gets terminal id.</summary>
    public Guid TerminalId { get; }

    /// <summary>Gets terminal columns.</summary>
    public int Columns { get; }

    /// <summary>Gets terminal rows.</summary>
    public int Rows { get; }
}

/// <summary>Task run request.</summary>
public sealed record TaskExecutionRequest(
    string TaskId,
    string Command,
    IReadOnlyList<string>? Arguments,
    string? WorkingDirectory,
    IReadOnlyList<TaskProblemMatcher>? ProblemMatchers);

/// <summary>Problem matcher configuration for task output parsing.</summary>
public sealed record TaskProblemMatcher(
    string Pattern,
    TaskProblemSeverity Severity = TaskProblemSeverity.Error,
    int FileGroup = 1,
    int LineGroup = 2,
    int ColumnGroup = 3,
    int MessageGroup = 4);

/// <summary>Problem severity emitted by task matchers.</summary>
public enum TaskProblemSeverity
{
    /// <summary>Error severity.</summary>
    Error,

    /// <summary>Warning severity.</summary>
    Warning,

    /// <summary>Information severity.</summary>
    Information
}

/// <summary>Matched problem item produced by a task run.</summary>
public sealed record TaskProblemMatch(
    TaskProblemSeverity Severity,
    string? FilePath,
    int? Line,
    int? Column,
    string Message);

/// <summary>Task execution result payload.</summary>
public sealed record TaskExecutionResult(
    string TaskId,
    int ExitCode,
    IReadOnlyList<string> Output,
    IReadOnlyList<TaskProblemMatch> Problems);
