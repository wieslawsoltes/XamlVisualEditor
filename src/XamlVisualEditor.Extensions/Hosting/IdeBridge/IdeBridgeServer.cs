using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Extensions.Hosting.IdeBridge;

/// <summary>Configures the IDE bridge server.</summary>
public sealed class IdeBridgeServerOptions
{
    /// <summary>Gets or sets whether stdio transport is enabled.</summary>
    public bool EnableStdio { get; set; } = true;

    /// <summary>Gets or sets the TCP port to listen on.</summary>
    public int? TcpPort { get; set; }

    /// <summary>Gets or sets the Unix domain socket path to listen on.</summary>
    public string? UnixSocketPath { get; set; }
}

/// <summary>Hosts IDE bridge JSON-RPC connections.</summary>
public sealed class IdeBridgeServer : IAsyncDisposable
{
    private readonly IdeBridgeServerOptions _options;
    private readonly IReadOnlyList<IIdeBridgeRequestHandler> _handlers;
    private readonly List<IdeBridgeJsonRpcConnection> _connections = new();
    private readonly List<Task> _acceptLoops = new();
    private readonly object _sync = new();
    private CancellationTokenSource? _cts;
    private TcpListener? _tcpListener;
    private Socket? _unixListener;
    private int _connectionCount;

    /// <summary>Raised when the connection count changes.</summary>
    public event EventHandler<IdeBridgeConnectionChangedEventArgs>? ConnectionChanged;

    /// <summary>Gets the active connection count.</summary>
    public int ConnectionCount => _connectionCount;

    /// <summary>Creates the IDE bridge server.</summary>
    public IdeBridgeServer(IdeBridgeServerOptions options, IEnumerable<IIdeBridgeRequestHandler> handlers)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _handlers = handlers?.ToList() ?? throw new ArgumentNullException(nameof(handlers));
    }

    /// <summary>Starts listening on configured transports.</summary>
    public void Start(CancellationToken ct)
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (_options.EnableStdio)
        {
            StartStdio(_cts.Token);
        }

        if (_options.TcpPort is not null)
        {
            StartTcp(_options.TcpPort.Value, _cts.Token);
        }

        if (!string.IsNullOrWhiteSpace(_options.UnixSocketPath))
        {
            StartUnixSocket(_options.UnixSocketPath, _cts.Token);
        }
    }

    private void StartStdio(CancellationToken ct)
    {
        Stream input = Console.OpenStandardInput();
        Stream output = Console.OpenStandardOutput();
        IdeBridgeJsonRpcConnection connection = CreateConnection(input, output, ownsStreams: false, ct);
        RegisterHandlers(connection);
        connection.Start(ct);
    }

    private void StartTcp(int port, CancellationToken ct)
    {
        _tcpListener = new TcpListener(IPAddress.Loopback, port);
        _tcpListener.Start();
        _acceptLoops.Add(Task.Run(() => AcceptTcpLoopAsync(_tcpListener, ct), ct));
    }

    private void StartUnixSocket(string socketPath, CancellationToken ct)
    {
        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
        }

        _unixListener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _unixListener.Bind(new UnixDomainSocketEndPoint(socketPath));
        _unixListener.Listen(backlog: 10);
        _acceptLoops.Add(Task.Run(() => AcceptUnixLoopAsync(_unixListener, ct), ct));
    }

    private async Task AcceptTcpLoopAsync(TcpListener listener, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                NetworkStream stream = client.GetStream();
                _ = HandleClientAsync(stream, stream, ownsStreams: true, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task AcceptUnixLoopAsync(Socket listener, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                Socket socket = await listener.AcceptAsync(ct).ConfigureAwait(false);
                NetworkStream stream = new(socket, ownsSocket: true);
                _ = HandleClientAsync(stream, stream, ownsStreams: true, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private Task HandleClientAsync(Stream input, Stream output, bool ownsStreams, CancellationToken ct)
    {
        IdeBridgeJsonRpcConnection connection = CreateConnection(input, output, ownsStreams, ct);
        RegisterHandlers(connection);
        connection.Start(ct);
        return Task.CompletedTask;
    }

    private IdeBridgeJsonRpcConnection CreateConnection(Stream input, Stream output, bool ownsStreams, CancellationToken ct)
    {
        IdeBridgeJsonRpcConnection connection = new(input, output, ownsStreams);
        connection.Disconnected += _ => RemoveConnection(connection);
        AddConnection(connection);
        return connection;
    }

    private void RegisterHandlers(IdeBridgeJsonRpcConnection connection)
    {
        foreach (IIdeBridgeRequestHandler handler in _handlers)
        {
            handler.Register(connection);
        }
    }

    private void AddConnection(IdeBridgeJsonRpcConnection connection)
    {
        lock (_sync)
        {
            _connections.Add(connection);
            _connectionCount = _connections.Count;
        }

        ConnectionChanged?.Invoke(this, new IdeBridgeConnectionChangedEventArgs(_connectionCount, DateTimeOffset.UtcNow));
    }

    private void RemoveConnection(IdeBridgeJsonRpcConnection connection)
    {
        lock (_sync)
        {
            _connections.Remove(connection);
            _connectionCount = _connections.Count;
        }

        ConnectionChanged?.Invoke(this, new IdeBridgeConnectionChangedEventArgs(_connectionCount, DateTimeOffset.UtcNow));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _tcpListener?.Stop();
        _unixListener?.Dispose();

        Task[] loops;
        lock (_sync)
        {
            loops = _acceptLoops.ToArray();
        }

        if (loops.Length > 0)
        {
            try
            {
                await Task.WhenAll(loops).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        IdeBridgeJsonRpcConnection[] connections;
        lock (_sync)
        {
            connections = _connections.ToArray();
            _connections.Clear();
            _connectionCount = 0;
        }

        foreach (IdeBridgeJsonRpcConnection connection in connections)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        _cts?.Dispose();
    }
}

/// <summary>Provides connection count changes.</summary>
public sealed class IdeBridgeConnectionChangedEventArgs : EventArgs
{
    public IdeBridgeConnectionChangedEventArgs(int connectionCount, DateTimeOffset timestamp)
    {
        ConnectionCount = connectionCount;
        Timestamp = timestamp;
    }

    public int ConnectionCount { get; }

    public DateTimeOffset Timestamp { get; }
}
