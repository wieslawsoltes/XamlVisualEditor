using System.IO;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Loads packages from a local catalog folder.</summary>
public sealed class LocalExtensionPackageCatalog : IExtensionPackageCatalog
{
    private readonly string _catalogRoot;
    private readonly ExtensionPackageLoader _loader;

    public LocalExtensionPackageCatalog(string catalogRoot, ExtensionPackageLoader loader)
    {
        _catalogRoot = catalogRoot;
        _loader = loader;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExtensionPackageInfo>> GetAvailableAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_catalogRoot))
        {
            return Array.Empty<ExtensionPackageInfo>();
        }

        List<ExtensionPackageInfo> results = new();
        foreach (string path in Directory.EnumerateFiles(_catalogRoot, "*.nupkg", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            ExtensionPackageInfo info = await _loader.LoadAsync(path, ct).ConfigureAwait(false);
            results.Add(info);
        }

        return results.Count == 0 ? Array.Empty<ExtensionPackageInfo>() : results;
    }
}
