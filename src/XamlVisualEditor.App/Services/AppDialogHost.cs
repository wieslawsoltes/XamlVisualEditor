using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.App.Services;

public sealed class AppDialogHost : IDialogHost
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Func<object?, object>> _factories = new(StringComparer.Ordinal);
    private readonly MainWindowProvider _windowProvider;

    public AppDialogHost(MainWindowProvider windowProvider)
    {
        _windowProvider = windowProvider;
    }

    public IDisposable RegisterDialog(string dialogId, Func<object?, object> factory)
    {
        if (string.IsNullOrWhiteSpace(dialogId))
        {
            throw new ArgumentException("Dialog id is required.", nameof(dialogId));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        lock (_gate)
        {
            _factories[dialogId] = factory;
        }

        return new Registration(() => Unregister(dialogId));
    }

    public async Task<T?> ShowDialogAsync<T>(string dialogId, object? viewModel, CancellationToken cancellationToken)
    {
        Func<object?, object>? factory;
        lock (_gate)
        {
            _factories.TryGetValue(dialogId, out factory);
        }

        if (factory is null)
        {
            throw new InvalidOperationException("Dialog not registered: " + dialogId);
        }

        Window? owner = _windowProvider.MainWindow;
        Task<T?>? dialogTask = null;
        Window? dialog = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            object instance = factory(viewModel);
            if (instance is not Window window)
            {
                throw new InvalidOperationException("Dialog factory must return a Window instance.");
            }

            dialog = window;
            if (owner is not null)
            {
                dialogTask = window.ShowDialog<T?>(owner);
            }
            else
            {
                window.Show();
                dialogTask = Task.FromResult<T?>(default);
            }
        });

        if (dialog is null || dialogTask is null)
        {
            return default;
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            Dispatcher.UIThread.Post(() => dialog.Close());
        });

        return await dialogTask.ConfigureAwait(false);
    }

    private void Unregister(string dialogId)
    {
        lock (_gate)
        {
            _factories.Remove(dialogId);
        }
    }

    private sealed class Registration : IDisposable
    {
        private readonly Action _dispose;
        private bool _isDisposed;

        public Registration(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _dispose();
            _isDisposed = true;
        }
    }
}
