namespace XamlVisualEditor.Extensions;

/// <summary>Hosts modal dialogs for extensions.</summary>
public interface IDialogHost
{
    /// <summary>Registers a dialog factory.</summary>
    IDisposable RegisterDialog(string dialogId, Func<object?, object> factory);

    /// <summary>Shows a dialog by id.</summary>
    Task<T?> ShowDialogAsync<T>(string dialogId, object? viewModel, CancellationToken cancellationToken);
}

/// <summary>Controls workspace opening behavior.</summary>
public interface IWorkspaceHost
{
    /// <summary>Opens a workspace path.</summary>
    Task OpenWorkspaceAsync(string workspacePath, WorkspaceOpenMode mode, CancellationToken cancellationToken);
}

/// <summary>Workspace open modes.</summary>
public enum WorkspaceOpenMode
{
    /// <summary>Open in the current window.</summary>
    CurrentWindow,

    /// <summary>Open in a new window.</summary>
    NewWindow
}