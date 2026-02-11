using System.Collections.Generic;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>In-memory command registry.</summary>
public sealed class CommandRegistry : ICommands
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Func<CommandContext, Task>> _handlers = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IDisposable Register(string commandId, Func<CommandContext, Task> handler)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            throw new ArgumentException("Command id is required.", nameof(commandId));
        }

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        lock (_gate)
        {
            _handlers[commandId] = handler;
        }

        return new Registration(() => Unregister(commandId));
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(string commandId, IReadOnlyList<object?>? args, CancellationToken cancellationToken)
    {
        Func<CommandContext, Task>? handler;
        lock (_gate)
        {
            _handlers.TryGetValue(commandId, out handler);
        }

        if (handler is null)
        {
            throw new InvalidOperationException("Command not found: " + commandId);
        }

        var context = new CommandContext(args, new NullWorkspace(), new NullWindow());
        await handler(context).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetCommandsAsync(CancellationToken cancellationToken)
    {
        List<string> commands = new();
        lock (_gate)
        {
            foreach (string key in _handlers.Keys)
            {
                commands.Add(key);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(commands);
    }

    private void Unregister(string commandId)
    {
        lock (_gate)
        {
            _handlers.Remove(commandId);
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
