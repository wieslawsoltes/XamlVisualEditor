using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Shell-backed debug settings host adapter.</summary>
public sealed class DebugSettingsHostAdapter : IDebugSettingsHost, IDisposable
{
    private readonly MainWindowViewModel _mainViewModel;
    private readonly CompositeDisposable _disposables = new();

    public DebugSettingsHostAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;

        IDisposable stateSubscription = _mainViewModel.WhenAnyValue(
                vm => vm.DebuggerAdapterPath,
                vm => vm.AutoDownloadTools,
                vm => vm.DebugSettings.IsBusy,
                vm => vm.DebugSettings.StatusText)
            .Subscribe(_ => PublishChanged());
        _disposables.Add(stateSubscription);
    }

    public event EventHandler<DebugSettingsChangedEventArgs>? Changed;

    public DebugSettingsState GetState()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread
                .InvokeAsync(CreateStateSnapshot, DispatcherPriority.Background)
                .GetTask()
                .GetAwaiter()
                .GetResult();
        }

        return CreateStateSnapshot();
    }

    public async Task SetAdapterPathAsync(string adapterPath, CancellationToken cancellationToken)
    {
        await InvokeOnUiThreadAsync(
            () => _mainViewModel.DebuggerAdapterPath = adapterPath,
            cancellationToken);
    }

    public async Task SetAutoDownloadToolsAsync(bool autoDownloadTools, CancellationToken cancellationToken)
    {
        await InvokeOnUiThreadAsync(
            () => _mainViewModel.AutoDownloadTools = autoDownloadTools,
            cancellationToken);
    }

    public async Task DownloadNetcoredbgAsync(CancellationToken cancellationToken)
    {
        Task downloadTask = await InvokeOnUiThreadAsync(
            () => _mainViewModel.DebugSettings.DownloadNetcoredbgCommand
                .Execute()
                .ToTask(cancellationToken),
            cancellationToken);
        await downloadTask.ConfigureAwait(false);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    private DebugSettingsState CreateStateSnapshot()
    {
        return new DebugSettingsState(
            _mainViewModel.DebuggerAdapterPath,
            _mainViewModel.AutoDownloadTools,
            _mainViewModel.DebugSettings.IsBusy,
            _mainViewModel.DebugSettings.StatusText);
    }

    private void PublishChanged()
    {
        Changed?.Invoke(this, new DebugSettingsChangedEventArgs(GetState()));
    }

    private static async Task InvokeOnUiThreadAsync(Action callback, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Dispatcher.UIThread.CheckAccess())
        {
            callback();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(callback, DispatcherPriority.Background, cancellationToken);
    }

    private static async Task<T> InvokeOnUiThreadAsync<T>(Func<T> callback, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Dispatcher.UIThread.CheckAccess())
        {
            return callback();
        }

        return await Dispatcher.UIThread.InvokeAsync(callback, DispatcherPriority.Background, cancellationToken);
    }
}
