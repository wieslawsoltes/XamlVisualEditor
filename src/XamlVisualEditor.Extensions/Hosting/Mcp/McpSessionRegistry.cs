using System.Collections.Concurrent;

namespace XamlVisualEditor.Extensions.Hosting.Mcp;

/// <summary>Stores MCP sessions by connection or token.</summary>
public sealed class McpSessionRegistry
{
    private readonly ConcurrentDictionary<McpJsonRpcConnection, McpSessionInfo> _byConnection = new();
    private readonly ConcurrentDictionary<string, McpSessionInfo> _byToken = new(StringComparer.Ordinal);

    public void Set(McpJsonRpcConnection connection, McpSessionInfo session)
    {
        _byConnection[connection] = session;
        _byToken[session.SessionToken] = session;
    }

    public void Set(string sessionToken, McpSessionInfo session)
    {
        _byToken[sessionToken] = session;
    }

    public void Remove(McpJsonRpcConnection connection)
    {
        _byConnection.TryRemove(connection, out _);
    }

    public McpSessionInfo? Resolve(McpRequestContext context)
    {
        if (context.Connection is not null && _byConnection.TryGetValue(context.Connection, out McpSessionInfo? session))
        {
            return session;
        }

        if (!string.IsNullOrWhiteSpace(context.SessionToken) && _byToken.TryGetValue(context.SessionToken, out McpSessionInfo? tokenSession))
        {
            return tokenSession;
        }

        return null;
    }
}
