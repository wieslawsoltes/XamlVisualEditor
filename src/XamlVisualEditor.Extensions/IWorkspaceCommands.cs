namespace XamlVisualEditor.Extensions;

/// <summary>Executes workspace-level commands.</summary>
public interface IWorkspaceCommands
{
    /// <summary>Gets whether a workspace is currently loaded.</summary>
    bool HasWorkspace { get; }

    /// <summary>Loads or reloads the active workspace.</summary>
    Task LoadWorkspaceAsync(CancellationToken cancellationToken);

    /// <summary>Restores workspace dependencies.</summary>
    Task RestoreWorkspaceAsync(CancellationToken cancellationToken);

    /// <summary>Builds the workspace.</summary>
    Task BuildWorkspaceAsync(CancellationToken cancellationToken);

    /// <summary>Rebuilds the workspace.</summary>
    Task RebuildWorkspaceAsync(CancellationToken cancellationToken);

    /// <summary>Cleans the workspace outputs.</summary>
    Task CleanWorkspaceAsync(CancellationToken cancellationToken);

    /// <summary>Sets the startup project for run/debug operations.</summary>
    Task SetStartupProjectAsync(string projectPath, string? targetFramework, CancellationToken cancellationToken);

    /// <summary>Performs an undo action for the active designer document.</summary>
    Task UndoAsync(CancellationToken cancellationToken);

    /// <summary>Performs a redo action for the active designer document.</summary>
    Task RedoAsync(CancellationToken cancellationToken);

    /// <summary>Cuts the current designer selection.</summary>
    Task CutAsync(CancellationToken cancellationToken);

    /// <summary>Copies the current designer selection.</summary>
    Task CopyAsync(CancellationToken cancellationToken);

    /// <summary>Pastes clipboard content into the active designer document.</summary>
    Task PasteAsync(CancellationToken cancellationToken);

    /// <summary>Deletes the current designer selection.</summary>
    Task DeleteAsync(CancellationToken cancellationToken);

    /// <summary>Selects all content in the active designer document.</summary>
    Task SelectAllAsync(CancellationToken cancellationToken);

    /// <summary>Starts rename symbol flow in the active text document.</summary>
    Task RenameSymbolAsync(CancellationToken cancellationToken);

    /// <summary>Formats the active text document.</summary>
    Task FormatDocumentAsync(CancellationToken cancellationToken);

    /// <summary>Shows available code actions for the active text document.</summary>
    Task ShowCodeActionsAsync(CancellationToken cancellationToken);

    /// <summary>Shows document symbols for the active text document.</summary>
    Task ShowDocumentSymbolsAsync(CancellationToken cancellationToken);

    /// <summary>Shows workspace symbols for the active text document context.</summary>
    Task ShowWorkspaceSymbolsAsync(CancellationToken cancellationToken);

    /// <summary>Toggles the breakpoints panel visibility.</summary>
    Task ToggleBreakpointsAsync(CancellationToken cancellationToken);

    /// <summary>Toggles the call stack panel visibility.</summary>
    Task ToggleCallStackAsync(CancellationToken cancellationToken);

    /// <summary>Toggles the locals panel visibility.</summary>
    Task ToggleLocalsAsync(CancellationToken cancellationToken);

    /// <summary>Toggles the watches panel visibility.</summary>
    Task ToggleWatchesAsync(CancellationToken cancellationToken);

    /// <summary>Starts a debug session.</summary>
    Task StartDebugAsync(CancellationToken cancellationToken);

    /// <summary>Stops the current debug session.</summary>
    Task StopDebugAsync(CancellationToken cancellationToken);

    /// <summary>Continues execution in the current debug session.</summary>
    Task ContinueDebugAsync(CancellationToken cancellationToken);

    /// <summary>Executes debugger step over.</summary>
    Task StepOverAsync(CancellationToken cancellationToken);

    /// <summary>Executes debugger step in.</summary>
    Task StepInAsync(CancellationToken cancellationToken);

    /// <summary>Executes debugger step out.</summary>
    Task StepOutAsync(CancellationToken cancellationToken);

    /// <summary>Pauses the current debug session.</summary>
    Task PauseDebugAsync(CancellationToken cancellationToken);

    /// <summary>Toggles a breakpoint at the active caret position.</summary>
    Task ToggleBreakpointAsync(CancellationToken cancellationToken);

    /// <summary>Starts run without debugger.</summary>
    Task StartRunAsync(CancellationToken cancellationToken);

    /// <summary>Stops run without debugger.</summary>
    Task StopRunAsync(CancellationToken cancellationToken);

    /// <summary>Creates a new terminal session.</summary>
    Task NewTerminalAsync(CancellationToken cancellationToken);
}
