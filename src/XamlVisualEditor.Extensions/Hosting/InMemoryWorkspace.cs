using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>In-memory workspace implementation.</summary>
public sealed class InMemoryWorkspace : IWorkspace
{
    private readonly object _gate = new();
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> FindFilesAsync(
        string includeGlob,
        string? excludeGlob,
        CancellationToken cancellationToken)
    {
        var matches = new List<string>();
        Regex include = GlobToRegex(includeGlob);
        Regex? exclude = string.IsNullOrWhiteSpace(excludeGlob) ? null : GlobToRegex(excludeGlob);

        lock (_gate)
        {
            foreach (string path in _files.Keys)
            {
                if (!include.IsMatch(path))
                {
                    continue;
                }

                if (exclude is not null && exclude.IsMatch(path))
                {
                    continue;
                }

                matches.Add(path);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(matches);
    }

    /// <inheritdoc />
    public Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_files.TryGetValue(path, out byte[]? content))
            {
                return Task.FromResult(content);
            }
        }

        throw new FileNotFoundException("File not found.", path);
    }

    /// <inheritdoc />
    public Task WriteFileAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _files[path] = content;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IFileSystemWatcher CreateFileSystemWatcher(string glob)
    {
        return new InMemoryFileSystemWatcher(glob);
    }

    /// <summary>Raises a configuration change event.</summary>
    public void RaiseConfigurationChanged(string? section)
    {
        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(section));
    }

    private static Regex GlobToRegex(string glob)
    {
        string pattern = Regex.Escape(glob)
            .Replace("\\*\\*", "__DOUBLE_STAR__", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal)
            .Replace("__DOUBLE_STAR__", ".*", StringComparison.Ordinal);

        return new Regex("^" + pattern + "$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }
}

/// <summary>In-memory file system watcher.</summary>
public sealed class InMemoryFileSystemWatcher : IFileSystemWatcher
{
    /// <summary>Creates a watcher.</summary>
    public InMemoryFileSystemWatcher(string glob)
    {
        Glob = glob;
    }

    /// <summary>Gets the glob pattern.</summary>
    public string Glob { get; }

    /// <inheritdoc />
    public event EventHandler<string>? Created;

    /// <inheritdoc />
    public event EventHandler<string>? Changed;

    /// <inheritdoc />
    public event EventHandler<string>? Deleted;

    /// <summary>Triggers a create event.</summary>
    public void TriggerCreated(string path)
    {
        Created?.Invoke(this, path);
    }

    /// <summary>Triggers a change event.</summary>
    public void TriggerChanged(string path)
    {
        Changed?.Invoke(this, path);
    }

    /// <summary>Triggers a delete event.</summary>
    public void TriggerDeleted(string path)
    {
        Deleted?.Invoke(this, path);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
