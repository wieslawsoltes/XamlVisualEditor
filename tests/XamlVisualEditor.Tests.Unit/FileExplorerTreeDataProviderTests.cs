using System.IO;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.FileExplorerExtension;
using Xunit;

#pragma warning disable CS0067

namespace XamlVisualEditor.Tests.Unit;

public sealed class FileExplorerTreeDataProviderTests
{
    [Fact]
    public async Task FiltersHiddenFilesWhenDisabled()
    {
        string root = CreateTempDirectory();
        try
        {
            string visibleFile = Path.Combine(root, "visible.txt");
            File.WriteAllText(visibleFile, "data");

            string hiddenFile = Path.Combine(root, ".hidden.txt");
            File.WriteAllText(hiddenFile, "data");
            if (OperatingSystem.IsWindows())
            {
                File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);
            }

            StubWorkspaceInfo workspaceInfo = new(root);
            StubEditorServices editor = new();
            StubWindow window = new();
            StubWorkspaceHost workspaceHost = new();
            IFileExplorerIconProvider iconProvider = new ThemeFileExplorerIconProvider(FileExplorerIconTheme.Windows);
            FileExplorerTreeDataProvider provider = new(workspaceInfo, editor, window, workspaceHost, iconProvider, showHidden: false);

            IReadOnlyList<FileExplorerEntry> entries = await provider.GetChildrenAsync(null, CancellationToken.None);

            Assert.Contains(entries, entry => entry.FullPath == visibleFile);
            Assert.DoesNotContain(entries, entry => entry.FullPath == hiddenFile);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UsesWorkspaceDirectoryWhenWorkspaceIsFile()
    {
        string root = CreateTempDirectory();
        try
        {
            string workspaceFile = Path.Combine(root, "sample.sln");
            File.WriteAllText(workspaceFile, "data");

            string file = Path.Combine(root, "readme.md");
            File.WriteAllText(file, "data");

            StubWorkspaceInfo workspaceInfo = new(workspaceFile);
            StubEditorServices editor = new();
            StubWindow window = new();
            StubWorkspaceHost workspaceHost = new();
            IFileExplorerIconProvider iconProvider = new ThemeFileExplorerIconProvider(FileExplorerIconTheme.Windows);
            FileExplorerTreeDataProvider provider = new(workspaceInfo, editor, window, workspaceHost, iconProvider, showHidden: true);

            IReadOnlyList<FileExplorerEntry> entries = await provider.GetChildrenAsync(null, CancellationToken.None);

            Assert.Contains(entries, entry => entry.FullPath == file);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "xve-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class StubWorkspaceInfo : IWorkspaceInfo
    {
        public StubWorkspaceInfo(string? workspacePath)
        {
            WorkspacePath = workspacePath;
        }

        public string? WorkspacePath { get; private set; }

        public event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;

        public void UpdateWorkspacePath(string? workspacePath)
        {
            WorkspacePath = workspacePath;
            WorkspaceChanged?.Invoke(this, new WorkspaceChangedEventArgs(WorkspacePath));
        }
    }

    private sealed class StubEditorServices : IEditorServices
    {
        public IEditorDocument? ActiveDocument => null;

        public event EventHandler<EditorActiveDocumentChangedEventArgs>? ActiveDocumentChanged;

        public IReadOnlyList<IEditorDocument> GetOpenDocuments()
        {
            return Array.Empty<IEditorDocument>();
        }

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, CancellationToken ct)
        {
            return Task.FromResult<IEditorDocument?>(null);
        }

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, EditorDocumentOpenBehavior behavior, CancellationToken ct)
        {
            return Task.FromResult<IEditorDocument?>(null);
        }
    }

    private sealed class StubWindow : IWindow
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
            throw new NotSupportedException();
        }

        public IStatusBarItem CreateStatusBarItem(StatusBarAlignment alignment, int priority)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubWorkspaceHost : IWorkspaceHost
    {
        public Task OpenWorkspaceAsync(string workspacePath, WorkspaceOpenMode mode, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
