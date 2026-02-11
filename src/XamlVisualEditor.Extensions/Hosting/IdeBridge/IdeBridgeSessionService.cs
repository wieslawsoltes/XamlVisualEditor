using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Extensions.Hosting.IdeBridge;

/// <summary>Resolves sessions and permissions for IDE bridge clients.</summary>
public sealed class IdeBridgeSessionService
{
    private readonly IdeBridgePermissionService _permissions;
    private readonly IWorkspaceInfo _workspaceInfo;

    /// <summary>Creates the session service.</summary>
    public IdeBridgeSessionService(IdeBridgePermissionService permissions, IWorkspaceInfo workspaceInfo)
    {
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _workspaceInfo = workspaceInfo ?? throw new ArgumentNullException(nameof(workspaceInfo));
    }

    /// <summary>Initializes a session for a client.</summary>
    public async Task<BridgeInitializeResult> InitializeAsync(BridgeInitializeParams? request, CancellationToken ct)
    {
        string workspaceId = ResolveWorkspaceId(request?.WorkspaceId);
        IdeBridgeWorkspacePermissionState? state = await _permissions
            .AuthorizeAsync(workspaceId, request?.SessionToken, ct)
            .ConfigureAwait(false);

        if (state is null)
        {
            throw new IdeBridgeJsonRpcException(-32000, "Permission denied.");
        }

        return new BridgeInitializeResult(
            IdeBridgeProtocol.ProtocolVersion,
            state.Capabilities,
            workspaceId,
            state.SessionToken);
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
