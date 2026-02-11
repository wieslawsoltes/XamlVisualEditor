namespace XamlVisualEditor.Extensions;

/// <summary>Provides access to workspace files and settings.</summary>
public interface IWorkspace
{
    /// <summary>Finds files using glob patterns.</summary>
    Task<IReadOnlyList<string>> FindFilesAsync(
        string includeGlob,
        string? excludeGlob,
        CancellationToken cancellationToken);

    /// <summary>Reads a file as bytes.</summary>
    Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken);

    /// <summary>Writes file content.</summary>
    Task WriteFileAsync(string path, byte[] content, CancellationToken cancellationToken);

    /// <summary>Creates a file system watcher.</summary>
    IFileSystemWatcher CreateFileSystemWatcher(string glob);

    /// <summary>Raised when configuration changes.</summary>
    event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
}

/// <summary>Provides access to configuration values.</summary>
public interface ISettings
{
    /// <summary>Gets a configuration value.</summary>
    T? Get<T>(string section, T? defaultValue = default);

    /// <summary>Updates a configuration value.</summary>
    Task UpdateAsync(string section, object? value, SettingsTarget target, CancellationToken cancellationToken);
}

/// <summary>Configuration update targets.</summary>
public enum SettingsTarget
{
    /// <summary>User-level settings.</summary>
    User,

    /// <summary>Workspace-level settings.</summary>
    Workspace
}

/// <summary>Observes file system changes.</summary>
public interface IFileSystemWatcher : IDisposable
{
    /// <summary>Raised when a file is created.</summary>
    event EventHandler<string> Created;

    /// <summary>Raised when a file is changed.</summary>
    event EventHandler<string> Changed;

    /// <summary>Raised when a file is deleted.</summary>
    event EventHandler<string> Deleted;
}

/// <summary>Provides information about a configuration change.</summary>
public sealed class ConfigurationChangedEventArgs : EventArgs
{
    /// <summary>Creates the event args.</summary>
    public ConfigurationChangedEventArgs(string? section)
    {
        Section = section;
    }

    /// <summary>Gets the affected section.</summary>
    public string? Section { get; }
}
