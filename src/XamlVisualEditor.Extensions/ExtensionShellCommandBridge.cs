namespace XamlVisualEditor.Extensions;

/// <summary>Represents non-lifecycle shell commands exposed to extensions.</summary>
public enum ShellCommandKind
{
    Undo,
    Redo,
    Cut,
    Copy,
    Paste,
    Delete,
    SelectAll,
    RenameSymbol,
    FormatDocument,
    ShowCodeActions,
    ShowDocumentSymbols,
    ShowWorkspaceSymbols,
    ToggleBreakpoints,
    ToggleCallStack,
    ToggleLocals,
    ToggleWatches,
    StartDebug,
    StopDebug,
    ContinueDebug,
    StepOver,
    StepIn,
    StepOut,
    PauseDebug,
    ToggleBreakpoint,
    StartRun,
    StopRun,
    NewTerminal
}

/// <summary>Provides extension-safe access to non-lifecycle shell commands.</summary>
public interface IShellCommandBridge
{
    /// <summary>Executes the requested shell command when currently available.</summary>
    Task ExecuteAsync(ShellCommandKind command, CancellationToken cancellationToken);
}
