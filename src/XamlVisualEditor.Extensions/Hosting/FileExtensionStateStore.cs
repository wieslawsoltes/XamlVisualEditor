using System.Linq;
using System.Text.Json;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Persists extension enablement state to disk.</summary>
public sealed class FileExtensionStateStore : IExtensionStateStore
{
    private readonly string _stateFilePath;
    private readonly object _gate = new();
    private Dictionary<string, bool>? _cache;

    public FileExtensionStateStore(string stateFilePath)
    {
        _stateFilePath = stateFilePath;
    }

    /// <inheritdoc />
    public Task<bool> GetEnabledAsync(string extensionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(extensionId))
        {
            throw new ArgumentException("Extension id is required.", nameof(extensionId));
        }

        lock (_gate)
        {
            EnsureLoaded();
            return Task.FromResult(_cache!.TryGetValue(extensionId, out bool enabled) && enabled);
        }
    }

    /// <inheritdoc />
    public Task SetEnabledAsync(string extensionId, bool enabled, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(extensionId))
        {
            throw new ArgumentException("Extension id is required.", nameof(extensionId));
        }

        lock (_gate)
        {
            EnsureLoaded();
            _cache![extensionId] = enabled;
            Save();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ExtensionStateEntry>> GetAllAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            EnsureLoaded();
            List<ExtensionStateEntry> results = new();
            foreach (KeyValuePair<string, bool> entry in _cache!)
            {
                results.Add(new ExtensionStateEntry(entry.Key, entry.Value));
            }

            return Task.FromResult<IReadOnlyList<ExtensionStateEntry>>(results);
        }
    }

    private void EnsureLoaded()
    {
        if (_cache is not null)
        {
            return;
        }

        _cache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(_stateFilePath))
        {
            return;
        }

        string json = File.ReadAllText(_stateFilePath);
        ExtensionStateFile? state = JsonSerializer.Deserialize<ExtensionStateFile>(json);
        if (state?.Extensions is null)
        {
            return;
        }

        foreach (ExtensionStateEntry entry in state.Extensions)
        {
            _cache[entry.ExtensionId] = entry.Enabled;
        }
    }

    private void Save()
    {
        string? directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        ExtensionStateFile file = new()
        {
            Extensions = _cache!.Select(entry => new ExtensionStateEntry(entry.Key, entry.Value)).ToList()
        };

        string json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_stateFilePath, json);
    }

    private sealed class ExtensionStateFile
    {
        public List<ExtensionStateEntry> Extensions { get; init; } = new();
    }
}
