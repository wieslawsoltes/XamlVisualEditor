using System.Reactive.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionManagerViewModelTests
{
    [Fact]
    public async Task RefreshAsync_PopulatesPackages()
    {
        FakeManager manager = new();
        ExtensionManagerViewModel viewModel = new(manager, () => Task.FromResult<string?>(null));

        await viewModel.RefreshCommand.Execute().ToTask();

        Assert.Single(viewModel.InstalledPackages);
        Assert.Equal("example.sample", viewModel.InstalledPackages[0].ExtensionId);
    }

    [Fact]
    public async Task EnableToggle_CallsManager()
    {
        FakeManager manager = new();
        ExtensionManagerViewModel viewModel = new(manager, () => Task.FromResult<string?>(null));

        await viewModel.RefreshCommand.Execute().ToTask();
        ExtensionPackageItemViewModel item = viewModel.InstalledPackages[0];

        item.IsEnabled = false;
        await manager.DisabledSignal.Task;

        Assert.Contains("example.sample", manager.Disabled);
    }

    private sealed class FakeManager : IExtensionManager
    {
        public HashSet<string> Disabled { get; } = new(StringComparer.OrdinalIgnoreCase);
        public TaskCompletionSource<bool> DisabledSignal { get; } = new();

        public Task<IReadOnlyList<ExtensionPackageInfo>> GetInstalledAsync(CancellationToken ct)
        {
            ExtensionManifest manifest = new()
            {
                Name = "sample",
                Publisher = "example",
                Version = "1.0.0"
            };
            return Task.FromResult<IReadOnlyList<ExtensionPackageInfo>>(new[]
            {
                new ExtensionPackageInfo("sample.nupkg", manifest)
            });
        }

        public Task<ExtensionPackageInfo> InstallAsync(string packagePath, CancellationToken ct)
        {
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

        public Task<bool> GetEnabledAsync(string extensionId, CancellationToken ct)
        {
            return Task.FromResult(true);
        }

        public Task SetEnabledAsync(string extensionId, bool enabled, CancellationToken ct)
        {
            if (!enabled)
            {
                Disabled.Add(extensionId);
                DisabledSignal.TrySetResult(true);
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ExtensionUpdateInfo>> CheckForUpdatesAsync(CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<ExtensionUpdateInfo>>(Array.Empty<ExtensionUpdateInfo>());
        }
    }
}
