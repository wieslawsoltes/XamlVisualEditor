using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp;

public sealed class AcpService : IAcpService
{
    private readonly IAcpAgentHostFactory _hostFactory;
    private readonly IAcpSettings? _settings;
    private readonly AcpFileSystemHandler _fileSystemHandler = new();
    private readonly AcpTerminalManager _terminalManager = new();
    private Func<AcpPermissionRequest, CancellationToken, Task<AcpPermissionOutcome>>? _permissionHandler;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AcpAgentHost? _host;
    private string? _activeSessionId;

    public AcpService(IAcpAgentHostFactory hostFactory, IAcpSettings? settings = null)
    {
        _hostFactory = hostFactory;
        _settings = settings;
    }

    public bool IsConnected => _host is not null;

    public string? ActiveSessionId => _activeSessionId;

    public event Action<string>? StderrReceived;

    public event Action<string, JsonElement?>? NotificationReceived;

    public async Task ConnectAsync(AcpAgentProcessOptions options, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_host is not null)
            {
                return;
            }

            AcpAgentHost host = await _hostFactory.StartAsync(options, ct).ConfigureAwait(false);
            host.StderrReceived += HandleStderr;
            host.Client.NotificationReceived += HandleNotification;
            _fileSystemHandler.Register(host.Client);
            _terminalManager.Register(host.Client);
            host.Client.RegisterRequestHandler("session/request_permission", HandlePermissionRequestAsync);
            _host = host;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ConnectMockAgentAsync(CancellationToken ct)
    {
        string? mockPath = _settings?.MockAgentPath;
        if (string.IsNullOrWhiteSpace(mockPath))
        {
            throw new InvalidOperationException("Mock agent path is not configured.");
        }

        if (!File.Exists(mockPath))
        {
            throw new FileNotFoundException("Mock agent executable not found.", mockPath);
        }

        AcpAgentProcessOptions options = new()
        {
            FileName = "dotnet",
            Arguments = $"\"{mockPath}\""
        };

        await ConnectAsync(options, ct).ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_host is null)
            {
                return;
            }

            _host.StderrReceived -= HandleStderr;
            _host.Client.NotificationReceived -= HandleNotification;
            _fileSystemHandler.Unregister(_host.Client);
            _terminalManager.Unregister(_host.Client);
            _host.Client.TryRemoveRequestHandler("session/request_permission");
            await _terminalManager.ReleaseAllAsync().ConfigureAwait(false);
            await _host.DisposeAsync().ConfigureAwait(false);
            _host = null;
            _activeSessionId = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<JsonElement> InitializeAsync(object? parameters, CancellationToken ct)
    {
        return await SendRequestAsync("initialize", parameters, ct).ConfigureAwait(false);
    }

    public async Task<string> CreateSessionAsync(object? parameters, CancellationToken ct)
    {
        JsonElement result = await SendRequestAsync("session/new", parameters, ct).ConfigureAwait(false);
        _activeSessionId = result.TryGetProperty("sessionId", out JsonElement sessionIdElement)
            ? sessionIdElement.GetString()
            : null;
        return _activeSessionId ?? string.Empty;
    }

    public async Task<JsonElement> PromptAsync(string sessionId, object? content, CancellationToken ct)
    {
        var payload = new
        {
            sessionId,
            content
        };

        return await SendRequestAsync("session/prompt", payload, ct).ConfigureAwait(false);
    }

    public async Task CancelAsync(string sessionId, CancellationToken ct)
    {
        var payload = new
        {
            sessionId
        };

        await SendNotificationAsync("session/cancel", payload, ct).ConfigureAwait(false);
    }

    public async Task<JsonElement> SendRequestAsync(string method, object? parameters, CancellationToken ct)
    {
        AcpProtocolClient? client = _host?.Client;
        if (client is null)
        {
            throw new InvalidOperationException("ACP client is not connected.");
        }

        return await client.SendRequestAsync(method, parameters, ct).ConfigureAwait(false);
    }

    public async Task SendNotificationAsync(string method, object? parameters, CancellationToken ct)
    {
        AcpProtocolClient? client = _host?.Client;
        if (client is null)
        {
            throw new InvalidOperationException("ACP client is not connected.");
        }

        await client.SendNotificationAsync(method, parameters, ct).ConfigureAwait(false);
    }

    public void SetPermissionHandler(Func<AcpPermissionRequest, CancellationToken, Task<AcpPermissionOutcome>>? handler)
    {
        _permissionHandler = handler;
    }

    private async Task<JsonElement?> HandlePermissionRequestAsync(JsonElement? parameters, CancellationToken ct)
    {
        AcpPermissionRequest request = AcpPermissionRequest.Parse(parameters);
        AcpPermissionOutcome outcome = _permissionHandler is null
            ? AcpPermissionOutcome.Cancelled()
            : await _permissionHandler(request, ct).ConfigureAwait(false);

        if (outcome.IsCancelled || string.IsNullOrWhiteSpace(outcome.SelectedOptionId))
        {
            return JsonSerializer.SerializeToElement(new
            {
                outcome = new
                {
                    cancelled = new { }
                }
            });
        }

        return JsonSerializer.SerializeToElement(new
        {
            outcome = new
            {
                selected = new
                {
                    optionId = outcome.SelectedOptionId
                }
            }
        });
    }

    private void HandleStderr(string message)
    {
        StderrReceived?.Invoke(message);
    }

    private void HandleNotification(string method, JsonElement? parameters)
    {
        NotificationReceived?.Invoke(method, parameters);
    }
}
