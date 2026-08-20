using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;

namespace XamlVisualEditor.Shell.ViewModels;

public sealed class ExtensionManagerViewModel : ReactiveObject, IDisposable
{
    private readonly IExtensionManager _manager;
    private readonly Func<Task<string?>> _selectPackagePathAsync;
    private readonly CompositeDisposable _subscriptions = new();
    private readonly CompositeDisposable _itemSubscriptions = new();

    public ExtensionManagerViewModel(
        IExtensionManager manager,
        Func<Task<string?>> selectPackagePathAsync)
    {
        _manager = manager;
        _selectPackagePathAsync = selectPackagePathAsync;

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        InstallCommand = ReactiveCommand.CreateFromTask(InstallAsync);
        CheckUpdatesCommand = ReactiveCommand.CreateFromTask(CheckUpdatesAsync);

        IObservable<bool> hasSelection = this.WhenAnyValue(x => x.SelectedPackage)
            .Select(item => item is not null);
        UninstallCommand = ReactiveCommand.CreateFromTask(UninstallAsync, hasSelection);

        IDisposable selectionSubscription = this.WhenAnyValue(x => x.SelectedPackage)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasSelection)));
        _subscriptions.Add(selectionSubscription);

        // The constructor may run inside ExtensionManager's lazy built-in package
        // discovery (extensions are resolved from DI there). Scheduling the first
        // refresh keeps GetInstalledAsync from re-entering that lazy initialization,
        // and observing the task keeps failures out of the finalizer thread.
        RxSchedulers.MainThreadScheduler.Schedule(() => _ = InitialRefreshAsync());
    }

    private async Task InitialRefreshAsync()
    {
        try
        {
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Extension refresh failed: {ex.Message}";
        }
    }

    public ObservableCollection<ExtensionPackageItemViewModel> InstalledPackages { get; } = new();

    [Reactive]
    public ExtensionPackageItemViewModel? SelectedPackage { get; set; }

    [Reactive]
    public string StatusMessage { get; set; } = string.Empty;

    public bool HasSelection => SelectedPackage is not null;

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public ReactiveCommand<Unit, Unit> InstallCommand { get; }

    public ReactiveCommand<Unit, Unit> UninstallCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckUpdatesCommand { get; }

    public void Dispose()
    {
        _itemSubscriptions.Dispose();
        _subscriptions.Dispose();
    }

    private async Task RefreshAsync()
    {
        _itemSubscriptions.Clear();
        InstalledPackages.Clear();

        IReadOnlyList<ExtensionPackageInfo> packages = await _manager
            .GetInstalledAsync(CancellationToken.None)
            .ConfigureAwait(false);

        foreach (ExtensionPackageInfo package in packages)
        {
            bool enabled = await _manager.GetEnabledAsync(package.Manifest.ExtensionId, CancellationToken.None)
                .ConfigureAwait(false);

            ExtensionPackageItemViewModel item = new(package, enabled);
            item.EnabledChanged += OnItemEnabledChanged;
            InstalledPackages.Add(item);

            _itemSubscriptions.Add(Disposable.Create(() => item.EnabledChanged -= OnItemEnabledChanged));
        }

        StatusMessage = InstalledPackages.Count == 0 ? "No extensions installed." : string.Empty;
    }

    private async Task InstallAsync()
    {
        string? path = await _selectPackagePathAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Install canceled.";
            return;
        }

        try
        {
            ExtensionPackageInfo info = await _manager.InstallAsync(path, CancellationToken.None)
                .ConfigureAwait(false);
            await _manager.SetEnabledAsync(info.Manifest.ExtensionId, true, CancellationToken.None)
                .ConfigureAwait(false);
            StatusMessage = "Installed " + info.Manifest.ExtensionId;
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StatusMessage = "Install failed: " + ex.Message;
        }
    }

    private async Task UninstallAsync()
    {
        if (SelectedPackage is null)
        {
            return;
        }

        string extensionId = SelectedPackage.ExtensionId;
        try
        {
            await _manager.UninstallAsync(extensionId, CancellationToken.None).ConfigureAwait(false);
            StatusMessage = "Uninstalled " + extensionId;
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StatusMessage = "Uninstall failed: " + ex.Message;
        }
    }

    private async Task CheckUpdatesAsync()
    {
        IReadOnlyList<ExtensionUpdateInfo> updates = await _manager
            .CheckForUpdatesAsync(CancellationToken.None)
            .ConfigureAwait(false);

        Dictionary<string, ExtensionUpdateInfo> lookup = updates.ToDictionary(
            update => update.Installed.Manifest.ExtensionId,
            StringComparer.OrdinalIgnoreCase);

        foreach (ExtensionPackageItemViewModel item in InstalledPackages)
        {
            if (lookup.TryGetValue(item.ExtensionId, out ExtensionUpdateInfo? update))
            {
                item.LatestVersion = update.Available.Manifest.Version;
            }
            else
            {
                item.LatestVersion = null;
            }
        }

        StatusMessage = updates.Count == 0 ? "Extensions are up to date." : "Updates available.";
    }

    private void OnItemEnabledChanged(object? sender, bool enabled)
    {
        if (sender is not ExtensionPackageItemViewModel item)
        {
            return;
        }

        _ = UpdateEnabledStateAsync(item, enabled);
    }

    private async Task UpdateEnabledStateAsync(ExtensionPackageItemViewModel item, bool enabled)
    {
        try
        {
            await _manager.SetEnabledAsync(item.ExtensionId, enabled, CancellationToken.None)
                .ConfigureAwait(false);
            StatusMessage = enabled
                ? "Enabled " + item.ExtensionId
                : "Disabled " + item.ExtensionId;
        }
        catch (Exception ex)
        {
            item.SetEnabledSilently(!enabled);
            StatusMessage = "Enablement failed: " + ex.Message;
        }
    }
}

public sealed class ExtensionPackageItemViewModel : ReactiveObject
{
    private bool _isEnabled;
    private string? _latestVersion;
    private bool _suppressEnabledChange;

    public ExtensionPackageItemViewModel(ExtensionPackageInfo package, bool isEnabled)
    {
        Package = package;
        ExtensionId = package.Manifest.ExtensionId;
        DisplayName = string.IsNullOrWhiteSpace(package.Manifest.DisplayName)
            ? package.Manifest.Name
            : package.Manifest.DisplayName;
        Version = package.Manifest.Version;
        IsEnabled = isEnabled;
    }

    public ExtensionPackageInfo Package { get; }

    public string ExtensionId { get; }

    public string DisplayName { get; }

    public string Version { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _isEnabled, value);
            if (!_suppressEnabledChange)
            {
                EnabledChanged?.Invoke(this, value);
            }
        }
    }

    public string? LatestVersion
    {
        get => _latestVersion;
        set
        {
            this.RaiseAndSetIfChanged(ref _latestVersion, value);
            this.RaisePropertyChanged(nameof(UpdateLabel));
        }
    }

    public string UpdateLabel => string.IsNullOrWhiteSpace(LatestVersion)
        ? "Up to date"
        : "Update " + LatestVersion;

    public event EventHandler<bool>? EnabledChanged;

    public void SetEnabledSilently(bool enabled)
    {
        _suppressEnabledChange = true;
        IsEnabled = enabled;
        _suppressEnabledChange = false;
    }
}
