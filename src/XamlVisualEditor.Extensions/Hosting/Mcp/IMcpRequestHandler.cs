namespace XamlVisualEditor.Extensions.Hosting.Mcp;

/// <summary>Registers MCP request handlers.</summary>
public interface IMcpRequestHandler
{
    void Register(McpRequestRouter router);
}
