using System.Collections.Concurrent;
using System.Text.Json;

namespace XamlVisualEditor.Extensions.Hosting.Mcp;

/// <summary>Routes MCP JSON-RPC requests to handlers.</summary>
public sealed class McpRequestRouter
{
    private readonly ConcurrentDictionary<string, Func<McpRequestContext, JsonElement?, CancellationToken, Task<object?>>> _handlers = new(StringComparer.Ordinal);
    private readonly McpSessionRegistry _sessions;

    public McpRequestRouter(McpSessionRegistry sessions)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    public void Register(string method, Func<McpRequestContext, JsonElement?, CancellationToken, Task<object?>> handler)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("Method name is required.", nameof(method));
        }

        _handlers[method] = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public bool TryRemove(string method)
    {
        return _handlers.TryRemove(method, out _);
    }

    public async Task<object?> DispatchAsync(string method, McpRequestContext context, JsonElement? parameters, CancellationToken ct)
    {
        if (!_handlers.TryGetValue(method, out Func<McpRequestContext, JsonElement?, CancellationToken, Task<object?>>? handler))
        {
            throw new McpJsonRpcException(-32601, "Method not found");
        }

        if (string.IsNullOrWhiteSpace(context.SessionToken)
            && parameters is JsonElement paramsElement
            && paramsElement.ValueKind == JsonValueKind.Object
            && paramsElement.TryGetProperty("sessionToken", out JsonElement tokenElement))
        {
            context = new McpRequestContext(context.Connection, tokenElement.GetString());
        }

        context.Session = _sessions.Resolve(context);
        return await handler(context, parameters, ct).ConfigureAwait(false);
    }
}

/// <summary>Context for MCP request handling.</summary>
public sealed class McpRequestContext
{
    public McpRequestContext(McpJsonRpcConnection? connection, string? sessionToken)
    {
        Connection = connection;
        SessionToken = sessionToken;
    }

    public McpJsonRpcConnection? Connection { get; }

    public string? SessionToken { get; }

    public McpSessionInfo? Session { get; set; }
}

/// <summary>JSON-RPC error wrapper.</summary>
public sealed class McpJsonRpcException : Exception
{
    public McpJsonRpcException(int code, string message) : base(message)
    {
        Code = code;
    }

    public int Code { get; }
}
