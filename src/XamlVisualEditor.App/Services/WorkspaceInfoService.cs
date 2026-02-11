using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.App.Services;

/// <summary>
/// Tracks the active workspace path for extensions.
/// </summary>
public sealed class WorkspaceInfoService : IWorkspaceInfo, IWorkspaceInfoUpdater
{
    private string? _workspacePath;

    public string? WorkspacePath => _workspacePath;

    public event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;

    public void UpdateWorkspacePath(string? workspacePath)
    {
        if (string.Equals(_workspacePath, workspacePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _workspacePath = workspacePath;
        WorkspaceChanged?.Invoke(this, new WorkspaceChangedEventArgs(_workspacePath));
    }
}
