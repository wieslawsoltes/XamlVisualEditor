using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using XamlVisualEditor.App.Views;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Services;

public sealed class AppWindow : IWindow
{
    private readonly MainWindowProvider _windowProvider;
    private readonly Dictionary<string, AppOutputChannel> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AppStatusBarItem> _statusBarItems = new(StringComparer.Ordinal);

    public AppWindow(MainWindowProvider windowProvider)
    {
        _windowProvider = windowProvider;
    }

    public Task ShowInformationMessageAsync(string message, CancellationToken cancellationToken)
    {
        return ShowMessageAsync("Information", message, cancellationToken);
    }

    public Task ShowWarningMessageAsync(string message, CancellationToken cancellationToken)
    {
        return ShowMessageAsync("Warning", message, cancellationToken);
    }

    public Task ShowErrorMessageAsync(string message, CancellationToken cancellationToken)
    {
        return ShowMessageAsync("Error", message, cancellationToken);
    }

    public Task<string?> ShowInputBoxAsync(InputBoxOptions options, CancellationToken cancellationToken)
    {
        InputBoxDialogViewModel viewModel = new(options);
        InputBoxDialog dialog = new()
        {
            DataContext = viewModel
        };

        return ShowDialogAsync<string?>(dialog, cancellationToken);
    }

    public Task<QuickPickItem?> ShowQuickPickAsync(
        IReadOnlyList<QuickPickItem> items,
        QuickPickOptions options,
        CancellationToken cancellationToken)
    {
        QuickPickDialogViewModel viewModel = new(options.Title, items);
        QuickPickDialog dialog = new()
        {
            DataContext = viewModel
        };

        return ShowDialogAsync<QuickPickItem?>(dialog, cancellationToken);
    }

    public IOutputChannel CreateOutputChannel(string name)
    {
        lock (_channels)
        {
            if (_channels.TryGetValue(name, out AppOutputChannel? existing))
            {
                return existing;
            }

            OutputChannelInfo info = new(name);
            AppOutputChannel channel = new(
                info,
                message => OutputChannelMessage?.Invoke(this, message),
                cleared => OutputChannelCleared?.Invoke(this, cleared),
                () => RemoveChannel(info));

            _channels[name] = channel;
            OutputChannelCreated?.Invoke(this, new OutputChannelEventArgs(info));
            return channel;
        }
    }

    public Task<IReadOnlyList<OutputChannelInfo>> GetOutputChannelsAsync(CancellationToken cancellationToken)
    {
        lock (_channels)
        {
            List<OutputChannelInfo> results = new(_channels.Count);
            foreach (AppOutputChannel channel in _channels.Values)
            {
                results.Add(channel.Info);
            }

            return Task.FromResult<IReadOnlyList<OutputChannelInfo>>(results);
        }
    }

    public event EventHandler<OutputChannelEventArgs>? OutputChannelCreated;

    public event EventHandler<OutputChannelEventArgs>? OutputChannelRemoved;

    public event EventHandler<OutputChannelMessageEventArgs>? OutputChannelMessage;

    public event EventHandler<OutputChannelClearedEventArgs>? OutputChannelCleared;

    public IStatusBarItem CreateStatusBarItem(StatusBarAlignment alignment, int priority)
    {
        AppStatusBarItem item = new(
            Guid.NewGuid().ToString("N"),
            alignment,
            priority,
            PublishStatusBarState,
            RemoveStatusBarItem);

        lock (_statusBarItems)
        {
            _statusBarItems[item.ItemId] = item;
        }

        PublishStatusBarState(item.GetState());
        return item;
    }

    public void SyncStatusBarItems()
    {
        List<AppStatusBarState> states;
        lock (_statusBarItems)
        {
            states = _statusBarItems.Values.Select(item => item.GetState()).ToList();
        }

        foreach (AppStatusBarState state in states)
        {
            PublishStatusBarState(state);
        }
    }

    private Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken)
    {
        MessageDialogViewModel viewModel = new(title, message);
        MessageDialog dialog = new()
        {
            DataContext = viewModel
        };

        return ShowDialogAsync<bool>(dialog, cancellationToken);
    }

    private async Task<T?> ShowDialogAsync<T>(Window dialog, CancellationToken cancellationToken)
    {
        Window? owner = _windowProvider.MainWindow;
        Task<T?>? dialogTask = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (owner is not null)
            {
                dialogTask = dialog.ShowDialog<T?>(owner);
            }
            else
            {
                dialog.Show();
                dialogTask = Task.FromResult<T?>(default);
            }
        });

        if (dialogTask is null)
        {
            return default;
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            Dispatcher.UIThread.Post(() => dialog.Close());
        });

        return await dialogTask.ConfigureAwait(false);
    }

    private void RemoveChannel(OutputChannelInfo info)
    {
        lock (_channels)
        {
            _channels.Remove(info.Name);
        }

        OutputChannelRemoved?.Invoke(this, new OutputChannelEventArgs(info));
    }

    private void PublishStatusBarState(AppStatusBarState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (TryGetMainWindowViewModel(out MainWindowViewModel? viewModel))
            {
                viewModel!.UpsertStatusBarItem(
                    state.ItemId,
                    state.Text,
                    state.Tooltip,
                    state.CommandId,
                    state.Alignment,
                    state.Priority,
                    state.IsVisible);
            }
        });
    }

    private void RemoveStatusBarItem(string itemId)
    {
        lock (_statusBarItems)
        {
            _statusBarItems.Remove(itemId);
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (TryGetMainWindowViewModel(out MainWindowViewModel? viewModel))
            {
                viewModel!.RemoveStatusBarItem(itemId);
            }
        });
    }

    private bool TryGetMainWindowViewModel(out MainWindowViewModel? viewModel)
    {
        viewModel = _windowProvider.MainWindow?.DataContext as MainWindowViewModel;
        return viewModel is not null;
    }

    private sealed class AppOutputChannel : IOutputChannel
    {
        private readonly Action<OutputChannelMessageEventArgs>? _messageCallback;
        private readonly Action<OutputChannelClearedEventArgs>? _clearedCallback;
        private readonly Action? _disposedCallback;
        private readonly List<string> _lines = new();

        public AppOutputChannel(
            OutputChannelInfo info,
            Action<OutputChannelMessageEventArgs>? messageCallback,
            Action<OutputChannelClearedEventArgs>? clearedCallback,
            Action? disposedCallback)
        {
            Info = info;
            _messageCallback = messageCallback;
            _clearedCallback = clearedCallback;
            _disposedCallback = disposedCallback;
        }

        public OutputChannelInfo Info { get; }

        public string Name => Info.Name;

        public void Append(string value)
        {
            if (_lines.Count == 0)
            {
                _lines.Add(value);
            }
            else
            {
                int last = _lines.Count - 1;
                _lines[last] = _lines[last] + value;
            }

            _messageCallback?.Invoke(new OutputChannelMessageEventArgs(Info, value, false));
        }

        public void AppendLine(string value)
        {
            _lines.Add(value);
            _messageCallback?.Invoke(new OutputChannelMessageEventArgs(Info, value, true));
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void Clear()
        {
            _lines.Clear();
            _clearedCallback?.Invoke(new OutputChannelClearedEventArgs(Info));
        }

        public void Dispose()
        {
            _disposedCallback?.Invoke();
        }
    }

    private sealed class AppStatusBarItem : IStatusBarItem
    {
        private readonly object _gate = new();
        private readonly Action<AppStatusBarState> _stateCallback;
        private readonly Action<string> _disposedCallback;
        private bool _isDisposed;
        private bool _isVisible;
        private string _text = string.Empty;
        private string? _tooltip;
        private string? _commandId;

        public AppStatusBarItem(
            string itemId,
            StatusBarAlignment alignment,
            int priority,
            Action<AppStatusBarState> stateCallback,
            Action<string> disposedCallback)
        {
            ItemId = itemId;
            Alignment = alignment;
            Priority = priority;
            _stateCallback = stateCallback;
            _disposedCallback = disposedCallback;
        }

        public string ItemId { get; }

        public StatusBarAlignment Alignment { get; }

        public int Priority { get; }

        public string Text
        {
            get
            {
                lock (_gate)
                {
                    return _text;
                }
            }
            set
            {
                lock (_gate)
                {
                    _text = value ?? string.Empty;
                }

                PublishState();
            }
        }

        public string? Tooltip
        {
            get
            {
                lock (_gate)
                {
                    return _tooltip;
                }
            }
            set
            {
                lock (_gate)
                {
                    _tooltip = value;
                }

                PublishState();
            }
        }

        public string? CommandId
        {
            get
            {
                lock (_gate)
                {
                    return _commandId;
                }
            }
            set
            {
                lock (_gate)
                {
                    _commandId = value;
                }

                PublishState();
            }
        }

        public void Show()
        {
            lock (_gate)
            {
                _isVisible = true;
            }

            PublishState();
        }

        public void Hide()
        {
            lock (_gate)
            {
                _isVisible = false;
            }

            PublishState();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _isVisible = false;
            }

            _disposedCallback(ItemId);
        }

        public AppStatusBarState GetState()
        {
            lock (_gate)
            {
                return new AppStatusBarState(
                    ItemId,
                    _text,
                    _tooltip,
                    _commandId,
                    Alignment,
                    Priority,
                    _isVisible);
            }
        }

        private void PublishState()
        {
            if (_isDisposed)
            {
                return;
            }

            _stateCallback(GetState());
        }
    }

    private readonly record struct AppStatusBarState(
        string ItemId,
        string Text,
        string? Tooltip,
        string? CommandId,
        StatusBarAlignment Alignment,
        int Priority,
        bool IsVisible);
}
