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
}
