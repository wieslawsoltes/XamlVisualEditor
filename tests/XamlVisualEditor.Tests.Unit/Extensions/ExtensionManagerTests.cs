using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionManagerTests
{
    [Fact]
    public async Task ManagerDelegatesToStores()
    {
        FakeStore store = new();
        FakeStateStore stateStore = new();
        FakeUpdateService updates = new();
        ExtensionManager manager = new(store, stateStore, updates);

        await manager.InstallAsync("path.nupkg", CancellationToken.None);
        await manager.SetEnabledAsync("example.sample", true, CancellationToken.None);
        bool enabled = await manager.GetEnabledAsync("example.sample", CancellationToken.None);

        Assert.True(store.InstallCalled);
        Assert.True(enabled);
    }

    private sealed class FakeStore : IExtensionPackageStore
    {
        public bool InstallCalled { get; private set; }

        public Task<IReadOnlyList<ExtensionPackageInfo>> GetInstalledAsync(CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<ExtensionPackageInfo>>(Array.Empty<ExtensionPackageInfo>());
        }

        public Task<ExtensionPackageInfo> InstallAsync(string packagePath, CancellationToken ct)
        {
            InstallCalled = true;
            ExtensionManifest manifest = new()
            {
                Name = "sample",
                Publisher = "example",
                Version = "1.0.0"
            };
            return Task.FromResult(new ExtensionPackageInfo(packagePath, manifest));
        }

        public Task UninstallAsync(string extensionId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStateStore : IExtensionStateStore
    {
        private readonly Dictionary<string, bool> _states = new(StringComparer.OrdinalIgnoreCase);

        public Task<bool> GetEnabledAsync(string extensionId, CancellationToken ct)
        {
            _states.TryGetValue(extensionId, out bool enabled);
            return Task.FromResult(enabled);
        }

        public Task SetEnabledAsync(string extensionId, bool enabled, CancellationToken ct)
        {
            _states[extensionId] = enabled;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ExtensionStateEntry>> GetAllAsync(CancellationToken ct)
        {
            List<ExtensionStateEntry> items = new();
            foreach (KeyValuePair<string, bool> entry in _states)
            {
                items.Add(new ExtensionStateEntry(entry.Key, entry.Value));
            }

            return Task.FromResult<IReadOnlyList<ExtensionStateEntry>>(items);
        }
    }

    private sealed class FakeUpdateService : IExtensionUpdateService
    {
        public Task<IReadOnlyList<ExtensionUpdateInfo>> CheckForUpdatesAsync(CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<ExtensionUpdateInfo>>(Array.Empty<ExtensionUpdateInfo>());
        }
    }
}
