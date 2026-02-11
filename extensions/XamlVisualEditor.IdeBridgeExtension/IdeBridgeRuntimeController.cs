using System;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting.IdeBridge;

namespace XamlVisualEditor.IdeBridgeExtension;

/// <summary>Manages IDE bridge server lifecycle.</summary>
public sealed class IdeBridgeRuntimeController : IAsyncDisposable
{
    private const string SettingsSection = "ideBridge";
    private readonly ISettings _settings;
    private readonly IWorkspace _workspace;
    private readonly IdeBridgePermissionService _permissions;
    private readonly IdeBridgeSessionRegistry _registry;
    private readonly IIdeBridgeRequestHandler[] _handlers;
    private readonly object _gate = new();
    private IdeBridgeServer? _server;
    private IdeBridgeSettings _currentSettings = new();
    private bool _suppressReload;
    private int _connectionCount;
    private DateTimeOffset? _lastConnectionAt;

    public IdeBridgeRuntimeController(
        ISettings settings,
        IdeBridgePermissionService permissions,
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
        _registry = new IdeBridgeSessionRegistry();

        IdeBridgeSessionService sessions = new(_permissions, workspaceInfo);
        IdeBridgeHandshakeHandler handshake = new(sessions, _registry);
        IdeBridgeCoreHandler coreHandler = new(
            commands,
            workspace,
            workspaceInfo,
            window,
            editor,
            diagnostics,
            terminal,
            _registry);

        _handlers = new IIdeBridgeRequestHandler[] { handshake, coreHandler };
    }

    /// <summary>Gets the current settings.</summary>
    public IdeBridgeSettings CurrentSettings => _currentSettings;

    /// <summary>Gets whether the server is running.</summary>
    public bool IsRunning => _server is not null;

    /// <summary>Gets the active connection count.</summary>
    public int ConnectionCount => _connectionCount;

    /// <summary>Gets the last connection timestamp.</summary>
    public DateTimeOffset? LastConnectionAt => _lastConnectionAt;

    /// <summary>Gets the endpoint summary.</summary>
    public string EndpointSummary => BuildEndpointPreview(_currentSettings);

    /// <summary>Raised when status changes.</summary>
    public event EventHandler? StatusChanged;

    /// <summary>Loads settings and starts the server if enabled.</summary>
    public async Task InitializeAsync(CancellationToken ct)
    {
        _currentSettings = LoadSettings();
        _workspace.ConfigurationChanged += OnConfigurationChanged;
        if (_currentSettings.Enabled)
        {
            await StartAsync(ct).ConfigureAwait(false);
        }

        RaiseStatusChanged();
    }

    /// <summary>Applies settings and restarts the server.</summary>
    public async Task ApplySettingsAsync(IdeBridgeSettings settings, CancellationToken ct)
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

    /// <summary>Restarts the server using current settings.</summary>
    public async Task RestartAsync(CancellationToken ct)
    {
        await StopAsync().ConfigureAwait(false);
        if (_currentSettings.Enabled)
        {
            await StartAsync(ct).ConfigureAwait(false);
        }

        RaiseStatusChanged();
    }

    /// <summary>Stops the server.</summary>
    public async Task StopAsync()
    {
        IdeBridgeServer? server = null;
        lock (_gate)
        {
            server = _server;
            _server = null;
        }

        if (server is not null)
        {
            server.ConnectionChanged -= OnConnectionChanged;
            await server.DisposeAsync().ConfigureAwait(false);
        }

        _connectionCount = 0;
        RaiseStatusChanged();
    }

    private Task StartAsync(CancellationToken ct)
    {
        IdeBridgeServerOptions options = BuildOptions(_currentSettings);
        IdeBridgeServer server = new(options, _handlers);
        server.ConnectionChanged += OnConnectionChanged;
        server.Start(ct);

        lock (_gate)
        {
            _server = server;
        }

        _connectionCount = server.ConnectionCount;
        RaiseStatusChanged();

        return Task.CompletedTask;
    }

    private IdeBridgeSettings LoadSettings()
    {
        IdeBridgeSettings? settings = _settings.Get<IdeBridgeSettings>(SettingsSection);
        return settings ?? new IdeBridgeSettings();
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

        IdeBridgeSettings latest = LoadSettings();
        if (latest == _currentSettings)
        {
            return;
        }

        _currentSettings = latest;
        _ = RestartAsync(CancellationToken.None);
    }

    private void OnConnectionChanged(object? sender, IdeBridgeConnectionChangedEventArgs e)
    {
        _connectionCount = e.ConnectionCount;
        _lastConnectionAt = e.Timestamp;
        RaiseStatusChanged();
    }

    private void RaiseStatusChanged()
    {
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IdeBridgeServerOptions BuildOptions(IdeBridgeSettings settings)
    {
        IdeBridgeServerOptions options = new();
        string transport = (settings.Transport ?? "stdio").ToLowerInvariant();
        switch (transport)
        {
            case "tcp":
                options.EnableStdio = false;
                options.TcpPort = settings.TcpPort > 0 ? settings.TcpPort : 4711;
                break;
            case "unix":
                options.EnableStdio = false;
                options.UnixSocketPath = settings.UnixSocketPath;
                break;
            default:
                options.EnableStdio = true;
                break;
        }

        return options;
    }

    /// <summary>Builds an endpoint preview string.</summary>
    public static string BuildEndpointPreview(IdeBridgeSettings settings)
    {
        string transport = (settings.Transport ?? "stdio").ToLowerInvariant();
        return transport switch
        {
            "tcp" => $"tcp://127.0.0.1:{(settings.TcpPort > 0 ? settings.TcpPort : 4711)}",
            "unix" => string.IsNullOrWhiteSpace(settings.UnixSocketPath) ? "unix://(unset)" : "unix://" + settings.UnixSocketPath,
            _ => "stdio"
        };
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _workspace.ConfigurationChanged -= OnConfigurationChanged;
        return new ValueTask(StopAsync());
    }
}
