using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>
/// Activates in-proc extensions registered via DI and enabled installed extension
/// packages (their manifest's main assembly is loaded into the default load context so
/// contract types unify with the editor's own).
/// </summary>
public sealed class BuiltInExtensionHost : IDisposable
{
    private readonly IEnumerable<IXveExtension> _extensions;
    private readonly IExtensionPackageStore _packageStore;
    private readonly IExtensionStateStore _stateStore;
    private readonly ILogger<BuiltInExtensionHost> _logger;
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
        ISettings settings,
        IExtensionPackageStore packageStore,
        IExtensionStateStore stateStore,
        ILogger<BuiltInExtensionHost>? logger = null)
    {
        _extensions = extensions;
        _packageStore = packageStore;
        _stateStore = stateStore;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BuiltInExtensionHost>.Instance;
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
            ExtensionContext context = CreateContext(ResolveExtensionId(extension), ResolveExtensionPath(extension));
            await extension.ActivateAsync(context, cancellationToken).ConfigureAwait(false);
        }

        await ActivateInstalledPackagesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ActivateInstalledPackagesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ExtensionPackageInfo> installed;
        try
        {
            installed = await _packageStore.GetInstalledAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Installed extension packages could not be listed");
            return;
        }

        foreach (ExtensionPackageInfo package in installed)
        {
            string extensionId = package.Manifest.ExtensionId;
            try
            {
                bool enabled = await _stateStore.GetEnabledAsync(extensionId, cancellationToken).ConfigureAwait(false);
                if (!enabled || string.IsNullOrWhiteSpace(package.Manifest.Main))
                {
                    continue;
                }

                string contentDirectory = InstalledExtensionPackages.ExtractContent(package.PackagePath);
                string? mainAssemblyPath = InstalledExtensionPackages.ResolveMainAssemblyPath(contentDirectory, package.Manifest.Main);
                if (mainAssemblyPath is null)
                {
                    _logger.LogWarning("Extension package {ExtensionId}: main assembly '{Main}' not found", extensionId, package.Manifest.Main);
                    continue;
                }

                Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(mainAssemblyPath);
                foreach (Type type in InstalledExtensionPackages.FindExtensionTypes(assembly))
                {
                    if (Activator.CreateInstance(type) is not IXveExtension extension)
                    {
                        continue;
                    }

                    ExtensionContext context = CreateContext(extensionId, contentDirectory);
                    await extension.ActivateAsync(context, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Activated extension package {ExtensionId} ({Type})", extensionId, type.FullName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Extension package {ExtensionId} failed to activate", extensionId);
            }
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

    private ExtensionContext CreateContext(string extensionId, string extensionPath)
    {
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
