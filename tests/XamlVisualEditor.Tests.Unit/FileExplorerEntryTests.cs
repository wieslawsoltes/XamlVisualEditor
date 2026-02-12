using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.FileExplorerExtension;
using Xunit;

#pragma warning disable CS0067

namespace XamlVisualEditor.Tests.Unit;

public sealed class FileExplorerEntryTests
{
    [Fact]
    public async Task OpenUsesDocumentOnlyBehavior()
    {
        string root = CreateTempDirectory();
        try
        {
            string path = Path.Combine(root, "sample.sln");
            File.WriteAllText(path, "data");

            StubEditorServices editor = new();
            StubWindow window = new();
            StubWorkspaceHost workspaceHost = new();
            FileExplorerEntry entry = new(path, isDirectory: false, icon: null, editor, window, workspaceHost, () => { });

            await entry.OpenAsync(CancellationToken.None);

            Assert.Equal(EditorDocumentOpenBehavior.DocumentOnly, editor.LastBehavior);
            Assert.Equal(path, editor.LastPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateFileUsesDocumentOnlyBehavior()
    {
        string root = CreateTempDirectory();
        try
        {
            StubEditorServices editor = new();
            StubWindow window = new("new.sln");
            StubWorkspaceHost workspaceHost = new();
            FileExplorerEntry entry = new(root, isDirectory: true, icon: null, editor, window, workspaceHost, () => { });

            await entry.CreateFileAsync(CancellationToken.None);

            string createdPath = Path.Combine(root, "new.sln");
            Assert.True(File.Exists(createdPath));
            Assert.Equal(EditorDocumentOpenBehavior.DocumentOnly, editor.LastBehavior);
            Assert.Equal(createdPath, editor.LastPath);
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

    private sealed class StubEditorServices : IEditorServices
    {
        public string? LastPath { get; private set; }

        public EditorDocumentOpenBehavior? LastBehavior { get; private set; }

        public IEditorDocument? ActiveDocument => null;

        public event EventHandler<EditorActiveDocumentChangedEventArgs>? ActiveDocumentChanged;

        public IReadOnlyList<IEditorDocument> GetOpenDocuments()
        {
            return Array.Empty<IEditorDocument>();
        }

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, CancellationToken ct)
        {
            LastPath = filePath;
            LastBehavior = EditorDocumentOpenBehavior.AllowWorkspaceLoad;
            return Task.FromResult<IEditorDocument?>(null);
        }

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, EditorDocumentOpenBehavior behavior, CancellationToken ct)
        {
            LastPath = filePath;
            LastBehavior = behavior;
            return Task.FromResult<IEditorDocument?>(null);
        }
    }

    private sealed class StubWindow : IWindow
    {
        private readonly string? _input;

        public StubWindow(string? input = null)
        {
            _input = input;
        }

        public Task ShowInformationMessageAsync(string message, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ShowWarningMessageAsync(string message, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ShowErrorMessageAsync(string message, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<string?> ShowInputBoxAsync(InputBoxOptions options, CancellationToken cancellationToken)
        {
            return Task.FromResult(_input);
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
