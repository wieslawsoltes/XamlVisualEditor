using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia.Remote.Protocol;
using Avalonia.Remote.Protocol.Designer;
using Avalonia.Remote.Protocol.Viewport;

namespace XamlVisualEditor.Shell.ViewModels;

public sealed class PreviewerTcpSession : IDisposable
{
    private readonly string _xamlFilePath;
    private readonly Action<string, string>? _log;
    private readonly IDisposable _listener;
    private readonly object _gate = new();
    private IAvaloniaRemoteTransportConnection? _connection;
    private string? _pendingXaml;
    private string? _pendingAssemblyPath;
    private string? _pendingProjectPath;
    private double? _pendingViewportWidth;
    private double? _pendingViewportHeight;
    private double _viewportWidth = 800;
    private double _viewportHeight = 600;
    private FrameMessage? _lastFrame;

    public PreviewerTcpSession(string xamlFilePath, Action<string, string>? log)
    {
        _xamlFilePath = xamlFilePath;
        _log = log;
        Port = GetFreeTcpPort();
        SessionId = Guid.NewGuid().ToString();
        _listener = new BsonTcpTransport().Listen(IPAddress.Loopback, Port, OnConnected);
    }

    public int Port { get; }

    public string SessionId { get; }

    public event Action<PreviewerErrorInfo>? ErrorReceived;
    public event Action<IAvaloniaRemoteTransportConnection?>? ConnectionChanged;
    public event Action<FrameMessage>? FrameReceived;
    public event Action<RequestViewportResizeMessage>? ViewportResizeRequested;

    public IAvaloniaRemoteTransportConnection? Connection
    {
        get
        {
            lock (_gate)
            {
                return _connection;
            }
        }
    }

    public Task SendUpdateXamlAsync(
        string xaml,
        string assemblyPath,
        string xamlFileProjectPath,
        double? viewportWidth,
        double? viewportHeight)
    {
        IAvaloniaRemoteTransportConnection? connection;
        lock (_gate)
        {
            if (_connection is null)
            {
                _pendingXaml = xaml;
                _pendingAssemblyPath = assemblyPath;
                _pendingProjectPath = xamlFileProjectPath;
                _pendingViewportWidth = viewportWidth;
                _pendingViewportHeight = viewportHeight;
                return Task.CompletedTask;
            }

            connection = _connection;
        }

        UpdateViewportIfNeeded(connection, viewportWidth, viewportHeight, 96, 96);
        return connection.Send(new UpdateXamlMessage
        {
            Xaml = xaml,
            AssemblyPath = assemblyPath,
            XamlFileProjectPath = xamlFileProjectPath
        });
    }

    public void Dispose()
    {
        _listener.Dispose();
        lock (_gate)
        {
            _connection?.Dispose();
            _connection = null;
        }
        ConnectionChanged?.Invoke(null);
    }

    public FrameMessage? LastFrame
    {
        get
        {
            lock (_gate)
            {
                return _lastFrame;
            }
        }
    }

    public void UpdateViewport(double width, double height, double dpiX, double dpiY)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        IAvaloniaRemoteTransportConnection? connection;
        lock (_gate)
        {
            _pendingViewportWidth = width;
            _pendingViewportHeight = height;
            connection = _connection;
        }

        if (connection is null)
        {
            return;
        }

        UpdateViewportIfNeeded(connection, width, height, dpiX, dpiY);
    }

    private void OnConnected(IAvaloniaRemoteTransportConnection connection)
    {
        lock (_gate)
        {
            _connection?.Dispose();
            _connection = connection;
        }

        _log?.Invoke("Info", "Previewer transport connected");
        ConnectionChanged?.Invoke(connection);

        connection.OnMessage += OnMessage;
        connection.OnException += (_, ex) =>
        {
            string message = $"Previewer transport error: {ex.Message}";
            _log?.Invoke("Error", message);
            ErrorReceived?.Invoke(new PreviewerErrorInfo(message, null, null, _xamlFilePath));
        };
        connection.Start();

        SendPreflightMessages(connection);
        _log?.Invoke("Info", "Previewer preflight sent");
        TrySendPendingUpdate(connection);
    }

    private void OnMessage(IAvaloniaRemoteTransportConnection _, object message)
    {
        if (message is StartDesignerSessionMessage)
        {
            _log?.Invoke("Info", "Previewer session started");
            return;
        }

        if (message is FrameMessage frame)
        {
            lock (_gate)
            {
                _lastFrame = frame;
            }

            _connection?.Send(new FrameReceivedMessage
            {
                SequenceId = frame.SequenceId
            });

            FrameReceived?.Invoke(frame);
            return;
        }

        if (message is RequestViewportResizeMessage resize)
        {
            _log?.Invoke("Info", $"Previewer viewport resize requested: {resize.Width}x{resize.Height}");
            ViewportResizeRequested?.Invoke(resize);
            return;
        }

        if (message is UpdateXamlResultMessage updateResult)
        {
            if (!string.IsNullOrWhiteSpace(updateResult.Error))
            {
                _log?.Invoke("Error", updateResult.Error);
                int? line = TryGetPositiveInt(updateResult, "LineNumber", "Line");
                int? column = TryGetPositiveInt(updateResult, "LinePosition", "Position", "Column");
                ErrorReceived?.Invoke(new PreviewerErrorInfo(updateResult.Error, line, column, _xamlFilePath));
                return;
            }

            _log?.Invoke("Info", "Previewer XAML applied");
            return;
        }

        _log?.Invoke("Info", $"Previewer message: {message.GetType().Name}");
    }

    private static void SendPreflightMessages(IAvaloniaRemoteTransportConnection connection)
    {
        connection.Send(new ClientSupportedPixelFormatsMessage
        {
            Formats = new[] { PixelFormat.Bgra8888 }
        });

        connection.Send(new ClientRenderInfoMessage
        {
            DpiX = 96,
            DpiY = 96
        });

        SendViewportMessage(connection, 800, 600, 96, 96);
    }

    private void TrySendPendingUpdate(IAvaloniaRemoteTransportConnection connection)
    {
        string? xaml;
        string? assemblyPath;
        string? projectPath;
        double? viewportWidth;
        double? viewportHeight;

        lock (_gate)
        {
            xaml = _pendingXaml;
            assemblyPath = _pendingAssemblyPath;
            projectPath = _pendingProjectPath;
            viewportWidth = _pendingViewportWidth;
            viewportHeight = _pendingViewportHeight;
            _pendingXaml = null;
            _pendingAssemblyPath = null;
            _pendingProjectPath = null;
            _pendingViewportWidth = null;
            _pendingViewportHeight = null;
        }

        if (string.IsNullOrWhiteSpace(xaml) || string.IsNullOrWhiteSpace(assemblyPath))
        {
            return;
        }

        UpdateViewportIfNeeded(connection, viewportWidth, viewportHeight, 96, 96);
        _ = connection.Send(new UpdateXamlMessage
        {
            Xaml = xaml,
            AssemblyPath = assemblyPath,
            XamlFileProjectPath = projectPath
        });
    }

    private void UpdateViewportIfNeeded(
        IAvaloniaRemoteTransportConnection connection,
        double? width,
        double? height,
        double dpiX,
        double dpiY)
    {
        if (width is null || height is null || width.Value <= 0 || height.Value <= 0)
        {
            return;
        }

        if (Math.Abs(_viewportWidth - width.Value) < 0.01
            && Math.Abs(_viewportHeight - height.Value) < 0.01)
        {
            return;
        }

        _viewportWidth = width.Value;
        _viewportHeight = height.Value;
        SendViewportMessage(connection, _viewportWidth, _viewportHeight, dpiX, dpiY);
    }

    private static int? TryGetPositiveInt(object source, params string[] propertyNames)
    {
        foreach (string name in propertyNames)
        {
            System.Reflection.PropertyInfo? property = source.GetType().GetProperty(name);
            if (property is null)
            {
                continue;
            }

            object? value = property.GetValue(source);
            if (value is int intValue && intValue > 0)
            {
                return intValue;
            }

        }

        return null;
    }

    private static void SendViewportMessage(
        IAvaloniaRemoteTransportConnection connection,
        double width,
        double height,
        double dpiX,
        double dpiY)
    {
        connection.Send(new ClientViewportAllocatedMessage
        {
            Width = width,
            Height = height,
            DpiX = dpiX,
            DpiY = dpiY
        });
    }

    private static int GetFreeTcpPort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
