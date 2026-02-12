using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Default extension manager implementation.</summary>
public sealed class ExtensionManager : IExtensionManager
{
    private readonly IExtensionPackageStore _store;
    private readonly IExtensionStateStore _stateStore;
    private readonly IExtensionUpdateService _updateService;
    private readonly Lazy<IReadOnlyList<ExtensionPackageInfo>> _builtInPackages;
    private readonly Lazy<HashSet<string>> _builtInIds;

    public ExtensionManager(
        IExtensionPackageStore store,
        IExtensionStateStore stateStore,
        IExtensionUpdateService updateService,
        IServiceProvider services)
    {
        _store = store;
        _stateStore = stateStore;
        _updateService = updateService;
        _builtInPackages = new Lazy<IReadOnlyList<ExtensionPackageInfo>>(() =>
        {
            IEnumerable<IXveExtension> extensions = services.GetServices<IXveExtension>();
            return BuildBuiltInPackages(extensions ?? Array.Empty<IXveExtension>());
        });
        _builtInIds = new Lazy<HashSet<string>>(() =>
            new HashSet<string>(
                _builtInPackages.Value.Select(package => package.Manifest.ExtensionId),
                StringComparer.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExtensionPackageInfo>> GetInstalledAsync(CancellationToken ct)
    {
        IReadOnlyList<ExtensionPackageInfo> installed = await _store.GetInstalledAsync(ct).ConfigureAwait(false);
        if (_builtInPackages.Value.Count == 0)
        {
            return installed;
        }

        Dictionary<string, ExtensionPackageInfo> merged = new(StringComparer.OrdinalIgnoreCase);
        foreach (ExtensionPackageInfo package in installed)
        {
            merged[package.Manifest.ExtensionId] = package;
        }

        foreach (ExtensionPackageInfo package in _builtInPackages.Value)
        {
            if (!merged.ContainsKey(package.Manifest.ExtensionId))
            {
                merged.Add(package.Manifest.ExtensionId, package);
            }
        }

        return merged.Values.ToArray();
    }

    /// <inheritdoc />
    public Task<ExtensionPackageInfo> InstallAsync(string packagePath, CancellationToken ct)
    {
        return _store.InstallAsync(packagePath, ct);
    }

    /// <inheritdoc />
    public Task UninstallAsync(string extensionId, CancellationToken ct)
    {
        if (IsBuiltIn(extensionId))
        {
            return Task.CompletedTask;
        }

        return _store.UninstallAsync(extensionId, ct);
    }

    /// <inheritdoc />
    public Task<bool> GetEnabledAsync(string extensionId, CancellationToken ct)
    {
        if (IsBuiltIn(extensionId))
        {
            return Task.FromResult(true);
        }

        return _stateStore.GetEnabledAsync(extensionId, ct);
    }

    /// <inheritdoc />
    public Task SetEnabledAsync(string extensionId, bool enabled, CancellationToken ct)
    {
        if (IsBuiltIn(extensionId))
        {
            return Task.CompletedTask;
        }

        return _stateStore.SetEnabledAsync(extensionId, enabled, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ExtensionUpdateInfo>> CheckForUpdatesAsync(CancellationToken ct)
    {
        return _updateService.CheckForUpdatesAsync(ct);
    }

    private bool IsBuiltIn(string extensionId)
    {
        return !string.IsNullOrWhiteSpace(extensionId) && _builtInIds.Value.Contains(extensionId);
    }

    private static IReadOnlyList<ExtensionPackageInfo> BuildBuiltInPackages(IEnumerable<IXveExtension> extensions)
    {
        List<ExtensionPackageInfo> results = new();
        foreach (IXveExtension extension in extensions)
        {
            Type type = extension.GetType();
            Assembly assembly = type.Assembly;
            string name = assembly.GetName().Name ?? type.Name;
            string version = assembly.GetName().Version?.ToString() ?? "0.0.0";
            ExtensionManifest manifest = new()
            {
                Name = name,
                Publisher = "builtin",
                Version = version,
                DisplayName = name
            };

            string packagePath = "builtin:" + manifest.ExtensionId;
            results.Add(new ExtensionPackageInfo(packagePath, manifest));
        }

        return results;
    }
}
