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

    /// <summary>Registers a settings schema for a section.</summary>
    IDisposable RegisterSchema(SettingsSectionSchema schema);

    /// <summary>Gets all known settings schemas.</summary>
    IReadOnlyList<SettingsSectionSchema> GetSchemas();

    /// <summary>Attempts to resolve a schema by section.</summary>
    bool TryGetSchema(string section, out SettingsSectionSchema schema);

    /// <summary>Validates a value against a registered section schema.</summary>
    IReadOnlyList<SettingsValidationIssue> Validate(string section, object? value);

    /// <summary>Subscribes to typed change notifications for a section.</summary>
    IDisposable SubscribeSection<T>(string section, Action<SettingsSectionChangedEventArgs<T>> handler);

    /// <summary>Raised when a settings section changes.</summary>
    event EventHandler<SettingsSectionChangedEventArgs>? SectionChanged;
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

/// <summary>Describes settings section metadata and optional validation.</summary>
public sealed class SettingsSectionSchema
{
    /// <summary>Creates schema metadata.</summary>
    public SettingsSectionSchema(
        string section,
        string displayName,
        string description,
        string? valueKind = null,
        Func<object?, IReadOnlyList<SettingsValidationIssue>>? validator = null)
    {
        Section = section;
        DisplayName = displayName;
        Description = description;
        ValueKind = valueKind;
        Validator = validator;
    }

    /// <summary>Gets the settings section key.</summary>
    public string Section { get; }

    /// <summary>Gets the human-readable section name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the section description.</summary>
    public string Description { get; }

    /// <summary>Gets a human-readable value kind for tooling.</summary>
    public string? ValueKind { get; }

    /// <summary>Gets the optional section validator.</summary>
    public Func<object?, IReadOnlyList<SettingsValidationIssue>>? Validator { get; }
}

/// <summary>Represents a validation issue for a settings value.</summary>
public sealed record SettingsValidationIssue(string Message, string? Field = null);

/// <summary>Provides information about a settings section change.</summary>
public sealed class SettingsSectionChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public SettingsSectionChangedEventArgs(string section, SettingsTarget target, object? value)
    {
        Section = section;
        Target = target;
        Value = value;
    }

    /// <summary>Gets the section key.</summary>
    public string Section { get; }

    /// <summary>Gets the update target.</summary>
    public SettingsTarget Target { get; }

    /// <summary>Gets the new value.</summary>
    public object? Value { get; }
}

/// <summary>Provides a typed settings section change payload.</summary>
public sealed class SettingsSectionChangedEventArgs<T> : EventArgs
{
    /// <summary>Creates event args.</summary>
    public SettingsSectionChangedEventArgs(string section, SettingsTarget target, T? value)
    {
        Section = section;
        Target = target;
        Value = value;
    }

    /// <summary>Gets the section key.</summary>
    public string Section { get; }

    /// <summary>Gets the update target.</summary>
    public SettingsTarget Target { get; }

    /// <summary>Gets the typed section value.</summary>
    public T? Value { get; }
}
