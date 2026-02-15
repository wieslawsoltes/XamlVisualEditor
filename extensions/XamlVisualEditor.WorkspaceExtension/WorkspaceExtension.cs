using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.WorkspaceExtension;

public sealed class WorkspaceExtension : IXveExtension
{
    private const string LoadCommandId = "workspace.load";
    private const string RestoreCommandId = "workspace.restore";
    private const string BuildCommandId = "workspace.build";
    private const string RebuildCommandId = "workspace.rebuild";
    private const string CleanCommandId = "workspace.clean";
    private const string WorkspaceGroup = "0.workspace";

    private const string UndoCommandId = "editing.undo";
    private const string RedoCommandId = "editing.redo";
    private const string CutCommandId = "editing.cut";
    private const string CopyCommandId = "editing.copy";
    private const string PasteCommandId = "editing.paste";
    private const string DeleteCommandId = "editing.delete";
    private const string SelectAllCommandId = "editing.selectAll";
    private const string RenameSymbolCommandId = "navigation.renameSymbol";
    private const string FormatDocumentCommandId = "navigation.formatDocument";
    private const string CodeActionsCommandId = "navigation.codeActions";
    private const string DocumentSymbolsCommandId = "navigation.documentSymbols";
    private const string WorkspaceSymbolsCommandId = "navigation.workspaceSymbols";

    private const string ToggleBreakpointsCommandId = "debug.breakpoints.toggleView";
    private const string ToggleCallStackCommandId = "debug.callStack.toggleView";
    private const string ToggleLocalsCommandId = "debug.locals.toggleView";
    private const string ToggleWatchesCommandId = "debug.watches.toggleView";
    private const string StartDebugCommandId = "debug.start";
    private const string StopDebugCommandId = "debug.stop";
    private const string ContinueDebugCommandId = "debug.continue";
    private const string StepOverCommandId = "debug.stepOver";
    private const string StepInCommandId = "debug.stepIn";
    private const string StepOutCommandId = "debug.stepOut";
    private const string PauseDebugCommandId = "debug.pause";
    private const string ToggleBreakpointCommandId = "debug.toggleBreakpoint";
    private const string StartRunCommandId = "run.start";
    private const string StopRunCommandId = "run.stop";
    private const string NewTerminalCommandId = "terminal.new";
    private const string NewTerminalIconPath = "M4 5h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2zm2 3 4 3-4 3v-2H4v-2h2V8zm6 6h6v2h-6v-2z";
    private const string UndoIconPath = "M12.5 8c-2.65 0-5.05 1.04-6.83 2.75L3 8v8h8l-2.81-2.81C9.83 11.87 11.1 11 12.5 11c2.34 0 4.33 1.57 4.93 3.7l2.14-.7C18.63 10.73 15.84 8 12.5 8z";
    private const string RedoIconPath = "M18.4 10.6C16.55 9 14.15 8 11.5 8c-3.34 0-6.13 2.73-7.07 6l2.14.7c.6-2.13 2.59-3.7 4.93-3.7 1.4 0 2.67.87 3.31 2.19L12 16h8V8l-1.6 2.6z";
    private const string CutIconPath = "M9.64 7.64c.23-.5.36-1.05.36-1.64 0-2.21-1.79-4-4-4S2 3.79 2 6s1.79 4 4 4c.59 0 1.14-.13 1.64-.36L10 12l-2.36 2.36C7.14 14.13 6.59 14 6 14c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4c0-.59-.13-1.14-.36-1.64L12 14l7 7h3v-1L9.64 7.64zM6 8c-1.1 0-2-.89-2-2s.9-2 2-2 2 .89 2 2-.9 2-2 2zm0 12c-1.1 0-2-.89-2-2s.9-2 2-2 2 .89 2 2-.9 2-2 2zm6-7.5c-.28 0-.5-.22-.5-.5s.22-.5.5-.5.5.22.5.5-.22.5-.5.5zM19 3l-6 6 2 2 7-7V3h-3z";
    private const string CopyIconPath = "M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z";
    private const string PasteIconPath = "M19 2h-4.18C14.4.84 13.3 0 12 0c-1.3 0-2.4.84-2.82 2H5c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-7 0c.55 0 1 .45 1 1s-.45 1-1 1-1-.45-1-1 .45-1 1-1zm7 18H5V4h2v3h10V4h2v16z";
    private const string DeleteIconPath = "M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z";

    private readonly IWorkspaceCommands _workspaceCommands;
    private readonly IWorkspaceInfo _workspaceInfo;
    private readonly IShellCommandBridge _shellCommandBridge;
    private IWindow? _window;

    public WorkspaceExtension(
        IWorkspaceCommands workspaceCommands,
        IWorkspaceInfo workspaceInfo,
        IShellCommandBridge shellCommandBridge)
    {
        _workspaceCommands = workspaceCommands;
        _workspaceInfo = workspaceInfo;
        _shellCommandBridge = shellCommandBridge;
    }

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        _window = context.Window;

        context.Subscriptions.Add(context.Commands.Register(LoadCommandId, _ => LoadWorkspaceAsync()));
        context.Subscriptions.Add(context.Commands.Register(RestoreCommandId, _ =>
            ExecuteIfLoadedAsync(_workspaceCommands.RestoreWorkspaceAsync)));
        context.Subscriptions.Add(context.Commands.Register(BuildCommandId, _ =>
            ExecuteIfLoadedAsync(_workspaceCommands.BuildWorkspaceAsync)));
        context.Subscriptions.Add(context.Commands.Register(RebuildCommandId, _ =>
            ExecuteIfLoadedAsync(_workspaceCommands.RebuildWorkspaceAsync)));
        context.Subscriptions.Add(context.Commands.Register(CleanCommandId, _ =>
            ExecuteIfLoadedAsync(_workspaceCommands.CleanWorkspaceAsync)));

        RegisterShellCommands(context);
        RegisterCommandMetadata(context);
        RegisterMenuContributions(context);
        RegisterToolbarContributions(context);
        RegisterCommandPaletteContributions(context);

        return Task.CompletedTask;
    }

    private void RegisterShellCommands(ExtensionContext context)
    {
        context.Subscriptions.Add(RegisterShellCommand(context, UndoCommandId, ShellCommandKind.Undo));
        context.Subscriptions.Add(RegisterShellCommand(context, RedoCommandId, ShellCommandKind.Redo));
        context.Subscriptions.Add(RegisterShellCommand(context, CutCommandId, ShellCommandKind.Cut));
        context.Subscriptions.Add(RegisterShellCommand(context, CopyCommandId, ShellCommandKind.Copy));
        context.Subscriptions.Add(RegisterShellCommand(context, PasteCommandId, ShellCommandKind.Paste));
        context.Subscriptions.Add(RegisterShellCommand(context, DeleteCommandId, ShellCommandKind.Delete));
        context.Subscriptions.Add(RegisterShellCommand(context, SelectAllCommandId, ShellCommandKind.SelectAll));
        context.Subscriptions.Add(RegisterShellCommand(context, RenameSymbolCommandId, ShellCommandKind.RenameSymbol));
        context.Subscriptions.Add(RegisterShellCommand(context, FormatDocumentCommandId, ShellCommandKind.FormatDocument));
        context.Subscriptions.Add(RegisterShellCommand(context, CodeActionsCommandId, ShellCommandKind.ShowCodeActions));
        context.Subscriptions.Add(RegisterShellCommand(context, DocumentSymbolsCommandId, ShellCommandKind.ShowDocumentSymbols));
        context.Subscriptions.Add(RegisterShellCommand(context, WorkspaceSymbolsCommandId, ShellCommandKind.ShowWorkspaceSymbols));
        context.Subscriptions.Add(RegisterShellCommand(context, ToggleBreakpointsCommandId, ShellCommandKind.ToggleBreakpoints));
        context.Subscriptions.Add(RegisterShellCommand(context, ToggleCallStackCommandId, ShellCommandKind.ToggleCallStack));
        context.Subscriptions.Add(RegisterShellCommand(context, ToggleLocalsCommandId, ShellCommandKind.ToggleLocals));
        context.Subscriptions.Add(RegisterShellCommand(context, ToggleWatchesCommandId, ShellCommandKind.ToggleWatches));
        context.Subscriptions.Add(RegisterShellCommand(context, StartDebugCommandId, ShellCommandKind.StartDebug));
        context.Subscriptions.Add(RegisterShellCommand(context, StopDebugCommandId, ShellCommandKind.StopDebug));
        context.Subscriptions.Add(RegisterShellCommand(context, ContinueDebugCommandId, ShellCommandKind.ContinueDebug));
        context.Subscriptions.Add(RegisterShellCommand(context, StepOverCommandId, ShellCommandKind.StepOver));
        context.Subscriptions.Add(RegisterShellCommand(context, StepInCommandId, ShellCommandKind.StepIn));
        context.Subscriptions.Add(RegisterShellCommand(context, StepOutCommandId, ShellCommandKind.StepOut));
        context.Subscriptions.Add(RegisterShellCommand(context, PauseDebugCommandId, ShellCommandKind.PauseDebug));
        context.Subscriptions.Add(RegisterShellCommand(context, ToggleBreakpointCommandId, ShellCommandKind.ToggleBreakpoint));
        context.Subscriptions.Add(RegisterShellCommand(context, StartRunCommandId, ShellCommandKind.StartRun));
        context.Subscriptions.Add(RegisterShellCommand(context, StopRunCommandId, ShellCommandKind.StopRun));
        context.Subscriptions.Add(RegisterShellCommand(context, NewTerminalCommandId, ShellCommandKind.NewTerminal));
    }

    private IDisposable RegisterShellCommand(
        ExtensionContext context,
        string commandId,
        ShellCommandKind kind)
    {
        return context.Commands.Register(
            commandId,
            _ => _shellCommandBridge.ExecuteAsync(kind, CancellationToken.None));
    }

    private void RegisterCommandMetadata(ExtensionContext context)
    {
        context.Subscriptions.Add(context.CommandMetadata.Register(
            UndoCommandId,
            new CommandMetadata("Edit: Undo", "Edit", When: "hasDesignerDocument", Keybinding: "Ctrl+Z", Priority: 0)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            RedoCommandId,
            new CommandMetadata("Edit: Redo", "Edit", When: "hasDesignerDocument", Keybinding: "Ctrl+Y", Priority: 10)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            CutCommandId,
            new CommandMetadata("Edit: Cut", "Edit", When: "hasDesignerDocument", Keybinding: "Ctrl+X", Priority: 20)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            CopyCommandId,
            new CommandMetadata("Edit: Copy", "Edit", When: "hasDesignerDocument", Keybinding: "Ctrl+C", Priority: 30)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            PasteCommandId,
            new CommandMetadata("Edit: Paste", "Edit", When: "hasDesignerDocument", Keybinding: "Ctrl+V", Priority: 40)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            DeleteCommandId,
            new CommandMetadata("Edit: Delete", "Edit", When: "hasDesignerDocument", Keybinding: "Delete", Priority: 50)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            SelectAllCommandId,
            new CommandMetadata("Edit: Select All", "Edit", When: "hasDesignerDocument", Keybinding: "Ctrl+A", Priority: 60)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            RenameSymbolCommandId,
            new CommandMetadata("Navigation: Rename Symbol", "Navigation", When: "hasTextDocument", Keybinding: "F2", Priority: 70)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            FormatDocumentCommandId,
            new CommandMetadata("Navigation: Format Document", "Navigation", When: "hasTextDocument", Priority: 80)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            CodeActionsCommandId,
            new CommandMetadata("Navigation: Code Actions", "Navigation", When: "hasTextDocument", Priority: 90)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            DocumentSymbolsCommandId,
            new CommandMetadata("Navigation: Document Symbols", "Navigation", When: "hasTextDocument", Priority: 100)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            WorkspaceSymbolsCommandId,
            new CommandMetadata("Navigation: Workspace Symbols", "Navigation", When: "hasTextDocument", Priority: 110)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            StartDebugCommandId,
            new CommandMetadata("Debug: Start", "Debug", When: "hasWorkspace && debug.idle && !run.active", Keybinding: "F5", Priority: 120)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            StopDebugCommandId,
            new CommandMetadata("Debug: Stop", "Debug", When: "debug.active", Keybinding: "Shift+F5", Priority: 130)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            ContinueDebugCommandId,
            new CommandMetadata("Debug: Continue", "Debug", When: "debug.paused", Keybinding: "F5", Priority: 140)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            StepOverCommandId,
            new CommandMetadata("Debug: Step Over", "Debug", When: "debug.paused", Keybinding: "F10", Priority: 150)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            StepInCommandId,
            new CommandMetadata("Debug: Step In", "Debug", When: "debug.paused", Keybinding: "F11", Priority: 160)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            StepOutCommandId,
            new CommandMetadata("Debug: Step Out", "Debug", When: "debug.paused", Keybinding: "Shift+F11", Priority: 170)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            PauseDebugCommandId,
            new CommandMetadata("Debug: Pause", "Debug", When: "debug.running", Keybinding: "Ctrl+.", Priority: 180)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            ToggleBreakpointCommandId,
            new CommandMetadata("Debug: Toggle Breakpoint", "Debug", When: "hasActiveDocument", Keybinding: "F9", Priority: 190)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            StartRunCommandId,
            new CommandMetadata("Run: Start", "Run", When: "hasWorkspace && debug.idle && !run.active", Priority: 200)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            StopRunCommandId,
            new CommandMetadata("Run: Stop", "Run", When: "run.active", Priority: 210)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            NewTerminalCommandId,
            new CommandMetadata("Terminal: New Terminal", "Tools", Keybinding: "Ctrl+Shift+T", Priority: 220)));
    }

    private void RegisterMenuContributions(ExtensionContext context)
    {
        ExtensionMenuContribution[] menuItems =
        {
            new(LoadCommandId, "Reload Workspace", ExtensionMenuLocations.ToolsWorkspace, WorkspaceGroup, 0),
            new(RestoreCommandId, "Restore", ExtensionMenuLocations.ToolsWorkspace, WorkspaceGroup, 10),
            new(BuildCommandId, "Build", ExtensionMenuLocations.ToolsWorkspace, WorkspaceGroup, 20),
            new(RebuildCommandId, "Rebuild", ExtensionMenuLocations.ToolsWorkspace, WorkspaceGroup, 30),
            new(CleanCommandId, "Clean", ExtensionMenuLocations.ToolsWorkspace, WorkspaceGroup, 40),

            new(UndoCommandId, "Undo", ExtensionMenuLocations.Edit, "0.edit", 0),
            new(RedoCommandId, "Redo", ExtensionMenuLocations.Edit, "0.edit", 10),
            new(CutCommandId, "Cut", ExtensionMenuLocations.Edit, "1.clipboard", 20),
            new(CopyCommandId, "Copy", ExtensionMenuLocations.Edit, "1.clipboard", 30),
            new(PasteCommandId, "Paste", ExtensionMenuLocations.Edit, "1.clipboard", 40),
            new(DeleteCommandId, "Delete", ExtensionMenuLocations.Edit, "1.clipboard", 50),
            new(SelectAllCommandId, "Select All", ExtensionMenuLocations.Edit, "2.selection", 60),
            new(RenameSymbolCommandId, "Rename Symbol", ExtensionMenuLocations.Edit, "3.navigation", 70),
            new(FormatDocumentCommandId, "Format Document", ExtensionMenuLocations.Edit, "3.navigation", 80),
            new(CodeActionsCommandId, "Code Actions", ExtensionMenuLocations.Edit, "3.navigation", 90),
            new(DocumentSymbolsCommandId, "Document Symbols", ExtensionMenuLocations.Edit, "3.navigation", 100),
            new(WorkspaceSymbolsCommandId, "Workspace Symbols", ExtensionMenuLocations.Edit, "3.navigation", 110),

            new(ToggleBreakpointsCommandId, "Breakpoints", ExtensionMenuLocations.View, "views.debug", 120),
            new(ToggleCallStackCommandId, "Call Stack", ExtensionMenuLocations.View, "views.debug", 130),
            new(ToggleLocalsCommandId, "Locals", ExtensionMenuLocations.View, "views.debug", 140),
            new(ToggleWatchesCommandId, "Watches", ExtensionMenuLocations.View, "views.debug", 150),

            new(StartDebugCommandId, "Start", ExtensionMenuLocations.Debug, "0.debug", 0),
            new(StopDebugCommandId, "Stop", ExtensionMenuLocations.Debug, "0.debug", 10),
            new(ContinueDebugCommandId, "Continue", ExtensionMenuLocations.Debug, "1.execution", 20),
            new(StepOverCommandId, "Step Over", ExtensionMenuLocations.Debug, "1.execution", 30),
            new(StepInCommandId, "Step In", ExtensionMenuLocations.Debug, "1.execution", 40),
            new(StepOutCommandId, "Step Out", ExtensionMenuLocations.Debug, "1.execution", 50),
            new(PauseDebugCommandId, "Pause", ExtensionMenuLocations.Debug, "1.execution", 60),
            new(ToggleBreakpointCommandId, "Toggle Breakpoint", ExtensionMenuLocations.Debug, "2.breakpoints", 70),
            new(StartRunCommandId, "Start Run", ExtensionMenuLocations.Debug, "3.run", 80),
            new(StopRunCommandId, "Stop Run", ExtensionMenuLocations.Debug, "3.run", 90),

            new(NewTerminalCommandId, "New Terminal", ExtensionMenuLocations.Tools, "terminal", 10)
        };

        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(context.ExtensionId, menuItems));
    }

    private void RegisterToolbarContributions(ExtensionContext context)
    {
        ExtensionToolbarContribution[] toolbarItems =
        {
            new(NewTerminalCommandId, "New Terminal", "Create terminal session", ExtensionToolbarLocations.Main, "terminal", 10, NewTerminalIconPath),
            new(UndoCommandId, "Undo", "Undo (Ctrl+Z)", ExtensionToolbarLocations.Main, "edit", 20, UndoIconPath),
            new(RedoCommandId, "Redo", "Redo (Ctrl+Y)", ExtensionToolbarLocations.Main, "edit", 30, RedoIconPath),
            new(CutCommandId, "Cut", "Cut (Ctrl+X)", ExtensionToolbarLocations.Main, "edit", 40, CutIconPath),
            new(CopyCommandId, "Copy", "Copy (Ctrl+C)", ExtensionToolbarLocations.Main, "edit", 50, CopyIconPath),
            new(PasteCommandId, "Paste", "Paste (Ctrl+V)", ExtensionToolbarLocations.Main, "edit", 60, PasteIconPath),
            new(DeleteCommandId, "Delete", "Delete", ExtensionToolbarLocations.Main, "edit", 70, DeleteIconPath)
        };

        context.Subscriptions.Add(context.Contributions.RegisterToolbarItems(context.ExtensionId, toolbarItems));
    }

    private void RegisterCommandPaletteContributions(ExtensionContext context)
    {
        ExtensionCommandPaletteContribution[] paletteItems =
        {
            new(LoadCommandId, "Reload Workspace", "Workspace"),
            new(RestoreCommandId, "Restore Workspace", "Workspace"),
            new(BuildCommandId, "Build Workspace", "Workspace"),
            new(RebuildCommandId, "Rebuild Workspace", "Workspace"),
            new(CleanCommandId, "Clean Workspace", "Workspace"),
            new(StartDebugCommandId, "Debug: Start", "Debug"),
            new(StopDebugCommandId, "Debug: Stop", "Debug"),
            new(ContinueDebugCommandId, "Debug: Continue", "Debug"),
            new(StepOverCommandId, "Debug: Step Over", "Debug"),
            new(StepInCommandId, "Debug: Step In", "Debug"),
            new(StepOutCommandId, "Debug: Step Out", "Debug"),
            new(PauseDebugCommandId, "Debug: Pause", "Debug"),
            new(ToggleBreakpointCommandId, "Debug: Toggle Breakpoint", "Debug"),
            new(StartRunCommandId, "Run: Start", "Run"),
            new(StopRunCommandId, "Run: Stop", "Run"),
            new(NewTerminalCommandId, "Terminal: New Terminal", "Tools")
        };

        context.Subscriptions.Add(context.Contributions.RegisterCommandPaletteItems(context.ExtensionId, paletteItems));
    }

    private Task LoadWorkspaceAsync()
    {
        if (!_workspaceCommands.HasWorkspace || string.IsNullOrWhiteSpace(_workspaceInfo.WorkspacePath))
        {
            return ShowNoWorkspaceAsync();
        }

        return _workspaceCommands.LoadWorkspaceAsync(CancellationToken.None);
    }

    private Task ExecuteIfLoadedAsync(Func<CancellationToken, Task> action)
    {
        if (!_workspaceCommands.HasWorkspace)
        {
            return ShowNoWorkspaceAsync();
        }

        return action(CancellationToken.None);
    }

    private Task ShowNoWorkspaceAsync()
    {
        return _window?.ShowInformationMessageAsync("No workspace loaded.", CancellationToken.None)
               ?? Task.CompletedTask;
    }
}
