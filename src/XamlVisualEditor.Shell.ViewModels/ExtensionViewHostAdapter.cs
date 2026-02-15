using System;
using Avalonia.Threading;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

public sealed class ExtensionViewHostAdapter : IExtensionViewHost, IDisposable
{
    private readonly MainWindowViewModel _mainViewModel;

    public ExtensionViewHostAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _mainViewModel.ExtensionViewVisibilityChanged += OnExtensionViewVisibilityChanged;
        _mainViewModel.ExtensionViewFocusChanged += OnExtensionViewFocusChanged;
    }

    public event EventHandler<ExtensionViewVisibilityChangedEventArgs>? VisibilityChanged;

    public event EventHandler<ExtensionViewFocusChangedEventArgs>? FocusChanged;

    public async Task ShowAsync(string viewId, CancellationToken cancellationToken)
    {
        await InvokeOnUiThreadAsync(() => _mainViewModel.ShowExtensionView(viewId), cancellationToken);
    }

    public async Task ToggleAsync(string viewId, CancellationToken cancellationToken)
    {
        await InvokeOnUiThreadAsync(() => _mainViewModel.ToggleExtensionView(viewId), cancellationToken);
    }

    public async Task<bool> IsVisibleAsync(string viewId, CancellationToken cancellationToken)
    {
        return await InvokeOnUiThreadAsync(() => _mainViewModel.IsExtensionViewVisible(viewId), cancellationToken);
    }

    public async Task ActivateAsync(string viewId, CancellationToken cancellationToken)
    {
        await InvokeOnUiThreadAsync(() => _mainViewModel.ActivateExtensionView(viewId), cancellationToken);
    }

    public void Dispose()
    {
        _mainViewModel.ExtensionViewVisibilityChanged -= OnExtensionViewVisibilityChanged;
        _mainViewModel.ExtensionViewFocusChanged -= OnExtensionViewFocusChanged;
    }

    private void OnExtensionViewVisibilityChanged(object? sender, ExtensionViewVisibilityChangedEventArgs e)
    {
        VisibilityChanged?.Invoke(this, e);
    }

    private void OnExtensionViewFocusChanged(object? sender, ExtensionViewFocusChangedEventArgs e)
    {
        FocusChanged?.Invoke(this, e);
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
