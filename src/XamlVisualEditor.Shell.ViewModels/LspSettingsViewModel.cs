using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Lsp;

namespace XamlVisualEditor.Shell.ViewModels;

public sealed class LspSettingsViewModel : ReactiveObject
{
    private readonly ILspSettingsStore? _store;

    public ObservableCollection<LspServerEntryViewModel> Servers { get; } = new();

    [Reactive]
    public LspServerEntryViewModel? SelectedServer { get; set; }

    [Reactive]
    public string SettingsPath { get; private set; } = string.Empty;

    [Reactive]
    public string StatusText { get; private set; } = string.Empty;

    [Reactive]
    public bool RequiresRestart { get; private set; }

    public bool IsAvailable => _store is not null;

    public ReactiveCommand<Unit, Unit> AddServerCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveServerCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public ReactiveCommand<Unit, Unit> ReloadCommand { get; }

    public LspSettingsViewModel(ILspSettingsStore? store)
    {
        _store = store;
        SettingsPath = store?.SettingsPath ?? "";

        AddServerCommand = ReactiveCommand.Create(AddServer, this.WhenAnyValue(x => x.IsAvailable));
        RemoveServerCommand = ReactiveCommand.Create(RemoveSelected, this.WhenAnyValue(x => x.SelectedServer).Select(s => s is not null));
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, this.WhenAnyValue(x => x.IsAvailable));
        ReloadCommand = ReactiveCommand.CreateFromTask(LoadAsync, this.WhenAnyValue(x => x.IsAvailable));

        if (_store is not null)
        {
            _ = LoadAsync();
        }
        else
        {
            StatusText = "LSP settings store unavailable.";
        }
    }

    private void AddServer()
    {
        LspServerEntryViewModel entry = new()
        {
            LanguageId = "csharp",
            FileExtensions = ".cs"
        };

        Servers.Add(entry);
        SelectedServer = entry;
        RequiresRestart = true;
    }

    private void RemoveSelected()
    {
        if (SelectedServer is null)
        {
            return;
        }

        Servers.Remove(SelectedServer);
        SelectedServer = null;
        RequiresRestart = true;
    }

    private async Task LoadAsync()
    {
        if (_store is null)
        {
            return;
        }

        IReadOnlyList<LspServerConfiguration> servers = await _store.LoadAsync().ConfigureAwait(false);

        RxApp.MainThreadScheduler.Schedule(Unit.Default, (_, _) =>
        {
            Servers.Clear();
            foreach (LspServerConfiguration server in servers)
            {
                Servers.Add(LspServerEntryViewModel.FromServer(server));
            }

            StatusText = servers.Count == 0 ? "No LSP servers configured." : "LSP settings loaded.";
            RequiresRestart = false;
            return Disposable.Empty;
        });
    }

    private async Task SaveAsync()
    {
        if (_store is null)
        {
            return;
        }

        IReadOnlyList<LspServerConfiguration> servers = Servers
            .Select(entry => entry.ToServerConfiguration())
            .ToList();

        await _store.SaveAsync(servers).ConfigureAwait(false);

        RxApp.MainThreadScheduler.Schedule(Unit.Default, (_, _) =>
        {
            StatusText = "LSP settings saved. Restart required.";
            RequiresRestart = true;
            return Disposable.Empty;
        });
    }
}

public sealed class LspServerEntryViewModel : ReactiveObject
{
    [Reactive]
    public string LanguageId { get; set; } = "";

    [Reactive]
    public string ServerPath { get; set; } = "";

    [Reactive]
    public string Arguments { get; set; } = "";

    [Reactive]
    public string WorkingDirectory { get; set; } = "";

    [Reactive]
    public string FileExtensions { get; set; } = "";

    public static LspServerEntryViewModel FromServer(LspServerConfiguration server)
    {
        return new LspServerEntryViewModel
        {
            LanguageId = server.LanguageId,
            ServerPath = server.ServerPath,
            Arguments = string.Join(";", server.Arguments),
            WorkingDirectory = server.WorkingDirectory ?? string.Empty,
            FileExtensions = string.Join(";", server.FileExtensions)
        };
    }

    public LspServerConfiguration ToServerConfiguration()
    {
        return new LspServerConfiguration
        {
            LanguageId = LanguageId.Trim(),
            ServerPath = ServerPath.Trim(),
            Arguments = SplitList(Arguments),
            WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory.Trim(),
            FileExtensions = NormalizeExtensions(SplitList(FileExtensions))
        };
    }

    private static IReadOnlyList<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<string> NormalizeExtensions(IReadOnlyList<string> extensions)
    {
        List<string> normalized = new();
        foreach (string extension in extensions)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                continue;
            }

            string value = extension.StartsWith(".", StringComparison.Ordinal)
                ? extension
                : "." + extension;

            normalized.Add(value);
        }

        return normalized;
    }
}
