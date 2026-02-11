namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Coordinates extension install and enablement.</summary>
public interface IExtensionManager
{
    /// <summary>Gets installed packages.</summary>
    Task<IReadOnlyList<ExtensionPackageInfo>> GetInstalledAsync(CancellationToken ct);

    /// <summary>Installs a NuGet package.</summary>
    Task<ExtensionPackageInfo> InstallAsync(string packagePath, CancellationToken ct);

    /// <summary>Uninstalls an extension.</summary>
    Task UninstallAsync(string extensionId, CancellationToken ct);

    /// <summary>Gets whether an extension is enabled.</summary>
    Task<bool> GetEnabledAsync(string extensionId, CancellationToken ct);

    /// <summary>Sets whether an extension is enabled.</summary>
    Task SetEnabledAsync(string extensionId, bool enabled, CancellationToken ct);

    /// <summary>Checks for updates.</summary>
    Task<IReadOnlyList<ExtensionUpdateInfo>> CheckForUpdatesAsync(CancellationToken ct);
}
