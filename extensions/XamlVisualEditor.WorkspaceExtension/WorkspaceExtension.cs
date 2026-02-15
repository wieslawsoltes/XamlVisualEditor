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
    private const string NewTerminalIconPath = "M5.65 9.15c.2-.2.5-.2.7 0l2 2a.5.5 0 0 1 0 .7l-2 2a.5.5 0 0 1-.7-.7l1.64-1.65-1.64-1.65a.5.5 0 0 1 0-.7ZM14.5 13h-5a.5.5 0 0 0 0 1h5a.5.5 0 0 0 0-1ZM3 5.5A2.5 2.5 0 0 1 5.5 3h9A2.5 2.5 0 0 1 17 5.5v9a2.5 2.5 0 0 1-2.5 2.5h-9A2.5 2.5 0 0 1 3 14.5v-9ZM16 6v-.5c0-.83-.68-1.5-1.5-1.5h-9C4.67 4 4 4.67 4 5.5V6h12ZM4 7v7.5c0 .83.67 1.5 1.5 1.5h9c.82 0 1.5-.67 1.5-1.5V7H4Z";
    private const string UndoIconPath = "M5 2.5a.5.5 0 0 0-1 0v4.9c0 .33.27.6.6.6h4.9a.5.5 0 0 0 0-1H5.9l3.48-3.02a4 4 0 0 1 5.25 6.04l-8.17 7.1a.5.5 0 0 0 .65.76l8.17-7.1a5 5 0 0 0-6.56-7.55L5 6.46V2.5Z";
    private const string RedoIconPath = "M15 2.5a.5.5 0 0 1 1 0v4.9a.6.6 0 0 1-.6.6h-4.9a.5.5 0 0 1 0-1h3.6l-3.48-3.02a4 4 0 1 0-5.24 6.04l8.17 7.1a.5.5 0 1 1-.66.76l-8.17-7.1a5 5 0 1 1 6.56-7.55L15 6.46V2.5Z";
    private const string CutIconPath = "M5.92 2.23a.5.5 0 0 0-.84.54L9.4 9.43l-1.92 2.96a3 3 0 1 0 .78.64L10 10.35l1.74 2.68a3 3 0 1 0 .78-.64L5.92 2.23ZM14 17a2 2 0 1 1 0-4 2 2 0 0 1 0 4ZM4 15a2 2 0 1 1 4 0 2 2 0 0 1-4 0Zm7.2-6.49-.6-.92 3.48-5.36a.5.5 0 0 1 .84.54l-3.73 5.74Z";
    private const string CopyIconPath = "M8 2a2 2 0 0 0-2 2v10c0 1.1.9 2 2 2h6a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H8ZM7 4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H8a1 1 0 0 1-1-1V4ZM4 6a2 2 0 0 1 1-1.73V14.5A2.5 2.5 0 0 0 7.5 17h6.23A2 2 0 0 1 12 18H7.5A3.5 3.5 0 0 1 4 14.5V6Z";
    private const string PasteIconPath = "M4.5 4h1.59c.2.58.76 1 1.41 1h3c.65 0 1.2-.42 1.41-1h1.59c.28 0 .5.22.5.5v1a.5.5 0 0 0 1 0v-1c0-.83-.67-1.5-1.5-1.5h-1.59c-.2-.58-.76-1-1.41-1h-3c-.65 0-1.2.42-1.41 1H4.5C3.67 3 3 3.67 3 4.5v12c0 .83.67 1.5 1.5 1.5h3a.5.5 0 0 0 0-1h-3a.5.5 0 0 1-.5-.5v-12c0-.28.22-.5.5-.5Zm3 0a.5.5 0 0 1 0-1h3a.5.5 0 0 1 0 1h-3Zm3 3C9.67 7 9 7.67 9 8.5v8c0 .83.67 1.5 1.5 1.5h5c.83 0 1.5-.67 1.5-1.5v-8c0-.83-.67-1.5-1.5-1.5h-5ZM10 8.5c0-.28.22-.5.5-.5h5c.28 0 .5.22.5.5v8a.5.5 0 0 1-.5.5h-5a.5.5 0 0 1-.5-.5v-8Z";
    private const string DeleteIconPath = "M8.5 4h3a1.5 1.5 0 0 0-3 0Zm-1 0a2.5 2.5 0 0 1 5 0h5a.5.5 0 0 1 0 1h-1.05l-1.2 10.34A3 3 0 0 1 12.27 18H7.73a3 3 0 0 1-2.98-2.66L3.55 5H2.5a.5.5 0 0 1 0-1h5ZM5.74 15.23A2 2 0 0 0 7.73 17h4.54a2 2 0 0 0 1.99-1.77L15.44 5H4.56l1.18 10.23ZM8.5 7.5c.28 0 .5.22.5.5v6a.5.5 0 0 1-1 0V8c0-.28.22-.5.5-.5ZM12 8a.5.5 0 0 0-1 0v6a.5.5 0 0 0 1 0V8Z";

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
