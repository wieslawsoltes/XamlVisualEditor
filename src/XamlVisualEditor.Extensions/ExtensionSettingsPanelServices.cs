namespace XamlVisualEditor.Extensions;

/// <summary>Represents debug settings state exposed to extensions.</summary>
public sealed class DebugSettingsState
{
    /// <summary>Creates debug settings state.</summary>
    public DebugSettingsState(string adapterPath, bool autoDownloadTools, bool isBusy, string statusText)
    {
        AdapterPath = adapterPath;
        AutoDownloadTools = autoDownloadTools;
        IsBusy = isBusy;
        StatusText = statusText;
    }

    /// <summary>Gets debugger adapter path.</summary>
    public string AdapterPath { get; }

    /// <summary>Gets auto-download flag for debug tools.</summary>
    public bool AutoDownloadTools { get; }

    /// <summary>Gets whether a settings operation is in progress.</summary>
    public bool IsBusy { get; }

    /// <summary>Gets the last status text.</summary>
    public string StatusText { get; }
}

/// <summary>Provides debug settings change payload.</summary>
public sealed class DebugSettingsChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public DebugSettingsChangedEventArgs(DebugSettingsState state)
    {
        State = state;
    }

    /// <summary>Gets updated debug settings state.</summary>
    public DebugSettingsState State { get; }
}

/// <summary>Represents LSP server settings consumed by extensions.</summary>
public sealed class LspServerSettings
{
    /// <summary>Gets or sets language id.</summary>
    public string LanguageId { get; set; } = string.Empty;

    /// <summary>Gets or sets server executable path.</summary>
    public string ServerPath { get; set; } = string.Empty;

    /// <summary>Gets or sets server arguments.</summary>
    public IReadOnlyList<string> Arguments { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets file extensions.</summary>
    public IReadOnlyList<string> FileExtensions { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets optional working directory.</summary>
    public string? WorkingDirectory { get; set; }
}

/// <summary>Provides LSP settings change payload.</summary>
public sealed class LspSettingsChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public LspSettingsChangedEventArgs(IReadOnlyList<LspServerSettings> servers)
    {
        Servers = servers;
    }

    /// <summary>Gets current server settings.</summary>
    public IReadOnlyList<LspServerSettings> Servers { get; }
}

/// <summary>Provides access to debug settings host services.</summary>
public interface IDebugSettingsHost
{
    /// <summary>Gets current debug settings state snapshot.</summary>
    DebugSettingsState GetState();

    /// <summary>Updates debugger adapter path.</summary>
    Task SetAdapterPathAsync(string adapterPath, CancellationToken cancellationToken);

    /// <summary>Updates auto-download setting.</summary>
    Task SetAutoDownloadToolsAsync(bool autoDownloadTools, CancellationToken cancellationToken);

    /// <summary>Downloads/installs netcoredbg through host workflow.</summary>
    Task DownloadNetcoredbgAsync(CancellationToken cancellationToken);

    /// <summary>Raised when debug settings state changes.</summary>
    event EventHandler<DebugSettingsChangedEventArgs>? Changed;
}

/// <summary>Provides access to LSP settings host services.</summary>
public interface ILspSettingsHost
{
    /// <summary>Gets LSP settings storage path.</summary>
    string SettingsPath { get; }

    /// <summary>Loads configured LSP servers.</summary>
    Task<IReadOnlyList<LspServerSettings>> LoadServersAsync(CancellationToken cancellationToken);

    /// <summary>Saves configured LSP servers.</summary>
    Task SaveServersAsync(IReadOnlyList<LspServerSettings> servers, CancellationToken cancellationToken);

    /// <summary>Raised when LSP settings are updated.</summary>
    event EventHandler<LspSettingsChangedEventArgs>? Changed;
}
