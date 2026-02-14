using System;
using System.Collections.Generic;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>In-memory window implementation for testing.</summary>
public sealed class InMemoryWindow : IWindow
{
    private readonly List<string> _messages = new();
    private readonly Dictionary<string, InMemoryOutputChannel> _channels = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets recorded messages.</summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <inheritdoc />
    public Task ShowInformationMessageAsync(string message, CancellationToken cancellationToken)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShowWarningMessageAsync(string message, CancellationToken cancellationToken)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShowErrorMessageAsync(string message, CancellationToken cancellationToken)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> ShowInputBoxAsync(InputBoxOptions options, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public Task<QuickPickItem?> ShowQuickPickAsync(
        IReadOnlyList<QuickPickItem> items,
        QuickPickOptions options,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<QuickPickItem?>(null);
    }

    /// <inheritdoc />
    public IOutputChannel CreateOutputChannel(string name)
    {
        if (_channels.TryGetValue(name, out InMemoryOutputChannel? existing))
        {
            return existing;
        }

        OutputChannelInfo info = new(name);
        InMemoryOutputChannel channel = new(
            name,
            message => OutputChannelMessage?.Invoke(this, message),
            cleared => OutputChannelCleared?.Invoke(this, cleared),
            () => RemoveChannel(info));

        _channels[name] = channel;
        OutputChannelCreated?.Invoke(this, new OutputChannelEventArgs(info));
        return channel;
    }

    public Task<IReadOnlyList<OutputChannelInfo>> GetOutputChannelsAsync(CancellationToken cancellationToken)
    {
        List<OutputChannelInfo> results = new(_channels.Count);
        foreach (InMemoryOutputChannel channel in _channels.Values)
        {
            results.Add(new OutputChannelInfo(channel.Name));
        }

        return Task.FromResult<IReadOnlyList<OutputChannelInfo>>(results);
    }

#pragma warning disable CS0067
    public event EventHandler<OutputChannelEventArgs>? OutputChannelCreated;

    public event EventHandler<OutputChannelEventArgs>? OutputChannelRemoved;

    public event EventHandler<OutputChannelMessageEventArgs>? OutputChannelMessage;

    public event EventHandler<OutputChannelClearedEventArgs>? OutputChannelCleared;
#pragma warning restore CS0067

    /// <inheritdoc />
    public IStatusBarItem CreateStatusBarItem(StatusBarAlignment alignment, int priority)
    {
        return new InMemoryStatusBarItem();
    }

    private void RemoveChannel(OutputChannelInfo info)
    {
        _channels.Remove(info.Name);
        OutputChannelRemoved?.Invoke(this, new OutputChannelEventArgs(info));
    }
}

/// <summary>In-memory folder picker.</summary>
public sealed class InMemoryFolderPicker : IFolderPicker
{
    public Task<string?> PickFolderAsync(string? title, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(null);
    }
}

/// <summary>In-memory dialog host.</summary>
public sealed class InMemoryDialogHost : IDialogHost
{
    public IDisposable RegisterDialog(string dialogId, Func<object?, object> factory)
    {
        return new Registration();
    }

    public Task<T?> ShowDialogAsync<T>(string dialogId, object? viewModel, CancellationToken cancellationToken)
    {
        return Task.FromResult<T?>(default);
    }

    private sealed class Registration : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

/// <summary>In-memory workspace host.</summary>
public sealed class InMemoryWorkspaceHost : IWorkspaceHost
{
    public Task OpenWorkspaceAsync(string workspacePath, WorkspaceOpenMode mode, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

/// <summary>In-memory output channel.</summary>
public sealed class InMemoryOutputChannel : IOutputChannel
{
    private readonly List<string> _lines = new();
    private readonly OutputChannelInfo _info;
    private readonly Action<OutputChannelMessageEventArgs>? _messageCallback;
    private readonly Action<OutputChannelClearedEventArgs>? _clearedCallback;
    private readonly Action? _disposedCallback;

    /// <summary>Creates a channel.</summary>
    public InMemoryOutputChannel(
        string name,
        Action<OutputChannelMessageEventArgs>? messageCallback = null,
        Action<OutputChannelClearedEventArgs>? clearedCallback = null,
        Action? disposedCallback = null)
    {
        Name = name;
        _info = new OutputChannelInfo(name);
        _messageCallback = messageCallback;
        _clearedCallback = clearedCallback;
        _disposedCallback = disposedCallback;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>Gets recorded output lines.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <inheritdoc />
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

        _messageCallback?.Invoke(new OutputChannelMessageEventArgs(_info, value, false));
    }

    /// <inheritdoc />
    public void AppendLine(string value)
    {
        _lines.Add(value);
        _messageCallback?.Invoke(new OutputChannelMessageEventArgs(_info, value, true));
    }

    /// <inheritdoc />
    public void Show()
    {
    }

    /// <inheritdoc />
    public void Hide()
    {
    }

    /// <inheritdoc />
    public void Clear()
    {
        _lines.Clear();
        _clearedCallback?.Invoke(new OutputChannelClearedEventArgs(_info));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposedCallback?.Invoke();
    }
}

/// <summary>In-memory status bar item.</summary>
public sealed class InMemoryStatusBarItem : IStatusBarItem
{
    /// <inheritdoc />
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? Tooltip { get; set; }

    /// <inheritdoc />
    public string? CommandId { get; set; }

    /// <inheritdoc />
    public void Show()
    {
    }

    /// <inheritdoc />
    public void Hide()
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

internal sealed class NullWorkspace : IWorkspace
{
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged
    {
        add { }
        remove { }
    }

    public Task<IReadOnlyList<string>> FindFilesAsync(string includeGlob, string? excludeGlob, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        throw new FileNotFoundException("File not found.", path);
    }

    public Task WriteFileAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public IFileSystemWatcher CreateFileSystemWatcher(string glob)
    {
        return new InMemoryFileSystemWatcher(glob);
    }
}

internal sealed class NullWindow : IWindow
{
    public Task ShowInformationMessageAsync(string message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task ShowWarningMessageAsync(string message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task ShowErrorMessageAsync(string message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<string?> ShowInputBoxAsync(InputBoxOptions options, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<QuickPickItem?> ShowQuickPickAsync(
        IReadOnlyList<QuickPickItem> items,
        QuickPickOptions options,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<QuickPickItem?>(null);
    }

    public IOutputChannel CreateOutputChannel(string name)
    {
        return new InMemoryOutputChannel(name);
    }

    public Task<IReadOnlyList<OutputChannelInfo>> GetOutputChannelsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<OutputChannelInfo>>(Array.Empty<OutputChannelInfo>());
    }

#pragma warning disable CS0067
    public event EventHandler<OutputChannelEventArgs>? OutputChannelCreated;

    public event EventHandler<OutputChannelEventArgs>? OutputChannelRemoved;

    public event EventHandler<OutputChannelMessageEventArgs>? OutputChannelMessage;

    public event EventHandler<OutputChannelClearedEventArgs>? OutputChannelCleared;
#pragma warning restore CS0067

    public IStatusBarItem CreateStatusBarItem(StatusBarAlignment alignment, int priority)
    {
        return new InMemoryStatusBarItem();
    }
}
