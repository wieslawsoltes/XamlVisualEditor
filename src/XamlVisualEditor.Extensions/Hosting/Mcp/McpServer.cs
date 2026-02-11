using System;
using System.Linq;
using System.Net;
using System.Text.Json;

namespace XamlVisualEditor.Extensions.Hosting.Mcp;

/// <summary>Configures MCP server transports.</summary>
public sealed class McpServerOptions
{
    public bool EnableStdio { get; set; } = true;

    public bool EnableHttp { get; set; } = true;

    public int HttpPort { get; set; } = 4712;

    public string HttpPath { get; set; } = "/mcp/";
}

/// <summary>Hosts MCP JSON-RPC connections.</summary>
public sealed class McpServer : IAsyncDisposable
{
    private readonly McpServerOptions _options;
    private readonly IReadOnlyList<IMcpRequestHandler> _handlers;
    private readonly McpSessionRegistry _sessions;
    private readonly List<McpJsonRpcConnection> _connections = new();
    private readonly List<Task> _acceptLoops = new();
    private readonly object _sync = new();
    private CancellationTokenSource? _cts;
    private McpRequestRouter? _router;
    private McpHttpServer? _httpServer;

    public event EventHandler<McpConnectionChangedEventArgs>? ConnectionChanged;

    public int ConnectionCount => _connections.Count;

    public McpServer(McpServerOptions options, IEnumerable<IMcpRequestHandler> handlers, McpSessionRegistry sessions)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _handlers = handlers?.ToList() ?? throw new ArgumentNullException(nameof(handlers));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    public void Start(CancellationToken ct)
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _router = new McpRequestRouter(_sessions);
        foreach (IMcpRequestHandler handler in _handlers)
        {
            handler.Register(_router);
        }

        if (_options.EnableStdio)
        {
            StartStdio(_cts.Token);
        }

        if (_options.EnableHttp)
        {
            StartHttp(_cts.Token);
        }
    }

    private void StartStdio(CancellationToken ct)
    {
        if (_router is null)
        {
            return;
        }

        Stream input = Console.OpenStandardInput();
        Stream output = Console.OpenStandardOutput();
        McpJsonRpcConnection connection = new(input, output, _router, ownsStreams: false);
        connection.Disconnected += _ => RemoveConnection(connection);
        AddConnection(connection);
        connection.Start(ct);
    }

    private void StartHttp(CancellationToken ct)
    {
        if (_router is null)
        {
            return;
        }

        _httpServer = new McpHttpServer(_options.HttpPort, _options.HttpPath, _router);
        _acceptLoops.Add(_httpServer.StartAsync(ct));
    }

    private void AddConnection(McpJsonRpcConnection connection)
    {
        lock (_sync)
        {
            _connections.Add(connection);
        }

        ConnectionChanged?.Invoke(this, new McpConnectionChangedEventArgs(_connections.Count, DateTimeOffset.UtcNow));
    }

    private void RemoveConnection(McpJsonRpcConnection connection)
    {
        lock (_sync)
        {
            _connections.Remove(connection);
            _sessions.Remove(connection);
        }

        ConnectionChanged?.Invoke(this, new McpConnectionChangedEventArgs(_connections.Count, DateTimeOffset.UtcNow));
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        Task[] loops;
        lock (_sync)
        {
            loops = _acceptLoops.ToArray();
        }

        if (loops.Length > 0)
        {
            try
            {
                await Task.WhenAll(loops).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        McpJsonRpcConnection[] connections;
        lock (_sync)
        {
            connections = _connections.ToArray();
            _connections.Clear();
        }

        foreach (McpJsonRpcConnection connection in connections)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        if (_httpServer is not null)
        {
            await _httpServer.DisposeAsync().ConfigureAwait(false);
        }

        _cts?.Dispose();
    }

    private sealed class McpHttpServer : IAsyncDisposable
    {
        private readonly int _port;
        private readonly string _path;
        private readonly McpRequestRouter _router;
        private readonly HttpListener _listener = new();
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private CancellationTokenRegistration _shutdownRegistration;

        public McpHttpServer(int port, string path, McpRequestRouter router)
        {
            _port = port;
            string normalized = string.IsNullOrWhiteSpace(path) ? "/mcp/" : path;
            if (!normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = "/" + normalized;
            }
            if (!normalized.EndsWith("/", StringComparison.Ordinal))
            {
                normalized += "/";
            }

            _path = normalized;
            _router = router;
        }

        public Task StartAsync(CancellationToken ct)
        {
            if (_loop is not null)
            {
                return _loop;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            string prefix = $"http://127.0.0.1:{_port}{_path}";
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            _shutdownRegistration = _cts.Token.Register(static state =>
            {
                if (state is HttpListener listener)
                {
                    try
                    {
                        listener.Close();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (HttpListenerException)
                    {
                    }
                }
            }, _listener);
            _loop = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);
            return _loop;
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    HttpListenerContext context = await _listener.GetContextAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleRequestAsync(context, ct));
                }
            }
            catch (HttpListenerException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                response.Close();
                return;
            }

            using JsonDocument message = await JsonDocument.ParseAsync(request.InputStream, cancellationToken: ct).ConfigureAwait(false);
            JsonElement root = message.RootElement;

            if (!root.TryGetProperty("id", out JsonElement idElement) || !root.TryGetProperty("method", out JsonElement methodElement))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Close();
                return;
            }

            string? method = methodElement.GetString();
            if (string.IsNullOrWhiteSpace(method))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Close();
                return;
            }

            string? sessionToken = request.Headers["X-Mcp-Session"];
            McpRequestContext ctx = new(null, sessionToken);
            JsonElement? parameters = root.TryGetProperty("params", out JsonElement paramsElement)
                ? paramsElement.Clone()
                : null;

            if (!TryGetIdValue(idElement, out object? idValue) || idValue is null)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Close();
                return;
            }

            object payload;
            try
            {
                object? result = await _router.DispatchAsync(method, ctx, parameters, ct).ConfigureAwait(false);
                payload = new { jsonrpc = "2.0", id = idValue, result };
            }
            catch (McpJsonRpcException ex)
            {
                payload = new { jsonrpc = "2.0", id = idValue, error = new { code = ex.Code, message = ex.Message } };
            }
            catch (Exception ex)
            {
                payload = new { jsonrpc = "2.0", id = idValue, error = new { code = -32000, message = ex.Message } };
            }

            byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload, McpMessageFraming.SerializerOptions);
            response.ContentType = "application/json";
            response.ContentLength64 = body.Length;
            await response.OutputStream.WriteAsync(body, ct).ConfigureAwait(false);
            response.Close();
        }

        private static bool TryGetIdValue(JsonElement element, out object? id)
        {
            id = null;
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long numberId))
                    {
                        id = numberId;
                        return true;
                    }
                    return false;
                case JsonValueKind.String:
                    id = element.GetString();
                    return id is not null;
                default:
                    return false;
            }
        }

        public ValueTask DisposeAsync()
        {
            _cts?.Cancel();
            _shutdownRegistration.Dispose();
            _listener.Close();
            _cts?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>Provides connection count changes.</summary>
public sealed class McpConnectionChangedEventArgs : EventArgs
{
    public McpConnectionChangedEventArgs(int connectionCount, DateTimeOffset timestamp)
    {
        ConnectionCount = connectionCount;
        Timestamp = timestamp;
    }

    public int ConnectionCount { get; }

    public DateTimeOffset Timestamp { get; }
}
