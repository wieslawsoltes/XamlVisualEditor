using System.Collections.Generic;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>In-memory extension storage.</summary>
public sealed class InMemoryExtensionStorage : IExtensionStorage
{
    private readonly object _gate = new();
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_values.TryGetValue(key, out object? value) && value is T typed)
            {
                return Task.FromResult<T?>(typed);
            }
        }

        return Task.FromResult<T?>(default);
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _values[key] = value;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _values.Remove(key);
        }

        return Task.CompletedTask;
    }
}
