namespace XamlVisualEditor.Extensions;

/// <summary>Provides information about the active workspace.</summary>
public interface IWorkspaceInfo
{
    /// <summary>Gets the current workspace path.</summary>
    string? WorkspacePath { get; }

    /// <summary>Raised when the workspace changes.</summary>
    event EventHandler<WorkspaceChangedEventArgs> WorkspaceChanged;
}

/// <summary>Updates workspace info.</summary>
public interface IWorkspaceInfoUpdater
{
    /// <summary>Updates the current workspace path.</summary>
    void UpdateWorkspacePath(string? workspacePath);
}

/// <summary>Workspace change notification.</summary>
public sealed class WorkspaceChangedEventArgs : EventArgs
{
    /// <summary>Creates the event args.</summary>
    public WorkspaceChangedEventArgs(string? workspacePath)
    {
        WorkspacePath = workspacePath;
    }

    /// <summary>Gets the current workspace path.</summary>
    public string? WorkspacePath { get; }
}
