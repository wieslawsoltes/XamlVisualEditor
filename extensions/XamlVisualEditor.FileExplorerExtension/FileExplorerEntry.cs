using System.IO;
using System.Linq;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.FileExplorerExtension;

public sealed class FileExplorerEntry : IExtensionTreeItemOperationsProvider, IExtensionTreeItemChildrenProvider, IExtensionTreeItemWorkspaceProvider
{
    private readonly IEditorServices _editor;
    private readonly IWindow _window;
    private readonly IWorkspaceHost _workspaceHost;
    private readonly Action _refresh;

    public FileExplorerEntry(
        string fullPath,
        bool isDirectory,
        object? icon,
        IEditorServices editor,
        IWindow window,
        IWorkspaceHost workspaceHost,
        Action refresh)
    {
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Icon = icon;
        _editor = editor;
        _window = window;
        _workspaceHost = workspaceHost;
        _refresh = refresh;
        Name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    public string FullPath { get; }

    public string Name { get; }

    public bool IsDirectory { get; }

    public object? Icon { get; }

    public bool CanOpen => !IsDirectory && File.Exists(FullPath);

    public bool CanRename => File.Exists(FullPath) || Directory.Exists(FullPath);

    public bool CanDelete => File.Exists(FullPath) || Directory.Exists(FullPath);

    public bool CanCreateFile => ResolveTargetDirectory() is not null;

    public bool CanCreateFolder => ResolveTargetDirectory() is not null;

    public bool HasChildren => IsDirectory && TryHasChildren(FullPath);

    public bool CanOpenWorkspace => IsWorkspaceFile(FullPath);

    public Task OpenAsync(CancellationToken cancellationToken)
    {
        if (!CanOpen)
        {
            return Task.CompletedTask;
        }

        return _editor.OpenDocumentAsync(FullPath, EditorDocumentOpenBehavior.AllowWorkspaceLoad, cancellationToken);
    }

    public Task OpenWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (!CanOpenWorkspace)
        {
            return Task.CompletedTask;
        }

        return _workspaceHost.OpenWorkspaceAsync(FullPath, WorkspaceOpenMode.CurrentWindow, cancellationToken);
    }

    public async Task RenameAsync(CancellationToken cancellationToken)
    {
        if (!CanRename)
        {
            return;
        }

        string? parent = Path.GetDirectoryName(FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(parent))
        {
            return;
        }

        string? newName = await _window.ShowInputBoxAsync(
            new InputBoxOptions("Rename", "Enter the new name", Name),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, Name, StringComparison.Ordinal))
        {
            return;
        }

        string newPath = Path.Combine(parent, newName);
        if (File.Exists(newPath) || Directory.Exists(newPath))
        {
            await _window.ShowWarningMessageAsync("A file or folder with that name already exists.", cancellationToken);
            return;
        }

        try
        {
            if (IsDirectory)
            {
                Directory.Move(FullPath, newPath);
            }
            else
            {
                File.Move(FullPath, newPath);
            }

            _refresh();
        }
        catch (Exception ex)
        {
            await _window.ShowErrorMessageAsync($"Rename failed: {ex.Message}", cancellationToken);
        }
    }

    public async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (!CanDelete)
        {
            return;
        }

        QuickPickItem[] options =
        {
            new("Delete", "Permanently delete the item", null),
            new("Cancel", "Keep the item", null)
        };

        QuickPickItem? choice = await _window.ShowQuickPickAsync(
            options,
            new QuickPickOptions("Confirm Delete", CanPickMany: false),
            cancellationToken);

        if (choice is null || !string.Equals(choice.Label, "Delete", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            if (IsDirectory)
            {
                Directory.Delete(FullPath, recursive: true);
            }
            else
            {
                File.Delete(FullPath);
            }

            _refresh();
        }
        catch (Exception ex)
        {
            await _window.ShowErrorMessageAsync($"Delete failed: {ex.Message}", cancellationToken);
        }
    }

    public async Task CreateFileAsync(CancellationToken cancellationToken)
    {
        string? targetDirectory = ResolveTargetDirectory();
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return;
        }

        string? name = await _window.ShowInputBoxAsync(
            new InputBoxOptions("New File", "Enter the file name", ""),
            cancellationToken);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        string path = Path.Combine(targetDirectory, name);
        if (File.Exists(path) || Directory.Exists(path))
        {
            await _window.ShowWarningMessageAsync("A file or folder with that name already exists.", cancellationToken);
            return;
        }

        try
        {
            File.WriteAllText(path, string.Empty);
            _refresh();
            await _editor.OpenDocumentAsync(path, EditorDocumentOpenBehavior.AllowWorkspaceLoad, cancellationToken);
        }
        catch (Exception ex)
        {
            await _window.ShowErrorMessageAsync($"Create file failed: {ex.Message}", cancellationToken);
        }
    }

    public async Task CreateFolderAsync(CancellationToken cancellationToken)
    {
        string? targetDirectory = ResolveTargetDirectory();
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            return;
        }

        string? name = await _window.ShowInputBoxAsync(
            new InputBoxOptions("New Folder", "Enter the folder name", ""),
            cancellationToken);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        string path = Path.Combine(targetDirectory, name);
        if (File.Exists(path) || Directory.Exists(path))
        {
            await _window.ShowWarningMessageAsync("A file or folder with that name already exists.", cancellationToken);
            return;
        }

        try
        {
            Directory.CreateDirectory(path);
            _refresh();
        }
        catch (Exception ex)
        {
            await _window.ShowErrorMessageAsync($"Create folder failed: {ex.Message}", cancellationToken);
        }
    }

    private string? ResolveTargetDirectory()
    {
        if (IsDirectory)
        {
            return Directory.Exists(FullPath) ? FullPath : null;
        }

        string? parent = Path.GetDirectoryName(FullPath);
        return string.IsNullOrWhiteSpace(parent) ? null : parent;
    }

    private static bool TryHasChildren(string path)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWorkspaceFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string extension = Path.GetExtension(path);
        return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase);
    }
}
