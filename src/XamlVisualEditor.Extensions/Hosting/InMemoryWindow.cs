using System.Collections.Generic;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>In-memory window implementation for testing.</summary>
public sealed class InMemoryWindow : IWindow
{
    private readonly List<string> _messages = new();

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
        return new InMemoryOutputChannel(name);
    }

    /// <inheritdoc />
    public IStatusBarItem CreateStatusBarItem(StatusBarAlignment alignment, int priority)
    {
        return new InMemoryStatusBarItem();
    }
}

/// <summary>In-memory output channel.</summary>
public sealed class InMemoryOutputChannel : IOutputChannel
{
    private readonly List<string> _lines = new();

    /// <summary>Creates a channel.</summary>
    public InMemoryOutputChannel(string name)
    {
        Name = name;
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
            return;
        }

        int last = _lines.Count - 1;
        _lines[last] = _lines[last] + value;
    }

    /// <inheritdoc />
    public void AppendLine(string value)
    {
        _lines.Add(value);
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
    }

    /// <inheritdoc />
    public void Dispose()
    {
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

    public IStatusBarItem CreateStatusBarItem(StatusBarAlignment alignment, int priority)
    {
        return new InMemoryStatusBarItem();
    }
}
