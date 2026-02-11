namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Manages installed extension packages.</summary>
public interface IExtensionPackageStore
{
    /// <summary>Gets installed packages.</summary>
    Task<IReadOnlyList<ExtensionPackageInfo>> GetInstalledAsync(CancellationToken ct);

    /// <summary>Installs a package from a NuGet archive.</summary>
    Task<ExtensionPackageInfo> InstallAsync(string packagePath, CancellationToken ct);

    /// <summary>Uninstalls an extension by id.</summary>
    Task UninstallAsync(string extensionId, CancellationToken ct);
}
