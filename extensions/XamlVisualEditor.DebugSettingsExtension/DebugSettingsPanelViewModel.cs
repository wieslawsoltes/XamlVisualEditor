using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Debugging;

namespace XamlVisualEditor.DebugSettingsExtension;

/// <summary>Represents a selectable debugger service in extension settings UI.</summary>
public sealed record DebuggerServiceOption(string Id, string DisplayName);

/// <summary>Extension-owned ViewModel for debug settings UI.</summary>
public sealed class DebugSettingsPanelViewModel : ReactiveObject, IDisposable
{
    private const string NetcoredbgServiceId = "debugger.netcoredbg";
    private readonly IDebuggerServiceRegistry _debuggerRegistry;
    private readonly IDebugSettingsHost _host;
    private readonly CompositeDisposable _disposables = new();
    private string _adapterPath = string.Empty;
    private bool _autoDownloadTools;
    private bool _isBusy;
    private string _statusText = string.Empty;
    private DebuggerServiceOption? _selectedDebuggerService;

    public DebugSettingsPanelViewModel(
        IDebuggerServiceRegistry debuggerRegistry,
        IDebugSettingsHost host)
    {
        _debuggerRegistry = debuggerRegistry;
        _host = host;

        IDisposable adapterPathSubscription = this.WhenAnyValue(x => x.AdapterPath)
            .Skip(1)
            .DistinctUntilChanged()
            .Subscribe(path => _ = _host.SetAdapterPathAsync(path ?? string.Empty, CancellationToken.None));
        _disposables.Add(adapterPathSubscription);

        IDisposable autoDownloadSubscription = this.WhenAnyValue(x => x.AutoDownloadTools)
            .Skip(1)
            .DistinctUntilChanged()
            .Subscribe(value => _ = _host.SetAutoDownloadToolsAsync(value, CancellationToken.None));
        _disposables.Add(autoDownloadSubscription);

        IDisposable selectedServiceSubscription = this.WhenAnyValue(x => x.SelectedDebuggerService)
            .Skip(1)
            .Select(option => option?.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .DistinctUntilChanged(StringComparer.Ordinal)
            .Subscribe(id => _debuggerRegistry.ActiveServiceId = id);
        _disposables.Add(selectedServiceSubscription);

        IObservable<bool> canDownloadNetcoredbg = this.WhenAnyValue(x => x.SelectedDebuggerService, x => x.IsBusy)
            .Select(tuple => string.Equals(tuple.Item1?.Id, NetcoredbgServiceId, StringComparison.Ordinal) && !tuple.Item2);
        DownloadNetcoredbgCommand = ReactiveCommand.CreateFromTask(
            ct => _host.DownloadNetcoredbgAsync(ct),
            canDownloadNetcoredbg);
        _disposables.Add(DownloadNetcoredbgCommand);

        _host.Changed += OnHostChanged;
        _debuggerRegistry.Changed += OnRegistryChanged;

        RefreshServices();
        ApplyHostState(_host.GetState());
    }

    public ObservableCollection<DebuggerServiceOption> DebuggerServices { get; } = new();

    public string AdapterPath
    {
        get => _adapterPath;
        set => this.RaiseAndSetIfChanged(ref _adapterPath, value);
    }

    public bool AutoDownloadTools
    {
        get => _autoDownloadTools;
        set => this.RaiseAndSetIfChanged(ref _autoDownloadTools, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public DebuggerServiceOption? SelectedDebuggerService
    {
        get => _selectedDebuggerService;
        set => this.RaiseAndSetIfChanged(ref _selectedDebuggerService, value);
    }

    public bool IsNetcoredbgSelected =>
        string.Equals(SelectedDebuggerService?.Id, NetcoredbgServiceId, StringComparison.Ordinal);

    public ReactiveCommand<Unit, Unit> DownloadNetcoredbgCommand { get; }

    public void Dispose()
    {
        _host.Changed -= OnHostChanged;
        _debuggerRegistry.Changed -= OnRegistryChanged;
        _disposables.Dispose();
    }

    private void RefreshServices()
    {
        DebuggerServices.Clear();
        foreach (DebuggerServiceRegistration service in _debuggerRegistry.Services.OrderBy(s => s.DisplayName))
        {
            DebuggerServices.Add(new DebuggerServiceOption(service.Id, service.DisplayName));
        }

        string? activeId = _debuggerRegistry.ActiveServiceId;
        DebuggerServiceOption? next = DebuggerServices.FirstOrDefault(option =>
                string.Equals(option.Id, activeId, StringComparison.Ordinal))
            ?? DebuggerServices.FirstOrDefault();
        if (!ReferenceEquals(SelectedDebuggerService, next))
        {
            SelectedDebuggerService = next;
        }

        this.RaisePropertyChanged(nameof(IsNetcoredbgSelected));
    }

    private void ApplyHostState(DebugSettingsState state)
    {
        AdapterPath = state.AdapterPath;
        AutoDownloadTools = state.AutoDownloadTools;
        IsBusy = state.IsBusy;
        StatusText = state.StatusText;
    }

    private void OnHostChanged(object? sender, DebugSettingsChangedEventArgs e)
    {
        _ = RxSchedulers.MainThreadScheduler.Schedule(Unit.Default, (_, _) =>
        {
            ApplyHostState(e.State);
            return Disposable.Empty;
        });
    }

    private void OnRegistryChanged(object? sender, EventArgs e)
    {
        _ = RxSchedulers.MainThreadScheduler.Schedule(Unit.Default, (_, _) =>
        {
            RefreshServices();
            return Disposable.Empty;
        });
    }
}
