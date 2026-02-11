using System.Reflection;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>
/// Activates in-proc extensions registered via DI.
/// </summary>
public sealed class BuiltInExtensionHost : IDisposable
{
    private readonly IEnumerable<IXveExtension> _extensions;
    private readonly ICommands _commands;
    private readonly IExtensionContributionRegistry _contributions;
    private readonly IWorkspace _workspace;
    private readonly IWorkspaceInfo _workspaceInfo;
    private readonly IWindow _window;
    private readonly IViews _views;
    private readonly IExtensionLanguageServices _languageServices;
    private readonly IEditorServices _editor;
    private readonly ISettings _settings;
    private readonly List<IList<IDisposable>> _extensionSubscriptions = new();
    private bool _activated;

    public BuiltInExtensionHost(
        IEnumerable<IXveExtension> extensions,
        ICommands commands,
        IExtensionContributionRegistry contributions,
        IWorkspace workspace,
        IWorkspaceInfo workspaceInfo,
        IWindow window,
        IViews views,
        IExtensionLanguageServices languageServices,
        IEditorServices editor,
        ISettings settings)
    {
        _extensions = extensions;
        _commands = commands;
        _contributions = contributions;
        _workspace = workspace;
        _workspaceInfo = workspaceInfo;
        _window = window;
        _views = views;
        _languageServices = languageServices;
        _editor = editor;
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

        return new ExtensionContext(
            extensionId,
            extensionPath,
            _commands,
            _contributions,
            _workspace,
            _workspaceInfo,
            _window,
            _views,
            _languageServices,
            _editor,
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
