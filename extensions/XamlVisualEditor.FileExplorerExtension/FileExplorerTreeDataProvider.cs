using System.Collections.Generic;
using System.IO;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.FileExplorerExtension;

public sealed class FileExplorerTreeDataProvider : ITreeDataProvider<FileExplorerEntry>, IDisposable
{
    private readonly IWorkspaceInfo _workspaceInfo;
    private readonly IEditorServices _editor;
    private readonly IWindow _window;
    private readonly IWorkspaceHost _workspaceHost;
    private IFileExplorerIconProvider _iconProvider;
    private bool _showHidden;

    public FileExplorerTreeDataProvider(
        IWorkspaceInfo workspaceInfo,
        IEditorServices editor,
        IWindow window,
        IWorkspaceHost workspaceHost,
        IFileExplorerIconProvider iconProvider,
        bool showHidden)
    {
        _workspaceInfo = workspaceInfo;
        _editor = editor;
        _window = window;
        _workspaceHost = workspaceHost;
        _iconProvider = iconProvider;
        _showHidden = showHidden;
        _workspaceInfo.WorkspaceChanged += OnWorkspaceChanged;
    }

    public event EventHandler? Changed;

    public void Dispose()
    {
        _workspaceInfo.WorkspaceChanged -= OnWorkspaceChanged;
    }

    public void UpdateSettings(IFileExplorerIconProvider iconProvider, bool showHidden)
    {
        _iconProvider = iconProvider;
        _showHidden = showHidden;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<IReadOnlyList<FileExplorerEntry>> GetChildrenAsync(FileExplorerEntry? element, CancellationToken cancellationToken)
    {
        string? rootPath = ResolveRootPath(_workspaceInfo.WorkspacePath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return Array.Empty<FileExplorerEntry>();
        }

        string targetPath = element?.FullPath ?? rootPath;
        if (!Directory.Exists(targetPath))
        {
            return Array.Empty<FileExplorerEntry>();
        }

        try
        {
            return await Task.Run(() =>
            {
                List<FileExplorerEntry> results = new();
                AddEntries(targetPath, results, cancellationToken);
                return (IReadOnlyList<FileExplorerEntry>)results;
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<FileExplorerEntry>();
        }
    }

    public Task<TreeItem> GetTreeItemAsync(FileExplorerEntry element, CancellationToken cancellationToken)
    {
        TreeItem item = new(element.Name, null, element.FullPath, element.Icon);
        return Task.FromResult(item);
    }

    private void AddEntries(string path, List<FileExplorerEntry> results, CancellationToken cancellationToken)
    {
        List<FileExplorerEntry> directories = new();
        List<FileExplorerEntry> files = new();

        foreach (string directory in Directory.EnumerateDirectories(path))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            if (!_showHidden && IsHiddenPath(directory))
            {
                continue;
            }

            directories.Add(CreateEntry(directory, isDirectory: true));
        }

        foreach (string file in Directory.EnumerateFiles(path))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            if (!_showHidden && IsHiddenPath(file))
            {
                continue;
            }

            files.Add(CreateEntry(file, isDirectory: false));
        }

        directories.Sort(CompareEntries);
        files.Sort(CompareEntries);

        results.AddRange(directories);
        results.AddRange(files);
    }

    private FileExplorerEntry CreateEntry(string path, bool isDirectory)
    {
        object? icon = _iconProvider.GetIcon(path, isDirectory);
        return new FileExplorerEntry(path, isDirectory, icon, _editor, _window, _workspaceHost, NotifyChanged);
    }

    private static int CompareEntries(FileExplorerEntry left, FileExplorerEntry right)
    {
        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHiddenPath(string path)
    {
        string name = Path.GetFileName(path);
        if (name.StartsWith('.'))
        {
            return true;
        }

        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            return (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveRootPath(string? workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return null;
        }

        if (Directory.Exists(workspacePath))
        {
            return workspacePath;
        }

        if (File.Exists(workspacePath))
        {
            return Path.GetDirectoryName(workspacePath);
        }

        return Path.GetDirectoryName(workspacePath);
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs e)
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
