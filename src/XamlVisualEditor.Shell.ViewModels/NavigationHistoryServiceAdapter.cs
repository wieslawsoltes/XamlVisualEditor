using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Adapts shell navigation history to extension services.</summary>
public sealed class NavigationHistoryServiceAdapter : INavigationHistoryService, IDisposable
{
    private readonly MainWindowViewModel _mainViewModel;
    private readonly CompositeDisposable _disposables = new();

    public NavigationHistoryServiceAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;

        IDisposable subscription = _mainViewModel.WhenAnyValue(
                vm => vm.CanNavigateBack,
                vm => vm.CanNavigateForward)
            .Subscribe(values =>
            {
                HistoryChanged?.Invoke(this, new NavigationHistoryChangedEventArgs(values.Item1, values.Item2));
            });
        _disposables.Add(subscription);
    }

    public bool CanNavigateBack => _mainViewModel.CanNavigateBack;

    public bool CanNavigateForward => _mainViewModel.CanNavigateForward;

    public event EventHandler<NavigationHistoryChangedEventArgs>? HistoryChanged;

    public async Task<bool> NavigateBackAsync(CancellationToken ct)
    {
        if (!CanNavigateBack)
        {
            return false;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            await _mainViewModel.NavigateBackFromHistoryAsync(ct).ConfigureAwait(false);
            return true;
        }

        bool result = false;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await _mainViewModel.NavigateBackFromHistoryAsync(ct).ConfigureAwait(false);
            result = true;
        }, DispatcherPriority.Background, ct);
        return result;
    }

    public async Task<bool> NavigateForwardAsync(CancellationToken ct)
    {
        if (!CanNavigateForward)
        {
            return false;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            await _mainViewModel.NavigateForwardFromHistoryAsync(ct).ConfigureAwait(false);
            return true;
        }

        bool result = false;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await _mainViewModel.NavigateForwardFromHistoryAsync(ct).ConfigureAwait(false);
            result = true;
        }, DispatcherPriority.Background, ct);
        return result;
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
