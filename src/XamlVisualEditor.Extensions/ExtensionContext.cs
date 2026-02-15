namespace XamlVisualEditor.Extensions;

/// <summary>Entry point for extensions to interact with the host.</summary>
public interface IXveExtension
{
    /// <summary>Activates the extension.</summary>
    Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken);
}

/// <summary>Provides access to host services during activation.</summary>
public sealed class ExtensionContext
{
    /// <summary>Creates a new extension context.</summary>
    public ExtensionContext(
        string extensionId,
        string extensionPath,
        ICommands commands,
        ICommandMetadataRegistry commandMetadata,
        IExtensionContributionRegistry contributions,
        Debugging.IDebuggerServiceRegistry debuggerRegistry,
        IDesignerHost designer,
        IWorkspace workspace,
        IWorkspaceModel workspaceModel,
        IWorkspaceInfo workspaceInfo,
        IWindow window,
        IDialogHost dialogHost,
        IWorkspaceHost workspaceHost,
        IViews views,
        IExtensionLanguageServices languageServices,
        ILanguageNavigationService navigation,
        INavigationHistoryService navigationHistory,
        IAnimationEditorHost animationEditor,
        ICollaborationHost collaboration,
        ICollaborationPanelHost collaborationPanel,
        IDebugSettingsHost debugSettings,
        ILspSettingsHost lspSettings,
        IEditorServices editor,
        IDiagnosticsService diagnostics,
        IPropertyEditorRegistry propertyEditors,
        ITerminalBridge terminal,
        IExtensionPermissions permissions,
        IExtensionViewHost viewHost,
        ISettings settings,
        IExtensionStorage storage,
        IExtensionLogger logger,
        IList<IDisposable> subscriptions)
    {
        ExtensionId = extensionId;
        ExtensionPath = extensionPath;
        Commands = commands;
        CommandMetadata = commandMetadata;
        Contributions = contributions;
        DebuggerRegistry = debuggerRegistry;
        Designer = designer;
        Workspace = workspace;
        WorkspaceModel = workspaceModel;
        WorkspaceInfo = workspaceInfo;
        Window = window;
        DialogHost = dialogHost;
        WorkspaceHost = workspaceHost;
        Views = views;
        LanguageServices = languageServices;
        Navigation = navigation;
        NavigationHistory = navigationHistory;
        AnimationEditor = animationEditor;
        Collaboration = collaboration;
        CollaborationPanel = collaborationPanel;
        DebugSettings = debugSettings;
        LspSettings = lspSettings;
        Editor = editor;
        Diagnostics = diagnostics;
        PropertyEditors = propertyEditors;
        Terminal = terminal;
        Permissions = permissions;
        ViewHost = viewHost;
        Settings = settings;
        Storage = storage;
        Logger = logger;
        Subscriptions = subscriptions;
    }

    /// <summary>Gets the unique extension id.</summary>
    public string ExtensionId { get; }

    /// <summary>Gets the extension installation path.</summary>
    public string ExtensionPath { get; }

    /// <summary>Gets the command service.</summary>
    public ICommands Commands { get; }

    /// <summary>Gets command metadata registration service.</summary>
    public ICommandMetadataRegistry CommandMetadata { get; }

    /// <summary>Gets the contribution registry.</summary>
    public IExtensionContributionRegistry Contributions { get; }

    /// <summary>Gets the debugger registry.</summary>
    public Debugging.IDebuggerServiceRegistry DebuggerRegistry { get; }

    /// <summary>Gets the designer host service.</summary>
    public IDesignerHost Designer { get; }

    /// <summary>Gets the workspace service.</summary>
    public IWorkspace Workspace { get; }

    /// <summary>Gets the workspace model service.</summary>
    public IWorkspaceModel WorkspaceModel { get; }

    /// <summary>Gets the workspace info.</summary>
    public IWorkspaceInfo WorkspaceInfo { get; }

    /// <summary>Gets the window service.</summary>
    public IWindow Window { get; }

    /// <summary>Gets the dialog host service.</summary>
    public IDialogHost DialogHost { get; }

    /// <summary>Gets the workspace host service.</summary>
    public IWorkspaceHost WorkspaceHost { get; }

    /// <summary>Gets the views service.</summary>
    public IViews Views { get; }

    /// <summary>Gets the language services.</summary>
    public IExtensionLanguageServices LanguageServices { get; }

    /// <summary>Gets the language navigation services.</summary>
    public ILanguageNavigationService Navigation { get; }

    /// <summary>Gets the navigation history services.</summary>
    public INavigationHistoryService NavigationHistory { get; }

    /// <summary>Gets the animation editor host services.</summary>
    public IAnimationEditorHost AnimationEditor { get; }

    /// <summary>Gets collaboration session services.</summary>
    public ICollaborationHost Collaboration { get; }

    /// <summary>Gets the collaboration panel host services.</summary>
    public ICollaborationPanelHost CollaborationPanel { get; }

    /// <summary>Gets the debug settings host services.</summary>
    public IDebugSettingsHost DebugSettings { get; }

    /// <summary>Gets the LSP settings host services.</summary>
    public ILspSettingsHost LspSettings { get; }

    /// <summary>Gets the editor services.</summary>
    public IEditorServices Editor { get; }

    /// <summary>Gets the diagnostics service.</summary>
    public IDiagnosticsService Diagnostics { get; }

    /// <summary>Gets the property editor registry.</summary>
    public IPropertyEditorRegistry PropertyEditors { get; }

    /// <summary>Gets the terminal bridge service.</summary>
    public ITerminalBridge Terminal { get; }

    /// <summary>Gets runtime permission services for the extension.</summary>
    public IExtensionPermissions Permissions { get; }

    /// <summary>Gets the extension view host service.</summary>
    public IExtensionViewHost ViewHost { get; }

    /// <summary>Gets the settings service.</summary>
    public ISettings Settings { get; }

    /// <summary>Gets the extension storage service.</summary>
    public IExtensionStorage Storage { get; }

    /// <summary>Gets the extension logger.</summary>
    public IExtensionLogger Logger { get; }

    /// <summary>Gets the disposable subscriptions collection.</summary>
    public IList<IDisposable> Subscriptions { get; }
}
