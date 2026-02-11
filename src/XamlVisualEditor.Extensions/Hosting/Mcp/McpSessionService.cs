using System.IO;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Extensions.Hosting.Mcp;

/// <summary>Resolves sessions and permissions for MCP clients.</summary>
public sealed class McpSessionService
{
    private readonly McpPermissionService _permissions;
    private readonly IWorkspaceInfo _workspaceInfo;

    public McpSessionService(McpPermissionService permissions, IWorkspaceInfo workspaceInfo)
    {
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _workspaceInfo = workspaceInfo ?? throw new ArgumentNullException(nameof(workspaceInfo));
    }

    public async Task<McpInitializeOutcome> InitializeAsync(McpInitializeParams? request, CancellationToken ct)
    {
        string workspaceId = ResolveWorkspaceId(request?.WorkspaceId);
        McpWorkspacePermissionState? state = await _permissions
            .AuthorizeAsync(workspaceId, request?.SessionToken, ct)
            .ConfigureAwait(false);

        if (state is null)
        {
            throw new McpJsonRpcException(-32000, "Permission denied.");
        }

        McpServerInfo serverInfo = new("XamlVisualEditor", ExtensionSdkInfo.ApiVersion.ToString());
        McpServerCapabilities capabilities = new(
            new McpToolsCapability(),
            new McpResourcesCapability(),
            new McpPromptsCapability(),
            new McpLoggingCapability());

        McpInitializeResult result = new(
            McpProtocol.ProtocolVersion,
            serverInfo,
            capabilities,
            workspaceId,
            state.SessionToken);

        return new McpInitializeOutcome(result, state.AccessLevel);
    }

    private string ResolveWorkspaceId(string? requestedId)
    {
        string? path = requestedId ?? _workspaceInfo.WorkspacePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return "default";
        }

        return Path.GetFullPath(path);
    }
}
