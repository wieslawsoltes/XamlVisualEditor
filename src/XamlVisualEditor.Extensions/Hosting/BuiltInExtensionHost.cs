using System.Reflection;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>
/// Activates in-proc extensions registered via DI.
/// </summary>
public sealed class BuiltInExtensionHost : IDisposable
{
    private readonly IEnumerable<IXveExtension> _extensions;
    private readonly ICommands _commands;
    private readonly ICommandMetadataRegistry _commandMetadata;
    private readonly IExtensionContributionRegistry _contributions;
    private readonly Debugging.IDebuggerServiceRegistry _debuggerRegistry;
    private readonly IDesignerHost _designer;
    private readonly IWorkspace _workspace;
    private readonly IWorkspaceModel _workspaceModel;
    private readonly IWorkspaceInfo _workspaceInfo;
    private readonly ISystemIconService _systemIcons;
    private readonly IWindow _window;
    private readonly IDialogHost _dialogHost;
    private readonly IWorkspaceHost _workspaceHost;
    private readonly IViews _views;
    private readonly IExtensionLanguageServices _languageServices;
    private readonly ILanguageNavigationService _navigation;
    private readonly INavigationHistoryService _navigationHistory;
    private readonly IAnimationEditorHost _animationEditor;
    private readonly ICollaborationHost _collaboration;
    private readonly ICollaborationPanelHost _collaborationPanel;
    private readonly IDebugSettingsHost _debugSettings;
    private readonly ILspSettingsHost _lspSettings;
    private readonly IEditorServices _editor;
    private readonly IDiagnosticsService _diagnostics;
    private readonly IPropertyEditorRegistry _propertyEditors;
    private readonly ITerminalBridge _terminal;
    private readonly IExtensionViewHost _viewHost;
    private readonly ISettings _settings;
    private readonly List<IList<IDisposable>> _extensionSubscriptions = new();
    private bool _activated;

    public BuiltInExtensionHost(
        IEnumerable<IXveExtension> extensions,
        ICommands commands,
        ICommandMetadataRegistry commandMetadata,
        IExtensionContributionRegistry contributions,
        Debugging.IDebuggerServiceRegistry debuggerRegistry,
        IDesignerHost designer,
        IWorkspace workspace,
        IWorkspaceModel workspaceModel,
        IWorkspaceInfo workspaceInfo,
        ISystemIconService systemIcons,
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
        IExtensionViewHost viewHost,
        ISettings settings)
    {
        _extensions = extensions;
        _commands = commands;
        _commandMetadata = commandMetadata;
        _contributions = contributions;
        _debuggerRegistry = debuggerRegistry;
        _designer = designer;
        _workspace = workspace;
        _workspaceModel = workspaceModel;
        _workspaceInfo = workspaceInfo;
        _systemIcons = systemIcons;
        _window = window;
        _dialogHost = dialogHost;
        _workspaceHost = workspaceHost;
        _views = views;
        _languageServices = languageServices;
        _navigation = navigation;
        _navigationHistory = navigationHistory;
        _animationEditor = animationEditor;
        _collaboration = collaboration;
        _collaborationPanel = collaborationPanel;
        _debugSettings = debugSettings;
        _lspSettings = lspSettings;
        _editor = editor;
        _diagnostics = diagnostics;
        _propertyEditors = propertyEditors;
        _terminal = terminal;
        _viewHost = viewHost;
        _settings = settings;
    }

    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        if (_activated)
        {
            return;
        }

        _activated = true;
        foreach (IXveExtension extension in _extensions)
        {
            ExtensionContext context = CreateContext(extension);
            await extension.ActivateAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        foreach (IList<IDisposable> subscriptions in _extensionSubscriptions)
        {
            foreach (IDisposable disposable in subscriptions)
            {
                disposable.Dispose();
            }
        }

        _extensionSubscriptions.Clear();
    }

    private ExtensionContext CreateContext(IXveExtension extension)
    {
        string extensionId = ResolveExtensionId(extension);
        string extensionPath = ResolveExtensionPath(extension);
        List<IDisposable> subscriptions = new();
        _extensionSubscriptions.Add(subscriptions);

        IExtensionStorage storage = new InMemoryExtensionStorage();
        IExtensionLogger logger = new NullExtensionLogger();
        IExtensionPermissions permissions = new ExtensionPermissionService(extensionId, _settings, _window);

        return new ExtensionContext(
            extensionId,
            extensionPath,
            _commands,
            _commandMetadata,
            _contributions,
            _debuggerRegistry,
            _designer,
            _workspace,
            _workspaceModel,
            _workspaceInfo,
            _systemIcons,
            _window,
            _dialogHost,
            _workspaceHost,
            _views,
            _languageServices,
            _navigation,
            _navigationHistory,
            _animationEditor,
            _collaboration,
            _collaborationPanel,
            _debugSettings,
            _lspSettings,
            _editor,
            _diagnostics,
            _propertyEditors,
            _terminal,
            permissions,
            _viewHost,
            _settings,
            storage,
            logger,
            subscriptions);
    }

    private static string ResolveExtensionId(IXveExtension extension)
    {
        Assembly assembly = extension.GetType().Assembly;
        return assembly.GetName().Name ?? extension.GetType().FullName ?? "UnknownExtension";
    }

    private static string ResolveExtensionPath(IXveExtension extension)
    {
        Assembly assembly = extension.GetType().Assembly;
        string? location = assembly.Location;
        if (string.IsNullOrWhiteSpace(location))
        {
            return AppContext.BaseDirectory;
        }

        return Path.GetDirectoryName(location) ?? AppContext.BaseDirectory;
    }

    private sealed class NullExtensionLogger : IExtensionLogger
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
