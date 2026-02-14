namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>In-memory command metadata registry.</summary>
public sealed class CommandMetadataRegistry : ICommandMetadataRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CommandMetadata> _metadata = new(StringComparer.Ordinal);

    public event EventHandler? Changed;

    public IDisposable Register(string commandId, CommandMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            throw new ArgumentException("Command id is required.", nameof(commandId));
        }

        lock (_gate)
        {
            _metadata[commandId] = metadata;
        }

        Changed?.Invoke(this, EventArgs.Empty);

        return new Registration(() => Unregister(commandId));
    }

    public bool TryGet(string commandId, out CommandMetadata metadata)
    {
        lock (_gate)
        {
            return _metadata.TryGetValue(commandId, out metadata!);
        }
    }

    public IReadOnlyDictionary<string, CommandMetadata> GetAll()
    {
        lock (_gate)
        {
            return new Dictionary<string, CommandMetadata>(_metadata, StringComparer.Ordinal);
        }
    }

    private void Unregister(string commandId)
    {
        bool removed;
        lock (_gate)
        {
            removed = _metadata.Remove(commandId);
        }

        if (removed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
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
