namespace XamlVisualEditor.Extensions.Hosting.IdeBridge;

/// <summary>IDE bridge settings stored in user configuration.</summary>
public sealed record IdeBridgeSettings
{
    /// <summary>Gets whether the bridge is enabled.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the transport type (stdio, tcp, unix).</summary>
    public string? Transport { get; init; } = "stdio";

    /// <summary>Gets the TCP port to listen on.</summary>
    public int TcpPort { get; init; } = 4711;

    /// <summary>Gets the Unix domain socket path.</summary>
    public string? UnixSocketPath { get; init; }
}
