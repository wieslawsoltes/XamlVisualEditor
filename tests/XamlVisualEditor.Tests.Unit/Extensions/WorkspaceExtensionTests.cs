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
        var shellCommands = new StubShellCommandBridge();
        ExtensionContext context = CreateContext(commands, contributions, window, workspaceInfo, workspaceCommands);

        var extension = new WorkspaceExtensionEntry(workspaceCommands, workspaceInfo, shellCommands);
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
        var shellCommands = new StubShellCommandBridge();
        ExtensionContext context = CreateContext(commands, contributions, window, workspaceInfo, workspaceCommands);

        var extension = new WorkspaceExtensionEntry(workspaceCommands, workspaceInfo, shellCommands);
        await extension.ActivateAsync(context, CancellationToken.None);

        await commands.ExecuteAsync("workspace.build", null, CancellationToken.None);

        Assert.Equal(1, workspaceCommands.BuildCalls);
        Assert.Empty(window.Messages);
    }

    [Fact]
    public async Task ShellBridgeCommand_IsRegistered_AndInvoked()
    {
        var commands = new CommandRegistry();
        var contributions = new ExtensionContributionRegistry();
        var window = new InMemoryWindow();
        var workspaceInfo = new StubWorkspaceInfo { WorkspacePath = "workspace.sln" };
        var workspaceCommands = new StubWorkspaceCommands { HasWorkspace = true };
        var shellCommands = new StubShellCommandBridge();
        ExtensionContext context = CreateContext(commands, contributions, window, workspaceInfo, workspaceCommands);

        var extension = new WorkspaceExtensionEntry(workspaceCommands, workspaceInfo, shellCommands);
        await extension.ActivateAsync(context, CancellationToken.None);

        await commands.ExecuteAsync("terminal.new", null, CancellationToken.None);

        ShellCommandKind command = Assert.Single(shellCommands.Calls);
        Assert.Equal(ShellCommandKind.NewTerminal, command);
    }

    [Fact]
    public async Task Registers_Edit_And_Debug_Menu_Contributions()
    {
        var commands = new CommandRegistry();
        var contributions = new ExtensionContributionRegistry();
        var window = new InMemoryWindow();
        var workspaceInfo = new StubWorkspaceInfo { WorkspacePath = "workspace.sln" };
        var workspaceCommands = new StubWorkspaceCommands { HasWorkspace = true };
        var shellCommands = new StubShellCommandBridge();
        ExtensionContext context = CreateContext(commands, contributions, window, workspaceInfo, workspaceCommands);

        var extension = new WorkspaceExtensionEntry(workspaceCommands, workspaceInfo, shellCommands);
        await extension.ActivateAsync(context, CancellationToken.None);

        Assert.Contains(contributions.MenuItems, item =>
            item.CommandId == "editing.undo"
            && item.Location == ExtensionMenuLocations.Edit);
        Assert.Contains(contributions.MenuItems, item =>
            item.CommandId == "debug.start"
            && item.Location == ExtensionMenuLocations.Debug);
    }

    [Fact]
    public async Task BuildCommandShowsMessageWhenNoWorkspace()
    {
        var commands = new CommandRegistry();
        var contributions = new ExtensionContributionRegistry();
        var window = new InMemoryWindow();
        var workspaceInfo = new StubWorkspaceInfo();
        var workspaceCommands = new StubWorkspaceCommands { HasWorkspace = false };
        var shellCommands = new StubShellCommandBridge();
        ExtensionContext context = CreateContext(commands, contributions, window, workspaceInfo, workspaceCommands);

        var extension = new WorkspaceExtensionEntry(workspaceCommands, workspaceInfo, shellCommands);
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
        var collaborationHost = new StubCollaborationHost();
        var debugSettingsHost = new StubDebugSettingsHost();
        var lspSettingsHost = new StubLspSettingsHost();
        var viewHost = new StubExtensionViewHost();
        var workspaceModel = new WorkspaceModelAdapter(workspaceInfo, workspaceCommands, new StubWorkspaceService());
        var propertyEditors = new PropertyEditorRegistry();
        var permissions = new StubExtensionPermissions();

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
            new StubSystemIconService(),
            window,
            new InMemoryDialogHost(),
            new InMemoryWorkspaceHost(),
            new ExtensionViewRegistry(),
            new ExtensionLanguageServiceRegistry(),
            navigation,
            navigationHistory,
            animationHost,
            collaborationHost,
            collaborationHost,
            debugSettingsHost,
            lspSettingsHost,
            editorServices,
            new StubDiagnostics(),
            propertyEditors,
            new StubTerminalBridge(),
            permissions,
            viewHost,
            new InMemorySettingsStore(),
            new InMemoryExtensionStorage(),
            new StubExtensionLogger(),
            new List<IDisposable>());
    }

    private sealed class StubSystemIconService : ISystemIconService
    {
        public object? GetIcon(string? path, bool isDirectory, object? fallbackIcon = null, int iconSize = 16)
            => fallbackIcon;

        public object? GetFileIcon(string? path, object? fallbackIcon = null, int iconSize = 16)
            => fallbackIcon;

        public object? GetFolderIcon(string? path, object? fallbackIcon = null, int iconSize = 16)
            => fallbackIcon;
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
        public event EventHandler<ExtensionViewVisibilityChangedEventArgs>? VisibilityChanged;

        public event EventHandler<ExtensionViewFocusChangedEventArgs>? FocusChanged;

        public Task ShowAsync(string viewId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ToggleAsync(string viewId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> IsVisibleAsync(string viewId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task ActivateAsync(string viewId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubShellCommandBridge : IShellCommandBridge
    {
        public List<ShellCommandKind> Calls { get; } = new();

        public Task ExecuteAsync(ShellCommandKind command, CancellationToken cancellationToken)
        {
            Calls.Add(command);
            return Task.CompletedTask;
        }
    }

    private sealed class StubExtensionPermissions : IExtensionPermissions
    {
        public event EventHandler<ExtensionPermissionAuditEventArgs>? AccessAudited;
        public event EventHandler<ExtensionPermissionChangedEventArgs>? Changed;

        public void Declare(IReadOnlyList<ExtensionCapabilityDeclaration> capabilities)
        {
        }

        public Task<ExtensionPermissionDecision> RequestAsync(string capabilityId, CancellationToken cancellationToken)
            => Task.FromResult(new ExtensionPermissionDecision(
                capabilityId,
                IsAllowed: true,
                IsRemembered: false,
                ExtensionPermissionDecisionSource.Prompt,
                DateTimeOffset.UtcNow));

        public Task<IReadOnlyList<ExtensionPermissionEntry>> GetRememberedAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ExtensionPermissionEntry>>(Array.Empty<ExtensionPermissionEntry>());

        public Task ClearRememberedAsync(string? capabilityId, CancellationToken cancellationToken)
            => Task.CompletedTask;
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

        public Task<IReadOnlyList<LanguageLocation>> FindImplementationsAsync(
            LanguagePositionContext context,
            CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<LanguageLocation>>(Array.Empty<LanguageLocation>());
        }

        public Task<IReadOnlyList<LanguageSymbol>> GetWorkspaceSymbolsAsync(
            LanguageSymbolQuery query,
            CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<LanguageSymbol>>(Array.Empty<LanguageSymbol>());
        }

        public Task<LanguageRenameInfo?> PrepareRenameAsync(
            LanguagePositionContext context,
            CancellationToken ct)
        {
            return Task.FromResult<LanguageRenameInfo?>(null);
        }

        public Task<LanguageWorkspaceEdit?> RenameAsync(
            LanguageRenameContext context,
            CancellationToken ct)
        {
            return Task.FromResult<LanguageWorkspaceEdit?>(null);
        }

        public Task<IReadOnlyList<LanguageCodeAction>> GetCodeActionsAsync(
            LanguageCodeActionContext context,
            CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<LanguageCodeAction>>(Array.Empty<LanguageCodeAction>());
        }

        public Task<LanguageCodeAction?> ResolveCodeActionAsync(LanguageCodeAction action, CancellationToken ct)
        {
            return Task.FromResult<LanguageCodeAction?>(action);
        }

        public Task<bool> ApplyCodeActionAsync(LanguageCodeAction action, CancellationToken ct)
        {
            return Task.FromResult(false);
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

        public IDisposable BeginTransaction(string name)
        {
            return System.Reactive.Disposables.Disposable.Empty;
        }

        public Task RefreshPreviewAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubCollaborationHost : ICollaborationHost, ICollaborationPanelHost
    {
        public bool IsSessionActive => false;

        public string? SessionId => null;

        public string StatusMessage => string.Empty;

        public object? ViewModel => null;

        public event EventHandler<CollaborationSessionChangedEventArgs>? SessionChanged;

        public event EventHandler<CollaborationParticipantsChangedEventArgs>? ParticipantsChanged;

        public IReadOnlyList<CollaborationParticipantInfo> GetParticipants()
        {
            return Array.Empty<CollaborationParticipantInfo>();
        }

        public Task StartSessionAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<bool> JoinSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task LeaveSessionAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<string?> CreateShareLinkAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<bool> InviteAsync(string invitee, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class StubDebugSettingsHost : IDebugSettingsHost
    {
        public event EventHandler<DebugSettingsChangedEventArgs>? Changed;

        public DebugSettingsState GetState()
        {
            return new DebugSettingsState(string.Empty, false, false, string.Empty);
        }

        public Task SetAdapterPathAsync(string adapterPath, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SetAutoDownloadToolsAsync(bool autoDownloadTools, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DownloadNetcoredbgAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubLspSettingsHost : ILspSettingsHost
    {
        public string SettingsPath => string.Empty;

        public event EventHandler<LspSettingsChangedEventArgs>? Changed;

        public Task<IReadOnlyList<LspServerSettings>> LoadServersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<LspServerSettings>>(Array.Empty<LspServerSettings>());
        }

        public Task SaveServersAsync(IReadOnlyList<LspServerSettings> servers, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
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
        public event EventHandler<TerminalChangedEventArgs>? TerminalCreated;
        public event EventHandler<TerminalChangedEventArgs>? TerminalClosed;
        public event EventHandler<ActiveTerminalChangedEventArgs>? ActiveTerminalChanged;
        public event EventHandler<TerminalOutputEventArgs>? TerminalOutput;
        public event EventHandler<TerminalExitEventArgs>? TerminalExited;
        public event EventHandler<TerminalDimensionsChangedEventArgs>? TerminalDimensionsChanged;

        public Task<TerminalInfo> CreateAsync(TerminalCreateRequest request, CancellationToken ct)
        {
            return Task.FromResult(new TerminalInfo(Guid.NewGuid(), request.Title ?? "terminal"));
        }

        public Task SendTextAsync(Guid terminalId, string text, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TerminalInfo>> GetTerminalsAsync(CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<TerminalInfo>>(Array.Empty<TerminalInfo>());
        }

        public Task<Guid?> GetActiveTerminalIdAsync(CancellationToken ct)
        {
            return Task.FromResult<Guid?>(null);
        }

        public Task<bool> CloseAsync(Guid terminalId, CancellationToken ct)
        {
            return Task.FromResult(false);
        }

        public Task<TaskExecutionResult> RunTaskAsync(TaskExecutionRequest request, CancellationToken ct)
        {
            return Task.FromResult(new TaskExecutionResult(
                request.TaskId,
                0,
                Array.Empty<string>(),
                Array.Empty<TaskProblemMatch>()));
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

    private sealed class StubWorkspaceService : IWorkspaceService
    {
        public Task<WorkspaceModel> LoadSolutionAsync(string solutionPath, CancellationToken ct = default)
        {
            return Task.FromResult(new WorkspaceModel
            {
                Projects = Array.Empty<ProjectModel>()
            });
        }

        public Task<WorkspaceModel> LoadProjectAsync(string projectPath, CancellationToken ct = default)
        {
            return Task.FromResult(new WorkspaceModel
            {
                Projects = Array.Empty<ProjectModel>()
            });
        }

        public WorkspaceModel CreateStandaloneWorkspace(string xamlFilePath)
        {
            return new WorkspaceModel
            {
                Projects = Array.Empty<ProjectModel>()
            };
        }
    }
}

#pragma warning restore CS0067
