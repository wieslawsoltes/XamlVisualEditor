using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Debugging;
using XamlVisualEditor.Extensions.Hosting;
using XamlVisualEditor.ToolboxExtension;
using System.Reactive.Threading.Tasks;
using ToolboxExtensionEntry = XamlVisualEditor.ToolboxExtension.ToolboxExtension;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

#pragma warning disable CS0067

public sealed class ToolboxExtensionTests
{
    [Fact]
    public async Task ToolboxPanel_UsesSettingsCatalogAndCommandArgs()
    {
        RecordingCommands commands = new();
        InMemoryWindow window = new();
        InMemorySettingsStore settings = new();

        await settings.UpdateAsync(
            "toolbox.catalog",
            new List<ToolboxCatalogEntry>
            {
                new()
                {
                    DisplayName = "Custom Border",
                    CommandId = "toolbox.insertSelected",
                    CommandArguments = new List<string>
                    {
                        "Border",
                        "https://github.com/avaloniaui",
                        "11111111-1111-1111-1111-111111111111"
                    }
                }
            },
            SettingsTarget.User,
            CancellationToken.None);

        ToolboxPanelViewModel viewModel = new(commands, settings);
        Assert.Single(viewModel.Items);
        Assert.Equal("Custom Border", viewModel.Items[0].DisplayName);

        viewModel.SelectedItem = viewModel.Items[0];
        await viewModel.InsertSelectedCommand.Execute().ToTask();

        Assert.Equal("toolbox.insertSelected", commands.LastCommandId);
        Assert.Equal(3, commands.LastArguments?.Count ?? 0);
        Assert.Equal("Border", commands.LastArguments?[0] as string);
        Assert.Equal("https://github.com/avaloniaui", commands.LastArguments?[1] as string);
        Assert.Equal("11111111-1111-1111-1111-111111111111", commands.LastArguments?[2] as string);
    }

    [Fact]
    public async Task InsertSelected_UsesCommandArguments()
    {
        CommandRegistry commands = new();
        ExtensionContext context = CreateContext(commands, out StubDesignerHost designerHost, out InMemoryWindow window);

        ToolboxExtensionEntry extension = new();
        await extension.ActivateAsync(context, CancellationToken.None);

        await commands.ExecuteAsync(
            "toolbox.insertSelected",
            new object?[] { "StackPanel", "https://github.com/avaloniaui" },
            CancellationToken.None);

        Assert.Equal("StackPanel", designerHost.LastTypeName);
        Assert.Equal("https://github.com/avaloniaui", designerHost.LastXmlNamespace);
        Assert.Null(designerHost.LastParentNodeId);
    }

    [Fact]
    public async Task InsertSelected_ShowsWarning_WhenArgumentsMissing()
    {
        CommandRegistry commands = new();
        ExtensionContext context = CreateContext(commands, out StubDesignerHost _, out InMemoryWindow window);

        ToolboxExtensionEntry extension = new();
        await extension.ActivateAsync(context, CancellationToken.None);

        await commands.ExecuteAsync(
            "toolbox.insertSelected",
            new object?[] { "Button" },
            CancellationToken.None);

        Assert.Contains(window.Messages, message => message.Contains("expects arguments"));
    }

    private static ExtensionContext CreateContext(
        ICommands commands,
        out StubDesignerHost designerHost,
        out InMemoryWindow window)
    {
        window = new InMemoryWindow();
        designerHost = new StubDesignerHost();
        StubWorkspaceInfo workspaceInfo = new();
        StubWorkspaceCommands workspaceCommands = new();
        StubEditorServices editorServices = new();
        StubNavigationService navigation = new();
        StubNavigationHistoryService navigationHistory = new();
        StubAnimationEditorHost animationHost = new();
        StubCollaborationHost collaborationHost = new();
        StubDebugSettingsHost debugSettingsHost = new();
        StubLspSettingsHost lspSettingsHost = new();
        StubExtensionViewHost viewHost = new();
        StubExtensionPermissions permissions = new();

        return new ExtensionContext(
            "test.toolbox",
            "/tmp",
            commands,
            new CommandMetadataRegistry(),
            new ExtensionContributionRegistry(),
            new DebuggerServiceRegistry(),
            designerHost,
            new InMemoryWorkspace(),
            new WorkspaceModelAdapter(workspaceInfo, workspaceCommands, new StubWorkspaceService()),
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
            collaborationHost,
            debugSettingsHost,
            lspSettingsHost,
            editorServices,
            new StubDiagnostics(),
            new PropertyEditorRegistry(),
            new StubTerminalBridge(),
            permissions,
            viewHost,
            new InMemorySettingsStore(),
            new InMemoryExtensionStorage(),
            new StubExtensionLogger(),
            new List<IDisposable>());
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

    private sealed class StubDesignerHost : IDesignerHost
    {
        public string? LastTypeName { get; private set; }
        public string? LastXmlNamespace { get; private set; }
        public string? LastParentNodeId { get; private set; }

        public string? ActiveDocumentPath => "test.axaml";

        public event EventHandler<DesignerDocumentChangedEventArgs>? ActiveDocumentChanged;

        public event EventHandler<DesignerSelectionChangedEventArgs>? SelectionChanged;

        public Task<IReadOnlyList<DesignerNodeSummary>> GetSelectedNodesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DesignerNodeSummary>>(Array.Empty<DesignerNodeSummary>());

        public Task<IReadOnlyList<DesignerNodeSummary>> GetVisualTreeAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DesignerNodeSummary>>(Array.Empty<DesignerNodeSummary>());

        public Task<IReadOnlyList<DesignerNodeSummary>> GetLogicalTreeAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DesignerNodeSummary>>(Array.Empty<DesignerNodeSummary>());

        public Task<IReadOnlyList<DesignerPropertyInfo>> GetPropertiesAsync(string nodeId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DesignerPropertyInfo>>(Array.Empty<DesignerPropertyInfo>());

        public Task<IReadOnlyList<DesignerEventInfo>> GetEventsAsync(string nodeId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DesignerEventInfo>>(Array.Empty<DesignerEventInfo>());

        public Task<bool> SetPropertyAsync(string nodeId, string propertyName, string? value, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<string?> InsertElementAsync(string typeName, string xmlNamespace, string? parentNodeId, CancellationToken cancellationToken)
        {
            LastTypeName = typeName;
            LastXmlNamespace = xmlNamespace;
            LastParentNodeId = parentNodeId;
            return Task.FromResult<string?>("node-1");
        }

        public Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<string?> WrapNodeAsync(string nodeId, string wrapperTypeName, string wrapperXmlNamespace, CancellationToken cancellationToken)
            => Task.FromResult<string?>("wrap-1");

        public Task<bool> SelectNodeAsync(string nodeId, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<bool> RevealNodeAsync(string nodeId, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public IDesignerTransaction BeginTransaction(string name) => new StubTransaction();

        private sealed class StubTransaction : IDesignerTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public void Dispose() { }
        }
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

    private sealed class StubWorkspaceInfo : IWorkspaceInfo
    {
        public string? WorkspacePath { get; set; }
        public event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;
    }

    private sealed class StubWorkspaceCommands : IWorkspaceCommands
    {
        public bool HasWorkspace => true;
        public Task LoadWorkspaceAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RestoreWorkspaceAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task BuildWorkspaceAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RebuildWorkspaceAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CleanWorkspaceAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubEditorServices : IEditorServices
    {
        public IEditorDocument? ActiveDocument => null;
        public IReadOnlyList<IEditorDocument> GetOpenDocuments() => Array.Empty<IEditorDocument>();
        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, CancellationToken ct) => Task.FromResult<IEditorDocument?>(null);
        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, EditorDocumentOpenBehavior behavior, CancellationToken ct) => Task.FromResult<IEditorDocument?>(null);
        public Task<bool> OpenLocationAsync(LanguageLocation location, CancellationToken ct) => Task.FromResult(false);
        public event EventHandler<EditorActiveDocumentChangedEventArgs>? ActiveDocumentChanged;
    }

    private sealed class StubDiagnostics : IDiagnosticsService
    {
        public event EventHandler<DiagnosticsChannelsChangedEventArgs>? ChannelsChanged;

        public event EventHandler<DiagnosticsChannelPublishedEventArgs>? DiagnosticsChannelPublished;

        public event EventHandler<DiagnosticsSnapshotPublishedEventArgs>? DiagnosticsSnapshotPublished;

        public event EventHandler<DiagnosticsPublishedEventArgs>? DiagnosticsPublished;

        public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(string? filePath, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());

        public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(DiagnosticsQuery query, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());

        public Task<IReadOnlyList<DiagnosticsDocumentSnapshot>> GetDiagnosticsSnapshotAsync(
            DiagnosticsQuery query,
            CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DiagnosticsDocumentSnapshot>>(Array.Empty<DiagnosticsDocumentSnapshot>());

        public Task<IReadOnlyList<DiagnosticsChannelInfo>> GetChannelsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DiagnosticsChannelInfo>>(Array.Empty<DiagnosticsChannelInfo>());

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
            => Task.FromResult(new TerminalInfo(Guid.NewGuid(), request.Title ?? "terminal"));

        public Task SendTextAsync(Guid terminalId, string text, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<TerminalInfo>> GetTerminalsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<TerminalInfo>>(Array.Empty<TerminalInfo>());

        public Task<Guid?> GetActiveTerminalIdAsync(CancellationToken ct)
            => Task.FromResult<Guid?>(null);

        public Task<bool> CloseAsync(Guid terminalId, CancellationToken ct)
            => Task.FromResult(false);

        public Task<TaskExecutionResult> RunTaskAsync(TaskExecutionRequest request, CancellationToken ct)
            => Task.FromResult(new TaskExecutionResult(
                request.TaskId,
                0,
                Array.Empty<string>(),
                Array.Empty<TaskProblemMatch>()));
    }

    private sealed class StubExtensionLogger : IExtensionLogger
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }

    private sealed class RecordingCommands : ICommands
    {
        public string? LastCommandId { get; private set; }
        public IReadOnlyList<object?>? LastArguments { get; private set; }

        public IDisposable Register(string commandId, Func<CommandContext, Task> handler)
        {
            return new Registration();
        }

        public Task ExecuteAsync(string commandId, IReadOnlyList<object?>? args, CancellationToken cancellationToken)
        {
            LastCommandId = commandId;
            LastArguments = args;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetCommandsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        private sealed class Registration : IDisposable
        {
            public void Dispose()
            {
            }
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
