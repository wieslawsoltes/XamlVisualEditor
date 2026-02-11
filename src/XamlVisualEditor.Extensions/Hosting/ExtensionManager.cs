namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Default extension manager implementation.</summary>
public sealed class ExtensionManager : IExtensionManager
{
    private readonly IExtensionPackageStore _store;
    private readonly IExtensionStateStore _stateStore;
    private readonly IExtensionUpdateService _updateService;

    public ExtensionManager(
        IExtensionPackageStore store,
        IExtensionStateStore stateStore,
        IExtensionUpdateService updateService)
    {
        _store = store;
        _stateStore = stateStore;
        _updateService = updateService;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ExtensionPackageInfo>> GetInstalledAsync(CancellationToken ct)
    {
        return _store.GetInstalledAsync(ct);
    }

    /// <inheritdoc />
    public Task<ExtensionPackageInfo> InstallAsync(string packagePath, CancellationToken ct)
    {
        return _store.InstallAsync(packagePath, ct);
    }

    /// <inheritdoc />
    public Task UninstallAsync(string extensionId, CancellationToken ct)
    {
        return _store.UninstallAsync(extensionId, ct);
    }

    /// <inheritdoc />
    public Task<bool> GetEnabledAsync(string extensionId, CancellationToken ct)
    {
        return _stateStore.GetEnabledAsync(extensionId, ct);
    }

    /// <inheritdoc />
    public Task SetEnabledAsync(string extensionId, bool enabled, CancellationToken ct)
    {
        return _stateStore.SetEnabledAsync(extensionId, enabled, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ExtensionUpdateInfo>> CheckForUpdatesAsync(CancellationToken ct)
    {
        return _updateService.CheckForUpdatesAsync(ct);
    }
}
