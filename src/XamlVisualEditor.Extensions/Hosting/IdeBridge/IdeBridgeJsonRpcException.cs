namespace XamlVisualEditor.Extensions.Hosting.IdeBridge;

/// <summary>Represents a JSON-RPC error.</summary>
public sealed class IdeBridgeJsonRpcException : Exception
{
    /// <summary>Creates a JSON-RPC error.</summary>
    public IdeBridgeJsonRpcException(int code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Gets the JSON-RPC error code.</summary>
    public int Code { get; }
}
