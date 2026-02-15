using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.LspSettingsExtension;

/// <summary>Extension-owned ViewModel for LSP settings UI.</summary>
public sealed class LspSettingsPanelViewModel : ReactiveObject, IDisposable
{
    private readonly ILspSettingsHost _host;
    private readonly CompositeDisposable _disposables = new();
    private LspServerEntryViewModel? _selectedServer;
    private string _settingsPath = string.Empty;
    private string _statusText = string.Empty;
    private bool _requiresRestart;

    public ObservableCollection<LspServerEntryViewModel> Servers { get; } = new();

    public LspServerEntryViewModel? SelectedServer
    {
        get => _selectedServer;
        set => this.RaiseAndSetIfChanged(ref _selectedServer, value);
    }

    public string SettingsPath
    {
        get => _settingsPath;
        private set => this.RaiseAndSetIfChanged(ref _settingsPath, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public bool RequiresRestart
    {
        get => _requiresRestart;
        private set => this.RaiseAndSetIfChanged(ref _requiresRestart, value);
    }

    public bool IsAvailable => true;

    public ReactiveCommand<Unit, Unit> AddServerCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveServerCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public ReactiveCommand<Unit, Unit> ReloadCommand { get; }

    public LspSettingsPanelViewModel(ILspSettingsHost host)
    {
        _host = host;
        SettingsPath = host.SettingsPath;

        AddServerCommand = ReactiveCommand.Create(AddServer);
        RemoveServerCommand = ReactiveCommand.Create(RemoveSelected, this.WhenAnyValue(x => x.SelectedServer).Select(s => s is not null));
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        ReloadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
        _disposables.Add(AddServerCommand);
        _disposables.Add(RemoveServerCommand);
        _disposables.Add(SaveCommand);
        _disposables.Add(ReloadCommand);

        _host.Changed += OnHostChanged;
        _ = LoadAsync();
    }

    public void Dispose()
    {
        _host.Changed -= OnHostChanged;
        _disposables.Dispose();
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
        IReadOnlyList<LspServerSettings> servers = await _host.LoadServersAsync(CancellationToken.None).ConfigureAwait(false);

        _ = RxApp.MainThreadScheduler.Schedule(Unit.Default, (_, _) =>
        {
            Servers.Clear();
            foreach (LspServerSettings server in servers)
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
        IReadOnlyList<LspServerSettings> servers = Servers
            .Select(entry => entry.ToServerSettings())
            .ToList();

        await _host.SaveServersAsync(servers, CancellationToken.None).ConfigureAwait(false);

        _ = RxApp.MainThreadScheduler.Schedule(Unit.Default, (_, _) =>
        {
            StatusText = "LSP settings saved. Restart required.";
            RequiresRestart = true;
            return Disposable.Empty;
        });
    }

    private void OnHostChanged(object? sender, LspSettingsChangedEventArgs e)
    {
        _ = RxApp.MainThreadScheduler.Schedule(Unit.Default, (_, _) =>
        {
            Servers.Clear();
            foreach (LspServerSettings server in e.Servers)
            {
                Servers.Add(LspServerEntryViewModel.FromServer(server));
            }

            StatusText = "LSP settings updated.";
            return Disposable.Empty;
        });
    }
}

public sealed class LspServerEntryViewModel : ReactiveObject
{
    private string _languageId = string.Empty;
    private string _serverPath = string.Empty;
    private string _arguments = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _fileExtensions = string.Empty;

    public string LanguageId
    {
        get => _languageId;
        set => this.RaiseAndSetIfChanged(ref _languageId, value);
    }

    public string ServerPath
    {
        get => _serverPath;
        set => this.RaiseAndSetIfChanged(ref _serverPath, value);
    }

    public string Arguments
    {
        get => _arguments;
        set => this.RaiseAndSetIfChanged(ref _arguments, value);
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => this.RaiseAndSetIfChanged(ref _workingDirectory, value);
    }

    public string FileExtensions
    {
        get => _fileExtensions;
        set => this.RaiseAndSetIfChanged(ref _fileExtensions, value);
    }

    public static LspServerEntryViewModel FromServer(LspServerSettings server)
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

    public LspServerSettings ToServerSettings()
    {
        return new LspServerSettings
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
