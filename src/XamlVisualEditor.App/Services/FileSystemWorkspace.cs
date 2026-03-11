using System.IO;
using System.Text.RegularExpressions;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.App.Services;

/// <summary>
/// File-system-backed workspace service for extension APIs.
/// </summary>
public sealed class FileSystemWorkspace : IWorkspace
{
    private readonly IWorkspaceInfo _workspaceInfo;

    public FileSystemWorkspace(IWorkspaceInfo workspaceInfo)
    {
        _workspaceInfo = workspaceInfo;
    }

    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged
    {
        add { }
        remove { }
    }

    public Task<IReadOnlyList<string>> FindFilesAsync(
        string includeGlob,
        string? excludeGlob,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string rootPath = GetWorkspaceRootPath();
        if (!Directory.Exists(rootPath))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        Regex include = GlobToRegex(NormalizeGlob(includeGlob));
        Regex? exclude = string.IsNullOrWhiteSpace(excludeGlob)
            ? null
            : GlobToRegex(NormalizeGlob(excludeGlob));

        List<string> matches = new();
        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true
        };

        foreach (string path in Directory.EnumerateFiles(rootPath, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relative = NormalizePath(Path.GetRelativePath(rootPath, path));
            if (!include.IsMatch(relative))
            {
                continue;
            }

            if (exclude is not null && exclude.IsMatch(relative))
            {
                continue;
            }

            matches.Add(path);
        }

        matches.Sort(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyList<string>>(matches);
    }

    public Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        string resolvedPath = ResolvePath(path);
        return File.ReadAllBytesAsync(resolvedPath, cancellationToken);
    }

    public async Task WriteFileAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        string resolvedPath = ResolvePath(path);
        string? directoryPath = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await File.WriteAllBytesAsync(resolvedPath, content, cancellationToken).ConfigureAwait(false);
    }

    public IFileSystemWatcher CreateFileSystemWatcher(string glob)
    {
        string rootPath = GetWorkspaceRootPath();
        if (!Directory.Exists(rootPath))
        {
            return new NoOpFileSystemWatcher();
        }

        return new FileSystemWorkspaceWatcher(rootPath, NormalizeGlob(glob));
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(GetWorkspaceRootPath(), path));
    }

    private string GetWorkspaceRootPath()
    {
        string? workspacePath = _workspaceInfo.WorkspacePath;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return Directory.GetCurrentDirectory();
        }

        if (Directory.Exists(workspacePath))
        {
            return Path.GetFullPath(workspacePath);
        }

        if (File.Exists(workspacePath))
        {
            string? directoryPath = Path.GetDirectoryName(workspacePath);
            return string.IsNullOrWhiteSpace(directoryPath)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(directoryPath);
        }

        string fullPath = Path.GetFullPath(workspacePath);
        if (Path.HasExtension(fullPath))
        {
            string? directoryPath = Path.GetDirectoryName(fullPath);
            return string.IsNullOrWhiteSpace(directoryPath)
                ? Directory.GetCurrentDirectory()
                : directoryPath;
        }

        return fullPath;
    }

    private static string NormalizeGlob(string glob)
    {
        if (string.IsNullOrWhiteSpace(glob))
        {
            return "**/*";
        }

        return NormalizePath(glob.Trim());
    }

    private static string NormalizePath(string path)
    {
        return path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static Regex GlobToRegex(string glob)
    {
        string pattern = Regex.Escape(glob)
            .Replace("\\*\\*/", "(?:.*/)?", StringComparison.Ordinal)
            .Replace("/\\*\\*", "(?:/.*)?", StringComparison.Ordinal)
            .Replace("\\*\\*", ".*", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal);

        return new Regex("^" + pattern + "$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    private sealed class FileSystemWorkspaceWatcher : IFileSystemWatcher
    {
        private readonly string _rootPath;
        private readonly Regex _include;
        private readonly FileSystemWatcher _watcher;

        public FileSystemWorkspaceWatcher(string rootPath, string glob)
        {
            _rootPath = Path.GetFullPath(rootPath);
            _include = GlobToRegex(glob);
            _watcher = new FileSystemWatcher(_rootPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                               | NotifyFilters.DirectoryName
                               | NotifyFilters.LastWrite
                               | NotifyFilters.CreationTime
            };

            _watcher.Created += OnCreated;
            _watcher.Changed += OnChanged;
            _watcher.Deleted += OnDeleted;
            _watcher.Renamed += OnRenamed;
            _watcher.EnableRaisingEvents = true;
        }

        public event EventHandler<string>? Created;
        public event EventHandler<string>? Changed;
        public event EventHandler<string>? Deleted;

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnCreated;
            _watcher.Changed -= OnChanged;
            _watcher.Deleted -= OnDeleted;
            _watcher.Renamed -= OnRenamed;
            _watcher.Dispose();
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (Matches(e.FullPath))
            {
                Created?.Invoke(this, e.FullPath);
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (Matches(e.FullPath))
            {
                Changed?.Invoke(this, e.FullPath);
            }
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (Matches(e.FullPath))
            {
                Deleted?.Invoke(this, e.FullPath);
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (Matches(e.OldFullPath))
            {
                Deleted?.Invoke(this, e.OldFullPath);
            }

            if (Matches(e.FullPath))
            {
                Created?.Invoke(this, e.FullPath);
            }
        }

        private bool Matches(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(path);
            bool insideRoot = fullPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(fullPath, _rootPath, StringComparison.OrdinalIgnoreCase);
            if (!insideRoot)
            {
                return false;
            }

            string relative = NormalizePath(Path.GetRelativePath(_rootPath, fullPath));
            return _include.IsMatch(relative);
        }
    }

    private sealed class NoOpFileSystemWatcher : IFileSystemWatcher
    {
        public event EventHandler<string>? Created
        {
            add { }
            remove { }
        }

        public event EventHandler<string>? Changed
        {
            add { }
            remove { }
        }

        public event EventHandler<string>? Deleted
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
        }
    }
}
