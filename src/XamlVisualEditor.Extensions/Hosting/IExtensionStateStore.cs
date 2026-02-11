namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Stores extension enablement state.</summary>
public interface IExtensionStateStore
{
    /// <summary>Gets whether an extension is enabled.</summary>
    Task<bool> GetEnabledAsync(string extensionId, CancellationToken ct);

    /// <summary>Sets whether an extension is enabled.</summary>
    Task SetEnabledAsync(string extensionId, bool enabled, CancellationToken ct);

    /// <summary>Gets all stored states.</summary>
    Task<IReadOnlyList<ExtensionStateEntry>> GetAllAsync(CancellationToken ct);
}

/// <summary>Represents extension enablement state.</summary>
public sealed record ExtensionStateEntry(string ExtensionId, bool Enabled);
