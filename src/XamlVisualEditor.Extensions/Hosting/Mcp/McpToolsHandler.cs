using System;
using System.Text.Json;

namespace XamlVisualEditor.Extensions.Hosting.Mcp;

/// <summary>Handles MCP tools requests.</summary>
public sealed class McpToolsHandler : IMcpRequestHandler
{
    private readonly McpToolCatalog _catalog;

    public McpToolsHandler(McpToolCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public void Register(McpRequestRouter router)
    {
        router.Register(McpProtocol.ToolsListMethod, (_, _, _) => Task.FromResult<object?>(new McpToolsListResult(_catalog.ListTools())));
        router.Register(McpProtocol.ToolsCallMethod, (context, paramsElement, ct) => HandleToolCallAsync(context, paramsElement, ct));
    }

    private async Task<object?> HandleToolCallAsync(McpRequestContext context, JsonElement? parameters, CancellationToken ct)
    {
        McpSessionInfo session = RequireSession(context);
        McpToolCallParams request = Deserialize<McpToolCallParams>(parameters);

        if (!_catalog.TryGet(request.Name, out McpToolCatalog.McpTool tool))
        {
            throw new McpJsonRpcException(-32601, "Tool not found.");
        }

        if (tool.RequiresWrite && session.AccessLevel != McpAccessLevel.Full)
        {
            throw new McpJsonRpcException(-32000, "Write permission denied.");
        }

        McpToolCallResult result = await tool.Handler(request.Arguments, ct).ConfigureAwait(false);
        return result;
    }

    private static McpSessionInfo RequireSession(McpRequestContext context)
    {
        if (context.Session is null)
        {
            throw new McpJsonRpcException(-32000, "Session not initialized.");
        }

        return context.Session;
    }

    private static T Deserialize<T>(JsonElement? args)
    {
        if (args is null)
        {
            throw new McpJsonRpcException(-32602, "Invalid params.");
        }

        return JsonSerializer.Deserialize<T>(args.Value.GetRawText(), McpMessageFraming.SerializerOptions)
            ?? throw new McpJsonRpcException(-32602, "Invalid params.");
    }
}
