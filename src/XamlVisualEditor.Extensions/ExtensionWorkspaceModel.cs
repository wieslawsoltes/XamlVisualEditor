namespace XamlVisualEditor.Extensions;

/// <summary>Provides workspace model APIs for extensions.</summary>
public interface IWorkspaceModel
{
    /// <summary>Gets whether a workspace is currently loaded.</summary>
    bool HasWorkspace { get; }

    /// <summary>Gets the active workspace path.</summary>
    string? WorkspacePath { get; }

    /// <summary>Raised when workspace model state changes.</summary>
    event EventHandler<WorkspaceModelChangedEventArgs>? Changed;

    /// <summary>Gets discovered workspace projects.</summary>
    Task<IReadOnlyList<WorkspaceProjectInfo>> GetProjectsAsync(CancellationToken cancellationToken);

    /// <summary>Loads or reloads the active workspace.</summary>
    Task LoadAsync(CancellationToken cancellationToken);

    /// <summary>Restores workspace dependencies.</summary>
    Task RestoreAsync(CancellationToken cancellationToken);

    /// <summary>Builds the workspace.</summary>
    Task BuildAsync(CancellationToken cancellationToken);

    /// <summary>Rebuilds the workspace.</summary>
    Task RebuildAsync(CancellationToken cancellationToken);

    /// <summary>Cleans workspace outputs.</summary>
    Task CleanAsync(CancellationToken cancellationToken);
}

/// <summary>Workspace model change event args.</summary>
public sealed class WorkspaceModelChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public WorkspaceModelChangedEventArgs(string? workspacePath, bool hasWorkspace)
    {
        WorkspacePath = workspacePath;
        HasWorkspace = hasWorkspace;
    }

    /// <summary>Gets the current workspace path.</summary>
    public string? WorkspacePath { get; }

    /// <summary>Gets whether a workspace is loaded.</summary>
    public bool HasWorkspace { get; }
}

/// <summary>Lightweight project descriptor for extension consumption.</summary>
public sealed record WorkspaceProjectInfo(
    string Name,
    string ProjectPath,
    string? TargetFramework,
    bool IsStartupProject);
