using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Debugging;
using XamlVisualEditor.Extensions.Hosting;
using WorkspaceExtensionEntry = XamlVisualEditor.WorkspaceExtension.WorkspaceExtension;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

#pragma warning disable CS0067

public sealed class WorkspaceExtensionTests
{
    [Fact]
    public async Task RegistersWorkspaceCommands()
    {
        var commands = new CommandRegistry();
        var contributions = new ExtensionContributionRegistry();
        var window = new InMemoryWindow();
        var workspaceInfo = new StubWorkspaceInfo { WorkspacePath = "workspace.sln" };
        var workspaceCommands = new StubWorkspaceCommands { HasWorkspace = true };
        ExtensionContext context = CreateContext(commands, contributions, window, workspaceInfo);

        var extension = new WorkspaceExtensionEntry(workspaceCommands, workspaceInfo);
        await extension.ActivateAsync(context, CancellationToken.None);

        Assert.Contains(contributions.MenuItems, item =>
            item.CommandId == "workspace.build"
            && item.Location == ExtensionMenuLocations.ToolsWorkspace);
        Assert.Contains(contributions.CommandPaletteItems, item => item.CommandId == "workspace.clean");
    }

    [Fact]
    public async Task BuildCommandInvokesWorkspaceCommands()
    {
        var commands = new CommandRegistry();
        var contributions = new ExtensionContributionRegistry();
        var window = new InMemoryWindow();
        var workspaceInfo = new StubWorkspaceInfo { WorkspacePath = "workspace.sln" };
        var workspaceCommands = new StubWorkspaceCommands { HasWorkspace = true };
        ExtensionContext context = CreateContext(commands, contributions, window, workspaceInfo);

        var extension = new WorkspaceExtensionEntry(workspaceCommands, workspaceInfo);
        await extension.ActivateAsync(context, CancellationToken.None);

        await commands.ExecuteAsync("workspace.build", null, CancellationToken.None);

        Assert.Equal(1, workspaceCommands.BuildCalls);
        Assert.Empty(window.Messages);
    }

    [Fact]
    public async Task BuildCommandShowsMessageWhenNoWorkspace()
    {
        var commands = new CommandRegistry();
        var contributions = new ExtensionContributionRegistry();
        var window = new InMemoryWindow();
        var workspaceInfo = new StubWorkspaceInfo();
        var workspaceCommands = new StubWorkspaceCommands { HasWorkspace = false };
        ExtensionContext context = CreateContext(commands, contributions, window, workspaceInfo);

        var extension = new WorkspaceExtensionEntry(workspaceCommands, workspaceInfo);
        await extension.ActivateAsync(context, CancellationToken.None);

        await commands.ExecuteAsync("workspace.build", null, CancellationToken.None);

        Assert.Equal(0, workspaceCommands.BuildCalls);
        Assert.Contains("No workspace loaded.", window.Messages);
    }

    private static ExtensionContext CreateContext(
        ICommands commands,
        IExtensionContributionRegistry contributions,
        IWindow window,
        IWorkspaceInfo workspaceInfo)
    {
        return new ExtensionContext(
            "test.extension",
            "/tmp",
            commands,
            contributions,
            new DebuggerServiceRegistry(),
            new InMemoryWorkspace(),
            workspaceInfo,
            window,
            new InMemoryDialogHost(),
            new InMemoryWorkspaceHost(),
            new ExtensionViewRegistry(),
            new ExtensionLanguageServiceRegistry(),
            new StubEditorServices(),
            new StubDiagnostics(),
            new StubTerminalBridge(),
            new InMemorySettingsStore(),
            new InMemoryExtensionStorage(),
            new StubExtensionLogger(),
            new List<IDisposable>());
    }

    private sealed class StubWorkspaceCommands : IWorkspaceCommands
    {
        public int RestoreCalls { get; private set; }
        public int BuildCalls { get; private set; }
        public int RebuildCalls { get; private set; }
        public int CleanCalls { get; private set; }
        public int LoadCalls { get; private set; }
        public bool HasWorkspace { get; set; }

        public Task LoadWorkspaceAsync(CancellationToken cancellationToken)
        {
            LoadCalls++;
            return Task.CompletedTask;
        }

        public Task RestoreWorkspaceAsync(CancellationToken cancellationToken)
        {
            RestoreCalls++;
            return Task.CompletedTask;
        }

        public Task BuildWorkspaceAsync(CancellationToken cancellationToken)
        {
            BuildCalls++;
            return Task.CompletedTask;
        }

        public Task RebuildWorkspaceAsync(CancellationToken cancellationToken)
        {
            RebuildCalls++;
            return Task.CompletedTask;
        }

        public Task CleanWorkspaceAsync(CancellationToken cancellationToken)
        {
            CleanCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubWorkspaceInfo : IWorkspaceInfo
    {
        public string? WorkspacePath { get; set; }

        public event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;
    }

    private sealed class StubEditorServices : IEditorServices
    {
        public IEditorDocument? ActiveDocument => null;

        public IReadOnlyList<IEditorDocument> GetOpenDocuments() => Array.Empty<IEditorDocument>();

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, CancellationToken ct)
        {
            return Task.FromResult<IEditorDocument?>(null);
        }

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, EditorDocumentOpenBehavior behavior, CancellationToken ct)
        {
            return Task.FromResult<IEditorDocument?>(null);
        }

        public event EventHandler<EditorActiveDocumentChangedEventArgs>? ActiveDocumentChanged;
    }

    private sealed class StubDiagnostics : IDiagnosticsService
    {
        public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(string? filePath, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());
        }

        public event EventHandler<DiagnosticsChangedEventArgs>? DiagnosticsChanged;
    }

    private sealed class StubTerminalBridge : ITerminalBridge
    {
        public Task<TerminalInfo> CreateAsync(TerminalCreateRequest request, CancellationToken ct)
        {
            return Task.FromResult(new TerminalInfo(Guid.NewGuid(), request.Title ?? "terminal"));
        }

        public Task SendTextAsync(Guid terminalId, string text, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubExtensionLogger : IExtensionLogger
    {
        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}

#pragma warning restore CS0067
