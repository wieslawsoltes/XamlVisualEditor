namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Stores recent crash reports in memory.</summary>
public sealed class InMemoryExtensionCrashReporter : IExtensionCrashReporter
{
    private readonly int _capacity;
    private readonly LinkedList<ExtensionCrashInfo> _items = new();

    /// <summary>Creates a reporter with a maximum capacity.</summary>
    public InMemoryExtensionCrashReporter(int capacity = 100)
    {
        _capacity = capacity;
    }

    /// <summary>Gets recorded crash reports.</summary>
    public IReadOnlyCollection<ExtensionCrashInfo> Items => _items;

    /// <inheritdoc />
    public Task RecordAsync(ExtensionCrashInfo crashInfo, CancellationToken cancellationToken)
    {
        _items.AddFirst(crashInfo);
        while (_items.Count > _capacity)
        {
            _items.RemoveLast();
        }

        return Task.CompletedTask;
    }
}
