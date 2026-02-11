namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Compares installed packages with available updates.</summary>
public sealed class ExtensionUpdateService : IExtensionUpdateService
{
    private readonly IExtensionPackageStore _store;
    private readonly IExtensionPackageCatalog _catalog;

    public ExtensionUpdateService(IExtensionPackageStore store, IExtensionPackageCatalog catalog)
    {
        _store = store;
        _catalog = catalog;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExtensionUpdateInfo>> CheckForUpdatesAsync(CancellationToken ct)
    {
        IReadOnlyList<ExtensionPackageInfo> installed = await _store.GetInstalledAsync(ct).ConfigureAwait(false);
        IReadOnlyList<ExtensionPackageInfo> available = await _catalog.GetAvailableAsync(ct).ConfigureAwait(false);

        Dictionary<string, ExtensionPackageInfo> latestAvailable = new(StringComparer.OrdinalIgnoreCase);
        foreach (ExtensionPackageInfo package in available)
        {
            string id = package.Manifest.ExtensionId;
            if (!latestAvailable.TryGetValue(id, out ExtensionPackageInfo? existing)
                || ExtensionVersionComparer.IsNewer(package.Manifest.Version, existing.Manifest.Version))
            {
                latestAvailable[id] = package;
            }
        }

        List<ExtensionUpdateInfo> updates = new();
        foreach (ExtensionPackageInfo package in installed)
        {
            string id = package.Manifest.ExtensionId;
            if (latestAvailable.TryGetValue(id, out ExtensionPackageInfo? latest)
                && ExtensionVersionComparer.IsNewer(latest.Manifest.Version, package.Manifest.Version))
            {
                updates.Add(new ExtensionUpdateInfo(package, latest));
            }
        }

        return updates.Count == 0 ? Array.Empty<ExtensionUpdateInfo>() : updates;
    }

    private static class ExtensionVersionComparer
    {
        public static bool IsNewer(string candidate, string current)
        {
            return Compare(candidate, current) > 0;
        }

        private static int Compare(string left, string right)
        {
            if (Version.TryParse(left, out Version? leftVersion) && Version.TryParse(right, out Version? rightVersion))
            {
                return leftVersion.CompareTo(rightVersion);
            }

            return StringComparer.OrdinalIgnoreCase.Compare(left, right);
        }
    }
}
