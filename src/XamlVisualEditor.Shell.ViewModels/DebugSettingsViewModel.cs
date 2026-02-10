using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Core.Debugging;

namespace XamlVisualEditor.Shell.ViewModels;

public sealed class DebugSettingsViewModel : ReactiveObject
{
    private readonly IDebugToolInstaller? _installer;
    private readonly Func<string> _getAdapterPath;
    private readonly Action<string> _setAdapterPath;
    private readonly Func<bool> _getAutoDownload;
    private readonly Action<bool> _setAutoDownload;
    private readonly Func<DebugToolConsentRequest, Task<bool>> _confirmAsync;

    [Reactive]
    public string AdapterPath { get; set; }

    [Reactive]
    public bool AutoDownloadTools { get; set; }

    [Reactive]
    public bool IsBusy { get; private set; }

    [Reactive]
    public string StatusText { get; private set; } = "";

    public ReactiveCommand<Unit, Unit> DownloadNetcoredbgCommand { get; }

    public DebugSettingsViewModel(
        IDebugToolInstaller? installer,
        Func<string> getAdapterPath,
        Action<string> setAdapterPath,
        Func<bool> getAutoDownload,
        Action<bool> setAutoDownload,
        Func<DebugToolConsentRequest, Task<bool>> confirmAsync)
    {
        _installer = installer;
        _getAdapterPath = getAdapterPath;
        _setAdapterPath = setAdapterPath;
        _getAutoDownload = getAutoDownload;
        _setAutoDownload = setAutoDownload;
        _confirmAsync = confirmAsync;

        AdapterPath = _getAdapterPath();
        AutoDownloadTools = _getAutoDownload();

        this.WhenAnyValue(x => x.AdapterPath)
            .Skip(1)
            .Subscribe(path => _setAdapterPath(path));

        this.WhenAnyValue(x => x.AutoDownloadTools)
            .Skip(1)
            .Subscribe(value => _setAutoDownload(value));

        DownloadNetcoredbgCommand = ReactiveCommand.CreateFromTask(DownloadNetcoredbgAsync);
    }

    private async Task DownloadNetcoredbgAsync()
    {
        if (_installer is null)
        {
            StatusText = "Debug tool installer unavailable.";
            return;
        }

        IsBusy = true;
        StatusText = "Downloading netcoredbg...";
        try
        {
            string? path = await _installer.EnsureNetcoredbgAsync(_confirmAsync).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(path))
            {
                AdapterPath = path;
                StatusText = "netcoredbg installed.";
            }
            else
            {
                StatusText = "netcoredbg download cancelled.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
