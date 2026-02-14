using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
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
        ExtensionContext context = CreateContext(commands, contributions, window, workspaceInfo, workspaceCommands);

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
        ExtensionContext context = CreateContext(commands, contributions, window, workspaceInfo, workspaceCommands);

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
        ExtensionContext context = CreateContext(commands, contributions, window, workspaceInfo, workspaceCommands);

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
        IWorkspaceInfo workspaceInfo,
        IWorkspaceCommands workspaceCommands)
    {
        var editorServices = new StubEditorServices();
        var commandMetadata = new CommandMetadataRegistry();
        var designerHost = new DesignerHostAdapter(editorServices);
        var navigation = new StubNavigationService();
        var navigationHistory = new StubNavigationHistoryService();
        var animationHost = new StubAnimationEditorHost();
        var collaborationHost = new StubCollaborationPanelHost();
        var debugSettingsHost = new StubDebugSettingsHost();
        var lspSettingsHost = new StubLspSettingsHost();
        var viewHost = new StubExtensionViewHost();
        var workspaceModel = new WorkspaceModelAdapter(workspaceInfo, workspaceCommands);
        var propertyEditors = new PropertyEditorRegistry();

        return new ExtensionContext(
            "test.extension",
            "/tmp",
            commands,
            commandMetadata,
            contributions,
            new DebuggerServiceRegistry(),
            designerHost,
            new InMemoryWorkspace(),
            workspaceModel,
            workspaceInfo,
            window,
            new InMemoryDialogHost(),
            new InMemoryWorkspaceHost(),
            new ExtensionViewRegistry(),
            new ExtensionLanguageServiceRegistry(),
            navigation,
            navigationHistory,
            animationHost,
            collaborationHost,
            debugSettingsHost,
            lspSettingsHost,
            editorServices,
            new StubDiagnostics(),
            propertyEditors,
            new StubTerminalBridge(),
            viewHost,
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

    private sealed class StubExtensionViewHost : IExtensionViewHost
    {
        public Task ShowAsync(string viewId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ToggleAsync(string viewId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> IsVisibleAsync(string viewId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task ActivateAsync(string viewId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubNavigationService : ILanguageNavigationService
    {
        public Task<IReadOnlyList<LanguageLocation>> FindDefinitionsAsync(
            LanguagePositionContext context,
            CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<LanguageLocation>>(Array.Empty<LanguageLocation>());
        }

        public Task<IReadOnlyList<LanguageLocation>> FindReferencesAsync(
            LanguagePositionContext context,
            CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<LanguageLocation>>(Array.Empty<LanguageLocation>());
        }
    }

    private sealed class StubNavigationHistoryService : INavigationHistoryService
    {
        public bool CanNavigateBack => false;
        public bool CanNavigateForward => false;
        public event EventHandler<NavigationHistoryChangedEventArgs>? HistoryChanged;

        public Task<bool> NavigateBackAsync(CancellationToken ct)
        {
            return Task.FromResult(false);
        }

        public Task<bool> NavigateForwardAsync(CancellationToken ct)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class StubWorkspaceInfo : IWorkspaceInfo
    {
        public string? WorkspacePath { get; set; }

        public event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;
    }

    private sealed class StubAnimationEditorHost : IAnimationEditorHost
    {
        public object? ViewModel => null;
    }

    private sealed class StubCollaborationPanelHost : ICollaborationPanelHost
    {
        public object? ViewModel => null;
    }

    private sealed class StubDebugSettingsHost : IDebugSettingsHost
    {
        public object? ViewModel => null;
    }

    private sealed class StubLspSettingsHost : ILspSettingsHost
    {
        public object? ViewModel => null;
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

        public Task<bool> OpenLocationAsync(LanguageLocation location, CancellationToken ct)
        {
            return Task.FromResult(false);
        }

        public event EventHandler<EditorActiveDocumentChangedEventArgs>? ActiveDocumentChanged;
    }

    private sealed class StubDiagnostics : IDiagnosticsService
    {
        public event EventHandler<DiagnosticsChannelsChangedEventArgs>? ChannelsChanged;

        public event EventHandler<DiagnosticsChannelPublishedEventArgs>? DiagnosticsChannelPublished;

        public event EventHandler<DiagnosticsSnapshotPublishedEventArgs>? DiagnosticsSnapshotPublished;

        public event EventHandler<DiagnosticsPublishedEventArgs>? DiagnosticsPublished;

        public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(string? filePath, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());
        }

        public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(DiagnosticsQuery query, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());
        }

        public Task<IReadOnlyList<DiagnosticsDocumentSnapshot>> GetDiagnosticsSnapshotAsync(
            DiagnosticsQuery query,
            CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<DiagnosticsDocumentSnapshot>>(Array.Empty<DiagnosticsDocumentSnapshot>());
        }

        public Task<IReadOnlyList<DiagnosticsChannelInfo>> GetChannelsAsync(CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<DiagnosticsChannelInfo>>(Array.Empty<DiagnosticsChannelInfo>());
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
