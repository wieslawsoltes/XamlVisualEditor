using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Threading;
using ReactiveUI;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.OutputExtension;

public sealed class OutputMessageEntry
{
    public OutputMessageEntry(DateTimeOffset timestamp, string message, bool isLine)
    {
        Timestamp = timestamp;
        Message = message;
        IsLine = isLine;
    }

    public DateTimeOffset Timestamp { get; }
    public string Message { get; }
    public bool IsLine { get; }
}

public sealed class OutputChannelViewModel : ReactiveObject
{
    private readonly ObservableCollection<OutputMessageEntry> _messages = new();

    public OutputChannelViewModel(string name)
    {
        Name = name;
        MessagesView = new DataGridCollectionView(_messages);
    }

    public string Name { get; }

    public ObservableCollection<OutputMessageEntry> Messages => _messages;

    public DataGridCollectionView MessagesView { get; }
}

public sealed class OutputPanelViewModel : ReactiveObject, IDisposable
{
    private readonly IWindow _window;
    private readonly CompositeDisposable _disposables = new();
    private readonly ObservableCollection<OutputChannelViewModel> _channels = new();
    private readonly ObservableCollection<OutputMessageEntry> _displayMessages = new();
    private OutputChannelViewModel? _selectedChannel;
    private OutputMessageEntry? _selectedMessage;
    private string? _channelSearchText;
    private string? _messageSearchText;

    public OutputPanelViewModel(IWindow window)
    {
        _window = window;
        ChannelsView = new DataGridCollectionView(_channels)
        {
            Filter = FilterChannel
        };
        MessagesView = new DataGridCollectionView(_displayMessages)
        {
            Filter = FilterMessage
        };

        _disposables.Add(this.WhenAnyValue(x => x.SelectedChannel)
            .Subscribe(_ => UpdateDisplayMessages()));

        IObservable<bool> hasSelectedChannel = this.WhenAnyValue(x => x.SelectedChannel)
            .Select(channel => channel is not null);
        ClearChannelCommand = ReactiveCommand.CreateFromTask(ClearSelectedChannelAsync, hasSelectedChannel);

        IObservable<bool> hasChannels = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => _channels.CollectionChanged += h,
                h => _channels.CollectionChanged -= h)
            .Select(_ => _channels.Count > 0)
            .StartWith(_channels.Count > 0);
        ClearAllChannelsCommand = ReactiveCommand.CreateFromTask(ClearAllChannelsAsync, hasChannels);

        IObservable<bool> hasSelectedMessage = this.WhenAnyValue(x => x.SelectedMessage)
            .Select(message => message is not null);
        CopySelectedCommand = ReactiveCommand.CreateFromTask(CopySelectedAsync, hasSelectedMessage);

        IObservable<bool> hasMessages = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => _displayMessages.CollectionChanged += h,
                h => _displayMessages.CollectionChanged -= h)
            .Select(_ => _displayMessages.Count > 0)
            .StartWith(_displayMessages.Count > 0);
        CopyAllCommand = ReactiveCommand.CreateFromTask(CopyAllAsync, hasMessages);

        ClearFiltersCommand = ReactiveCommand.Create(() =>
        {
            ChannelSearchText = null;
            MessageSearchText = null;
        });

        _disposables.Add(this.WhenAnyValue(x => x.ChannelSearchText)
            .Subscribe(_ => ChannelsView.Refresh()));
        _disposables.Add(this.WhenAnyValue(x => x.MessageSearchText)
            .Subscribe(_ => MessagesView.Refresh()));
    }

    public DataGridCollectionView ChannelsView { get; }

    public DataGridCollectionView MessagesView { get; }

    public Interaction<string, Unit> CopyToClipboardInteraction { get; } = new();

    public ReactiveCommand<Unit, Unit> ClearChannelCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearAllChannelsCommand { get; }

    public ReactiveCommand<Unit, Unit> CopySelectedCommand { get; }

    public ReactiveCommand<Unit, Unit> CopyAllCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }

    public OutputChannelViewModel? SelectedChannel
    {
        get => _selectedChannel;
        set => this.RaiseAndSetIfChanged(ref _selectedChannel, value);
    }

    public OutputMessageEntry? SelectedMessage
    {
        get => _selectedMessage;
        set => this.RaiseAndSetIfChanged(ref _selectedMessage, value);
    }

    public string? ChannelSearchText
    {
        get => _channelSearchText;
        set => this.RaiseAndSetIfChanged(ref _channelSearchText, value);
    }

    public string? MessageSearchText
    {
        get => _messageSearchText;
        set => this.RaiseAndSetIfChanged(ref _messageSearchText, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<OutputChannelInfo> channels = await _window.GetOutputChannelsAsync(cancellationToken);
        foreach (OutputChannelInfo channel in channels)
        {
            AddChannel(channel);
        }

        if (_channels.Count > 0 && SelectedChannel is null)
        {
            SelectedChannel = _channels[0];
        }
    }

    public void HandleChannelCreated(OutputChannelInfo channel)
    {
        Dispatcher.UIThread.Post(() => AddChannel(channel));
    }

    public void HandleChannelRemoved(OutputChannelInfo channel)
    {
        Dispatcher.UIThread.Post(() => RemoveChannel(channel));
    }

    public void HandleChannelCleared(OutputChannelInfo channel)
    {
        Dispatcher.UIThread.Post(() => ClearChannel(channel));
    }

    public void HandleChannelMessage(OutputChannelInfo channel, string message, bool isLine)
    {
        Dispatcher.UIThread.Post(() => AppendMessage(channel, message, isLine));
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    private Task ClearSelectedChannelAsync()
    {
        OutputChannelViewModel? channel = SelectedChannel;
        if (channel is null)
        {
            return Task.CompletedTask;
        }

        _window.CreateOutputChannel(channel.Name).Clear();
        return Task.CompletedTask;
    }

    private Task ClearAllChannelsAsync()
    {
        foreach (OutputChannelViewModel channel in _channels)
        {
            _window.CreateOutputChannel(channel.Name).Clear();
        }

        return Task.CompletedTask;
    }

    private async Task CopySelectedAsync()
    {
        if (SelectedMessage is null)
        {
            return;
        }

        await CopyToClipboardInteraction.Handle(FormatMessage(SelectedMessage)).ToTask().ConfigureAwait(false);
    }

    private async Task CopyAllAsync()
    {
        if (_displayMessages.Count == 0)
        {
            return;
        }

        StringBuilder builder = new();
        foreach (object item in MessagesView)
        {
            if (item is OutputMessageEntry entry)
            {
                builder.AppendLine(FormatMessage(entry));
            }
        }

        await CopyToClipboardInteraction.Handle(builder.ToString()).ToTask().ConfigureAwait(false);
    }

    private void AddChannel(OutputChannelInfo channel)
    {
        if (FindChannel(channel.Name) is not null)
        {
            return;
        }

        OutputChannelViewModel viewModel = new(channel.Name);
        _channels.Add(viewModel);

        if (SelectedChannel is null)
        {
            SelectedChannel = viewModel;
        }
    }

    private void RemoveChannel(OutputChannelInfo channel)
    {
        OutputChannelViewModel? existing = FindChannel(channel.Name);
        if (existing is null)
        {
            return;
        }

        _channels.Remove(existing);
        if (ReferenceEquals(SelectedChannel, existing))
        {
            SelectedChannel = _channels.Count > 0 ? _channels[0] : null;
        }
    }

    private void ClearChannel(OutputChannelInfo channel)
    {
        OutputChannelViewModel? existing = FindChannel(channel.Name);
        if (existing is null)
        {
            return;
        }

        existing.Messages.Clear();
        if (ReferenceEquals(SelectedChannel, existing))
        {
            _displayMessages.Clear();
        }
    }

    private void AppendMessage(OutputChannelInfo channel, string message, bool isLine)
    {
        OutputChannelViewModel? existing = FindChannel(channel.Name);
        if (existing is null)
        {
            existing = new OutputChannelViewModel(channel.Name);
            _channels.Add(existing);
        }

        OutputMessageEntry entry = new(DateTimeOffset.Now, message, isLine);
        existing.Messages.Add(entry);

        if (ReferenceEquals(SelectedChannel, existing))
        {
            _displayMessages.Add(entry);
        }
    }

    private void UpdateDisplayMessages()
    {
        _displayMessages.Clear();
        SelectedMessage = null;
        if (SelectedChannel is null)
        {
            return;
        }

        foreach (OutputMessageEntry entry in SelectedChannel.Messages)
        {
            _displayMessages.Add(entry);
        }
    }

    private OutputChannelViewModel? FindChannel(string name)
    {
        foreach (OutputChannelViewModel channel in _channels)
        {
            if (string.Equals(channel.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return channel;
            }
        }

        return null;
    }

    private bool FilterChannel(object? item)
    {
        if (item is not OutputChannelViewModel channel)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ChannelSearchText))
        {
            return true;
        }

        return channel.Name.Contains(ChannelSearchText, StringComparison.OrdinalIgnoreCase);
    }

    private bool FilterMessage(object? item)
    {
        if (item is not OutputMessageEntry message)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(MessageSearchText))
        {
            return true;
        }

        return message.Message.Contains(MessageSearchText, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatMessage(OutputMessageEntry message)
    {
        return $"[{message.Timestamp:O}] {message.Message}";
    }
}
