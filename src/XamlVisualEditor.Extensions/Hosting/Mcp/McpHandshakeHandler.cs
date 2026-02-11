using System;
using System.Text.Json;

namespace XamlVisualEditor.Extensions.Hosting.Mcp;

/// <summary>Handles MCP handshake requests.</summary>
public sealed class McpHandshakeHandler : IMcpRequestHandler
{
    private readonly McpSessionService _sessionService;
    private readonly McpSessionRegistry _registry;

    public McpHandshakeHandler(McpSessionService sessionService, McpSessionRegistry registry)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public void Register(McpRequestRouter router)
    {
        router.Register(McpProtocol.InitializeMethod, (context, paramsElement, ct) => HandleInitializeAsync(context, paramsElement, ct));
    }

    private async Task<object?> HandleInitializeAsync(McpRequestContext context, JsonElement? parameters, CancellationToken ct)
    {
        McpInitializeParams? request = parameters is null
            ? null
            : JsonSerializer.Deserialize<McpInitializeParams>(parameters.Value.GetRawText(), McpMessageFraming.SerializerOptions);

        McpInitializeOutcome outcome = await _sessionService.InitializeAsync(request, ct).ConfigureAwait(false);
        McpInitializeResult result = outcome.Result;
        McpSessionInfo session = new(result.WorkspaceId, outcome.AccessLevel, result.SessionToken);

        if (context.Connection is not null)
        {
            _registry.Set(context.Connection, session);
        }

        _registry.Set(result.SessionToken, session);

        return result;
    }
}
