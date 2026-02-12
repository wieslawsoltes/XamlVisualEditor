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
        IExtensionContributionRegistry contributions,
        Debugging.IDebuggerServiceRegistry debuggerRegistry,
        IWorkspace workspace,
        IWorkspaceInfo workspaceInfo,
        IWindow window,
        IDialogHost dialogHost,
        IWorkspaceHost workspaceHost,
        IViews views,
        IExtensionLanguageServices languageServices,
        IEditorServices editor,
        IDiagnosticsService diagnostics,
        ITerminalBridge terminal,
        ISettings settings,
        IExtensionStorage storage,
        IExtensionLogger logger,
        IList<IDisposable> subscriptions)
    {
        ExtensionId = extensionId;
        ExtensionPath = extensionPath;
        Commands = commands;
        Contributions = contributions;
        DebuggerRegistry = debuggerRegistry;
        Workspace = workspace;
        WorkspaceInfo = workspaceInfo;
        Window = window;
        DialogHost = dialogHost;
        WorkspaceHost = workspaceHost;
        Views = views;
        LanguageServices = languageServices;
        Editor = editor;
        Diagnostics = diagnostics;
        Terminal = terminal;
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

    /// <summary>Gets the contribution registry.</summary>
    public IExtensionContributionRegistry Contributions { get; }

    /// <summary>Gets the debugger registry.</summary>
    public Debugging.IDebuggerServiceRegistry DebuggerRegistry { get; }

    /// <summary>Gets the workspace service.</summary>
    public IWorkspace Workspace { get; }

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

    /// <summary>Gets the editor services.</summary>
    public IEditorServices Editor { get; }

    /// <summary>Gets the diagnostics service.</summary>
    public IDiagnosticsService Diagnostics { get; }

    /// <summary>Gets the terminal bridge service.</summary>
    public ITerminalBridge Terminal { get; }

    /// <summary>Gets the settings service.</summary>
    public ISettings Settings { get; }

    /// <summary>Gets the extension storage service.</summary>
    public IExtensionStorage Storage { get; }

    /// <summary>Gets the extension logger.</summary>
    public IExtensionLogger Logger { get; }

    /// <summary>Gets the disposable subscriptions collection.</summary>
    public IList<IDisposable> Subscriptions { get; }
}
