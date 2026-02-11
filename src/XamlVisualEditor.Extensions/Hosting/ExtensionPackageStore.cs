using System.IO;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Stores installed extension packages on disk.</summary>
public sealed class ExtensionPackageStore : IExtensionPackageStore
{
    private readonly string _installRoot;
    private readonly ExtensionPackageLoader _loader;

    public ExtensionPackageStore(string installRoot, ExtensionPackageLoader loader)
    {
        _installRoot = installRoot;
        _loader = loader;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExtensionPackageInfo>> GetInstalledAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_installRoot))
        {
            return Array.Empty<ExtensionPackageInfo>();
        }

        List<ExtensionPackageInfo> results = new();
        foreach (string path in Directory.EnumerateFiles(_installRoot, "*.nupkg", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            ExtensionPackageInfo info = await _loader.LoadAsync(path, ct).ConfigureAwait(false);
            results.Add(info);
        }

        return results.Count == 0 ? Array.Empty<ExtensionPackageInfo>() : results;
    }

    /// <inheritdoc />
    public async Task<ExtensionPackageInfo> InstallAsync(string packagePath, CancellationToken ct)
    {
        ExtensionPackageInfo info = await _loader.LoadAsync(packagePath, ct).ConfigureAwait(false);
        string extensionDir = Path.Combine(_installRoot, info.Manifest.ExtensionId, info.Manifest.Version);
        Directory.CreateDirectory(extensionDir);

        string fileName = info.Manifest.ExtensionId + "." + info.Manifest.Version + ".nupkg";
        string targetPath = Path.Combine(extensionDir, fileName);
        File.Copy(packagePath, targetPath, overwrite: true);

        return await _loader.LoadAsync(targetPath, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task UninstallAsync(string extensionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(extensionId))
        {
            throw new ArgumentException("Extension id is required.", nameof(extensionId));
        }

        string extensionDir = Path.Combine(_installRoot, extensionId);
        if (Directory.Exists(extensionDir))
        {
            Directory.Delete(extensionDir, recursive: true);
        }

        return Task.CompletedTask;
    }
}
