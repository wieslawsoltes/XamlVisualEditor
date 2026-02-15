using System.Reactive.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.LspSettingsExtension;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class LspSettingsPanelViewModelTests
{
    [Fact]
    public async Task ReloadCommand_LoadsServersFromHost()
    {
        StubLspSettingsHost host = new();
        host.SetServers(new[]
        {
            new LspServerSettings
            {
                LanguageId = "xaml",
                ServerPath = "/tools/xaml-lsp",
                Arguments = new[] { "--stdio" },
                FileExtensions = new[] { ".axaml" }
            }
        });

        LspSettingsPanelViewModel viewModel = new(host);
        await viewModel.ReloadCommand.Execute().ToTask();

        Assert.Single(viewModel.Servers);
        Assert.Equal("xaml", viewModel.Servers[0].LanguageId);
        Assert.Equal("/tools/xaml-lsp", viewModel.Servers[0].ServerPath);
    }

    [Fact]
    public async Task SaveCommand_PersistsEditedServers()
    {
        StubLspSettingsHost host = new();
        LspSettingsPanelViewModel viewModel = new(host);

        viewModel.Servers.Clear();
        viewModel.Servers.Add(new LspServerEntryViewModel
        {
            LanguageId = "csharp",
            ServerPath = "/tools/csharp-lsp",
            Arguments = "--stdio",
            WorkingDirectory = "/workspace",
            FileExtensions = "cs;.csx"
        });

        await viewModel.SaveCommand.Execute().ToTask();

        Assert.NotNull(host.LastSaved);
        Assert.Single(host.LastSaved!);
        Assert.Equal("csharp", host.LastSaved[0].LanguageId);
        Assert.Equal(".cs", host.LastSaved[0].FileExtensions[0]);
        Assert.Equal(".csx", host.LastSaved[0].FileExtensions[1]);
    }

    private sealed class StubLspSettingsHost : ILspSettingsHost
    {
        private IReadOnlyList<LspServerSettings> _servers = Array.Empty<LspServerSettings>();

        public string SettingsPath => "/tmp/lsp-servers.json";

        public IReadOnlyList<LspServerSettings>? LastSaved { get; private set; }

        public event EventHandler<LspSettingsChangedEventArgs>? Changed;

        public Task<IReadOnlyList<LspServerSettings>> LoadServersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_servers);
        }

        public Task SaveServersAsync(IReadOnlyList<LspServerSettings> servers, CancellationToken cancellationToken)
        {
            LastSaved = servers;
            _servers = servers;
            Changed?.Invoke(this, new LspSettingsChangedEventArgs(servers));
            return Task.CompletedTask;
        }

        public void SetServers(IReadOnlyList<LspServerSettings> servers)
        {
            _servers = servers;
        }
    }
}
