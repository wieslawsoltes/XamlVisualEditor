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

    private readonly IWorkspaceCommands _workspaceCommands;
    private readonly IWorkspaceInfo _workspaceInfo;
    private IWindow? _window;

    public WorkspaceExtension(IWorkspaceCommands workspaceCommands, IWorkspaceInfo workspaceInfo)
    {
        _workspaceCommands = workspaceCommands;
        _workspaceInfo = workspaceInfo;
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

        ExtensionMenuContribution[] menuItems =
        {
            new(LoadCommandId, "Reload Workspace", ExtensionMenuLocations.ToolsWorkspace, WorkspaceGroup, 0),
            new(RestoreCommandId, "Restore", ExtensionMenuLocations.ToolsWorkspace, WorkspaceGroup, 10),
            new(BuildCommandId, "Build", ExtensionMenuLocations.ToolsWorkspace, WorkspaceGroup, 20),
            new(RebuildCommandId, "Rebuild", ExtensionMenuLocations.ToolsWorkspace, WorkspaceGroup, 30),
            new(CleanCommandId, "Clean", ExtensionMenuLocations.ToolsWorkspace, WorkspaceGroup, 40)
        };
        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(context.ExtensionId, menuItems));

        ExtensionCommandPaletteContribution[] paletteItems =
        {
            new(LoadCommandId, "Reload Workspace", "Workspace"),
            new(RestoreCommandId, "Restore Workspace", "Workspace"),
            new(BuildCommandId, "Build Workspace", "Workspace"),
            new(RebuildCommandId, "Rebuild Workspace", "Workspace"),
            new(CleanCommandId, "Clean Workspace", "Workspace")
        };
        context.Subscriptions.Add(context.Contributions.RegisterCommandPaletteItems(context.ExtensionId, paletteItems));

        return Task.CompletedTask;
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
