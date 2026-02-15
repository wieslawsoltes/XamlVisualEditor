# Shell Command Audit

Date: 2026-02-15

Scope: `MainWindowViewModel` command surface in `src/XamlVisualEditor.Shell.ViewModels/ShellViewModels.cs`.

## Summary

- Shell currently contains both host-lifecycle commands and non-core feature commands.
- Extension command infrastructure is active and already owns most migrated feature domains.
- Remaining shell cleanup should focus on moving non-core command handlers and leaving host-lifecycle commands only.

## Host-Lifecycle Commands (Keep in Shell)

- `NewDocumentCommand`
- `OpenDocumentCommand`
- `OpenPathCommand`
- `OpenPathsCommand`
- `SaveDocumentCommand`
- `SaveAllCommand`
- `CloseDocumentCommand`
- `ExitCommand`
- `ResetLayoutCommand`
- `OpenCanvasCommand`
- `ToggleExtensionsManagerCommand`
- `ShowCommandPaletteCommand`
- `SetThemeDefaultCommand`
- `SetThemeLightCommand`
- `SetThemeDarkCommand`
- `SetAutoSaveCommand`
- `SetManualSaveCommand`
- `SetNoSaveCommand`
- `AboutCommand`

## Non-Core Commands Still in Shell (Relocation Candidates)

- Editing pipeline: `UndoCommand`, `RedoCommand`, `CutCommand`, `CopyCommand`, `PasteCommand`, `DeleteCommand`, `SelectAllCommand`
- Language/navigation UX: `RenameSymbolCommand`, `FormatDocumentCommand`, `CodeActionsCommand`, `DocumentSymbolsCommand`, `WorkspaceSymbolsCommand`
- Debug/run controls: `StartDebugCommand`, `StopDebugCommand`, `ContinueDebugCommand`, `StepOverCommand`, `StepInCommand`, `StepOutCommand`, `PauseDebugCommand`, `ToggleBreakpointCommand`, `StartRunCommand`, `StopRunCommand`
- Workspace/run configuration: `SetStartupProjectCommand`
- Terminal: `NewTerminalCommand`
- Debug panel toggles: `ToggleBreakpointsCommand`, `ToggleCallStackCommand`, `ToggleLocalsCommand`, `ToggleWatchesCommand`

## Extension-Owned Command Domains (Already Migrated)

- `toolbox.*`
- `propertyEditor.*`
- `treeInspector.*`
- `output.*`
- `navigation.*`
- `animationEditor.*`
- `collaboration.*`
- `debugSettings.*`
- `lspSettings.*`
- `workspace.*`
- `fileExplorer.*`
- `dotnetTemplates.*`

## Next Cleanup Step

Move the non-core shell commands listed above to extension command registrations and keep shell command ownership limited to lifecycle/layout orchestration.
