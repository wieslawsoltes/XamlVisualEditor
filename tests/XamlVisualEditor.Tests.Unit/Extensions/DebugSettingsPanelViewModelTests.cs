using System;
using System.Reactive.Threading.Tasks;
using XamlVisualEditor.DebugSettingsExtension;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Debugging;
using XamlVisualEditor.Extensions.Hosting;
using DebugSettingsExtensionEntry = XamlVisualEditor.DebugSettingsExtension.DebugSettingsExtension;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class DebugSettingsPanelViewModelTests
{
    [Fact]
    public void Constructor_InitializesStateAndServices()
    {
        DebuggerServiceRegistry registry = new();
        registry.Register(new DebuggerServiceRegistration(
            "debugger.netcoredbg",
            "Netcoredbg",
            new FakeDebuggerService()),
            makeDefault: true);

        StubDebugSettingsHost host = new("tools/netcoredbg", autoDownloadTools: true, isBusy: false, statusText: "Ready");
        DebugSettingsPanelViewModel viewModel = new(registry, host);

        Assert.Equal("tools/netcoredbg", viewModel.AdapterPath);
        Assert.True(viewModel.AutoDownloadTools);
        Assert.Single(viewModel.DebuggerServices);
        Assert.Equal("debugger.netcoredbg", viewModel.SelectedDebuggerService?.Id);
    }

    [Fact]
    public void AdapterAndAutoDownloadChanges_PropagateToHost()
    {
        DebuggerServiceRegistry registry = new();
        registry.Register(new DebuggerServiceRegistration(
            "debugger.netcoredbg",
            "Netcoredbg",
            new FakeDebuggerService()),
            makeDefault: true);

        StubDebugSettingsHost host = new(string.Empty, autoDownloadTools: false, isBusy: false, statusText: string.Empty);
        DebugSettingsPanelViewModel viewModel = new(registry, host);

        viewModel.AdapterPath = "/tmp/netcoredbg";
        viewModel.AutoDownloadTools = true;

        Assert.Equal("/tmp/netcoredbg", host.AdapterPath);
        Assert.True(host.AutoDownloadTools);
    }

    [Fact]
    public async Task DownloadCommand_DelegatesToHost()
    {
        DebuggerServiceRegistry registry = new();
        registry.Register(new DebuggerServiceRegistration(
            "debugger.netcoredbg",
            "Netcoredbg",
            new FakeDebuggerService()),
            makeDefault: true);

        StubDebugSettingsHost host = new(string.Empty, autoDownloadTools: false, isBusy: false, statusText: string.Empty);
        DebugSettingsPanelViewModel viewModel = new(registry, host);

        await viewModel.DownloadNetcoredbgCommand.Execute().ToTask();

        Assert.Equal(1, host.DownloadCalls);
    }

    [Fact]
    public async Task Extension_AllowsEmptyAdapterPath_WhenAdapterIsNotResolved()
    {
        InMemorySettingsStore settings = new();
        StubDebugSettingsHost host = new("tools/netcoredbg", autoDownloadTools: true, isBusy: false, statusText: "Ready");
        DebugSettingsExtensionEntry extension = new();
        ExtensionContext context = CreateContext(settings, host);

        await extension.ActivateAsync(context, CancellationToken.None);

        await host.SetAdapterPathAsync(string.Empty, CancellationToken.None);

        Assert.Equal(string.Empty, host.AdapterPath);
    }

    private static ExtensionContext CreateContext(
        ISettings settings,
        IDebugSettingsHost debugSettingsHost)
    {
        return new ExtensionContext(
            "test.debug-settings",
            "/tmp",
            new CommandRegistry(),
            new CommandMetadataRegistry(),
            new ExtensionContributionRegistry(),
            new DebuggerServiceRegistry(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new ExtensionViewRegistry(),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            debugSettingsHost,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            settings,
            new InMemoryExtensionStorage(),
            null!,
            new List<IDisposable>());
    }

    private sealed class StubDebugSettingsHost : IDebugSettingsHost
    {
        public StubDebugSettingsHost(string adapterPath, bool autoDownloadTools, bool isBusy, string statusText)
        {
            AdapterPath = adapterPath;
            AutoDownloadTools = autoDownloadTools;
            IsBusy = isBusy;
            StatusText = statusText;
        }

        public string AdapterPath { get; private set; }

        public bool AutoDownloadTools { get; private set; }

        public bool IsBusy { get; private set; }

        public string StatusText { get; private set; }

        public int DownloadCalls { get; private set; }

        public event EventHandler<DebugSettingsChangedEventArgs>? Changed;

        public DebugSettingsState GetState()
        {
            return new DebugSettingsState(AdapterPath, AutoDownloadTools, IsBusy, StatusText);
        }

        public Task SetAdapterPathAsync(string adapterPath, CancellationToken cancellationToken)
        {
            if (string.Equals(AdapterPath, adapterPath, StringComparison.Ordinal))
            {
                return Task.CompletedTask;
            }

            AdapterPath = adapterPath;
            Changed?.Invoke(this, new DebugSettingsChangedEventArgs(GetState()));
            return Task.CompletedTask;
        }

        public Task SetAutoDownloadToolsAsync(bool autoDownloadTools, CancellationToken cancellationToken)
        {
            if (AutoDownloadTools == autoDownloadTools)
            {
                return Task.CompletedTask;
            }

            AutoDownloadTools = autoDownloadTools;
            Changed?.Invoke(this, new DebugSettingsChangedEventArgs(GetState()));
            return Task.CompletedTask;
        }

        public Task DownloadNetcoredbgAsync(CancellationToken cancellationToken)
        {
            DownloadCalls++;
            IsBusy = false;
            StatusText = "netcoredbg installed.";
            Changed?.Invoke(this, new DebugSettingsChangedEventArgs(GetState()));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDebuggerService : IDebuggerService
    {
        public Task<IDebugSession> LaunchAsync(DebugLaunchOptions options, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<IDebugSession> AttachAsync(DebugAttachOptions options, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }
    }
}
