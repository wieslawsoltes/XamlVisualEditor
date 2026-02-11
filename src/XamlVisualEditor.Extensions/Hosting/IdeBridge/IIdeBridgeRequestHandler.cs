namespace XamlVisualEditor.Extensions.Hosting.IdeBridge;

/// <summary>Registers JSON-RPC handlers for the IDE bridge.</summary>
public interface IIdeBridgeRequestHandler
{
    /// <summary>Registers handlers on a connection.</summary>
    void Register(IdeBridgeJsonRpcConnection connection);
}
