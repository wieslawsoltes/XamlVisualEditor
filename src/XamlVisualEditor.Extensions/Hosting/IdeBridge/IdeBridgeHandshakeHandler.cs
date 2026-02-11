using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Extensions.Hosting.IdeBridge;

/// <summary>Handles bridge initialization and shutdown.</summary>
public sealed class IdeBridgeHandshakeHandler : IIdeBridgeRequestHandler
{
    private readonly IdeBridgeSessionService _sessionService;
    private readonly IdeBridgeSessionRegistry _registry;

    /// <summary>Creates the handshake handler.</summary>
    public IdeBridgeHandshakeHandler(IdeBridgeSessionService sessionService, IdeBridgeSessionRegistry registry)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc />
    public void Register(IdeBridgeJsonRpcConnection connection)
    {
        connection.RegisterRequestHandler(IdeBridgeProtocol.InitializeMethod, (paramsElement, ct) => HandleInitializeAsync(connection, paramsElement, ct));
        connection.RegisterRequestHandler(IdeBridgeProtocol.ShutdownMethod, (paramsElement, ct) => HandleShutdownAsync(connection, ct));
    }

    private async Task<object?> HandleInitializeAsync(IdeBridgeJsonRpcConnection connection, JsonElement? parameters, CancellationToken ct)
    {
        BridgeInitializeParams? request = parameters is null
            ? null
            : JsonSerializer.Deserialize<BridgeInitializeParams>(parameters.Value.GetRawText(), IdeBridgeMessageFraming.SerializerOptions);

        BridgeInitializeResult result = await _sessionService.InitializeAsync(request, ct).ConfigureAwait(false);
        _registry.Set(connection, new IdeBridgeSessionInfo(result.WorkspaceId, result.Capabilities, result.SessionToken));
        return result;
    }

    private Task<object?> HandleShutdownAsync(IdeBridgeJsonRpcConnection connection, CancellationToken ct)
    {
        _registry.Remove(connection);
        return Task.FromResult<object?>(new { });
    }
}
