using System;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting.Mcp;

namespace XamlVisualEditor.McpExtension;

/// <summary>Manages MCP server lifecycle.</summary>
public sealed class McpRuntimeController : IAsyncDisposable
{
    private const string SettingsSection = "mcp";
    private readonly ISettings _settings;
    private readonly IWorkspace _workspace;
    private readonly McpPermissionService _permissions;
    private readonly McpSessionRegistry _registry;
    private readonly IMcpRequestHandler[] _handlers;
    private readonly object _gate = new();
    private McpServer? _server;
    private CancellationTokenSource? _serverLifetime;
    private McpSettings _currentSettings = new();
    private bool _suppressReload;
    private int _connectionCount;
    private DateTimeOffset? _lastConnectionAt;

    public McpRuntimeController(
        ISettings settings,
        McpPermissionService permissions,
        ICommands commands,
        IWorkspace workspace,
        IWorkspaceInfo workspaceInfo,
        IWindow window,
        IEditorServices editor,
        IDiagnosticsService diagnostics,
        ITerminalBridge terminal)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _registry = new McpSessionRegistry();

        McpSessionService sessions = new(_permissions, workspaceInfo);
        McpHandshakeHandler handshake = new(sessions, _registry);
        McpToolCatalog catalog = new(commands, workspace, workspaceInfo, window, editor, diagnostics, terminal, settings);
        McpToolsHandler tools = new(catalog);
        McpResourcesHandler resources = new(workspace, workspaceInfo, editor);
        McpPromptsHandler prompts = new(workspaceInfo, editor, diagnostics);

        _handlers = new IMcpRequestHandler[] { handshake, tools, resources, prompts };
    }

    public McpSettings CurrentSettings => _currentSettings;

    public bool IsRunning => _server is not null;

    public int ConnectionCount => _connectionCount;

    public DateTimeOffset? LastConnectionAt => _lastConnectionAt;

    public string EndpointSummary => BuildEndpointPreview(_currentSettings);

    public event EventHandler? StatusChanged;

    public async Task InitializeAsync(CancellationToken ct)
    {
        _currentSettings = LoadSettings();
        _workspace.ConfigurationChanged += OnConfigurationChanged;
        if (_currentSettings.Enabled)
        {
            await StartAsync().ConfigureAwait(false);
        }

        RaiseStatusChanged();
    }

    public async Task ApplySettingsAsync(McpSettings settings, CancellationToken ct)
    {
        _currentSettings = settings;
        _suppressReload = true;
        try
        {
            await _settings.UpdateAsync(SettingsSection, settings, SettingsTarget.User, ct).ConfigureAwait(false);
        }
        finally
        {
            _suppressReload = false;
        }

        await RestartAsync(ct).ConfigureAwait(false);
    }

    public async Task RestartAsync(CancellationToken ct)
    {
        await StopAsync().ConfigureAwait(false);
        if (_currentSettings.Enabled)
        {
            await StartAsync().ConfigureAwait(false);
        }

        RaiseStatusChanged();
    }

    public async Task StopAsync()
    {
        McpServer? server = null;
        CancellationTokenSource? lifetime = null;
        lock (_gate)
        {
            server = _server;
            _server = null;
            lifetime = _serverLifetime;
            _serverLifetime = null;
        }

        if (lifetime is not null)
        {
            lifetime.Cancel();
            lifetime.Dispose();
        }

        if (server is not null)
        {
            server.ConnectionChanged -= OnConnectionChanged;
            await server.DisposeAsync().ConfigureAwait(false);
        }

        _connectionCount = 0;
        RaiseStatusChanged();
    }

    private Task StartAsync()
    {
        CancellationTokenSource lifetime = new();
        McpServerOptions options = BuildOptions(_currentSettings);
        McpServer server = new(options, _handlers, _registry);
        server.ConnectionChanged += OnConnectionChanged;
        server.Start(lifetime.Token);

        lock (_gate)
        {
            _server = server;
            _serverLifetime = lifetime;
        }

        _connectionCount = server.ConnectionCount;
        RaiseStatusChanged();
        return Task.CompletedTask;
    }

    private McpSettings LoadSettings()
    {
        McpSettings? settings = _settings.Get<McpSettings>(SettingsSection);
        return settings ?? new McpSettings();
    }

    private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs args)
    {
        if (_suppressReload)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(args.Section) && !args.Section.StartsWith(SettingsSection, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        McpSettings latest = LoadSettings();
        if (latest == _currentSettings)
        {
            return;
        }

        _currentSettings = latest;
        _ = RestartAsync(CancellationToken.None);
    }

    private void OnConnectionChanged(object? sender, McpConnectionChangedEventArgs e)
    {
        _connectionCount = e.ConnectionCount;
        _lastConnectionAt = e.Timestamp;
        RaiseStatusChanged();
    }

    private void RaiseStatusChanged()
    {
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static McpServerOptions BuildOptions(McpSettings settings)
    {
        McpServerOptions options = new();
        string transport = (settings.Transport ?? "both").ToLowerInvariant();
        options.EnableStdio = transport is "stdio" or "both";
        options.EnableHttp = transport is "http" or "both";
        options.HttpPort = settings.HttpPort > 0 ? settings.HttpPort : 4712;
        options.HttpPath = string.IsNullOrWhiteSpace(settings.HttpPath) ? "/mcp/" : settings.HttpPath;
        return options;
    }

    public static string BuildEndpointPreview(McpSettings settings)
    {
        string transport = (settings.Transport ?? "both").ToLowerInvariant();
        string path = string.IsNullOrWhiteSpace(settings.HttpPath) ? "/mcp/" : settings.HttpPath;
        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            path = "/" + path;
        }
        if (!path.EndsWith("/", StringComparison.Ordinal))
        {
            path += "/";
        }

        string http = $"http://127.0.0.1:{(settings.HttpPort > 0 ? settings.HttpPort : 4712)}{path}";
        return transport switch
        {
            "stdio" => "stdio",
            "http" => http,
            _ => "stdio + " + http
        };
    }

    public ValueTask DisposeAsync()
    {
        _workspace.ConfigurationChanged -= OnConfigurationChanged;
        return new ValueTask(StopAsync());
    }
}
