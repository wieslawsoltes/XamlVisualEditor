using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Remote.Protocol;
using Avalonia.Remote.Protocol.Viewport;
using Avalonia.Threading;
using XamlVisualEditor.Shell.ViewModels;
using ProtocolPixelFormat = Avalonia.Remote.Protocol.Viewport.PixelFormat;
using SurfacePixelFormat = Avalonia.Platform.PixelFormat;

namespace XamlVisualEditor.App.Views;

public sealed class PreviewerSurfaceControl : Control
{
    public static readonly StyledProperty<PreviewerTcpSession?> SessionProperty =
        AvaloniaProperty.Register<PreviewerSurfaceControl, PreviewerTcpSession?>(nameof(Session));

    private PreviewerTcpSession? _session;
    private FrameMessage? _lastFrame;
    private WriteableBitmap? _bitmap;
    private byte[]? _rowBuffer;
    private DateTimeOffset? _lastFrameAt;
    private bool _isConnected;
    private DateTimeOffset? _lastConnectionAt;

    public PreviewerTcpSession? Session
    {
        get => GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    static PreviewerSurfaceControl()
    {
        SessionProperty.Changed.AddClassHandler<PreviewerSurfaceControl>(
            (control, args) => control.OnSessionChanged(args));
    }

    public override void Render(DrawingContext context)
    {
        if (_lastFrame is null || _lastFrame.Width == 0 || _lastFrame.Height == 0)
        {
            DrawStatusOverlay(context, "Waiting for previewer frames...");
            base.Render(context);
            return;
        }

        if (_bitmap is null
            || _bitmap.PixelSize.Width != _lastFrame.Width
            || _bitmap.PixelSize.Height != _lastFrame.Height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(_lastFrame.Width, _lastFrame.Height),
                new Vector(96, 96),
                SurfacePixelFormat.Bgra8888,
                AlphaFormat.Premul);
        }

        using (ILockedFramebuffer buffer = _bitmap.Lock())
        {
            CopyFrameToBuffer(_lastFrame, buffer);
        }

        Rect sourceRect = new Rect(0, 0, _bitmap.Size.Width, _bitmap.Size.Height);
        Rect destRect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.DrawImage(_bitmap, sourceRect, destRect);
        DrawStatusOverlay(context, BuildFrameStatus());
    }

    protected override void ArrangeCore(Rect finalRect)
    {
        base.ArrangeCore(finalRect);
        if (_session is null)
        {
            return;
        }

        _session.UpdateViewport(finalRect.Width, finalRect.Height, 96, 96);
    }

    private void OnSessionChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (args.OldValue is PreviewerTcpSession oldSession)
        {
            oldSession.ConnectionChanged -= OnConnectionChanged;
            oldSession.FrameReceived -= OnFrameReceived;
            oldSession.ViewportResizeRequested -= OnViewportResizeRequested;
        }

        if (args.NewValue is PreviewerTcpSession newSession)
        {
            newSession.ConnectionChanged += OnConnectionChanged;
            newSession.FrameReceived += OnFrameReceived;
            newSession.ViewportResizeRequested += OnViewportResizeRequested;
        }

        _session = args.NewValue as PreviewerTcpSession;
        ResetFrameState();
        _lastFrame = _session?.LastFrame;
        _lastFrameAt = _lastFrame is null ? null : DateTimeOffset.Now;
        UpdateConnectionState(_session?.Connection);
        UpdateViewportFromBounds();
        InvalidateVisual();
    }

    private void OnFrameReceived(FrameMessage frame)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _lastFrame = frame;
            _lastFrameAt = DateTimeOffset.Now;
            InvalidateVisual();
        });
    }

    private void OnConnectionChanged(IAvaloniaRemoteTransportConnection? connection)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateConnectionState(connection);
            InvalidateVisual();
        });
    }

    private void OnViewportResizeRequested(RequestViewportResizeMessage resize)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Width = Math.Min(4096, Math.Max(resize.Width, 1));
            Height = Math.Min(4096, Math.Max(resize.Height, 1));
        });
    }

    private void ResetFrameState()
    {
        _lastFrame = null;
        _lastFrameAt = null;
        _isConnected = false;
        _lastConnectionAt = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }

    private void UpdateConnectionState(IAvaloniaRemoteTransportConnection? connection)
    {
        _isConnected = connection is not null;
        _lastConnectionAt = DateTimeOffset.Now;
    }

    private string BuildFrameStatus()
    {
        if (_lastFrame is null)
        {
            return "Waiting for previewer frames...";
        }

        string format = _lastFrame.Format.ToString();
        string lastSeen = _lastFrameAt is null
            ? "never"
            : _lastFrameAt.Value.ToLocalTime().ToString("HH:mm:ss");
        string connection = _isConnected ? "connected" : "disconnected";
        string session = _session is null ? "no session" : $"{_session.SessionId}@{_session.Port}";
        return $"{connection} {session} | Frame {_lastFrame.Width}x{_lastFrame.Height} {format} | last {lastSeen}";
    }

    private static void DrawStatusOverlay(DrawingContext context, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        FormattedText formatted = new(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            12,
            Brushes.White);

        Rect background = new Rect(6, 6, formatted.Width + 10, formatted.Height + 6);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), background);
        context.DrawText(formatted, new Point(11, 9));
    }

    private void UpdateViewportFromBounds()
    {
        if (_session is null)
        {
            return;
        }

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        _session.UpdateViewport(Bounds.Width, Bounds.Height, 96, 96);
    }

    private void CopyFrameToBuffer(FrameMessage frame, ILockedFramebuffer buffer)
    {
        if (frame.Format == ProtocolPixelFormat.Bgra8888)
        {
            CopyBgra8888(frame, buffer);
            return;
        }

        if (frame.Format == ProtocolPixelFormat.Rgb565)
        {
            CopyRgb565(frame, buffer);
        }
    }

    private static int GetStride(FrameMessage frame, int bytesPerPixel)
        => frame.Stride > 0 ? frame.Stride : frame.Width * bytesPerPixel;

    private static void CopyBgra8888(FrameMessage frame, ILockedFramebuffer buffer)
    {
        int stride = GetStride(frame, 4);
        int lineLen = Math.Min(frame.Width * 4, buffer.RowBytes);
        for (int y = 0; y < frame.Height; y++)
        {
            Marshal.Copy(frame.Data, y * stride, buffer.Address + y * buffer.RowBytes, lineLen);
        }
    }

    private void CopyRgb565(FrameMessage frame, ILockedFramebuffer buffer)
    {
        int stride = GetStride(frame, 2);
        int destRowBytes = Math.Min(frame.Width * 4, buffer.RowBytes);
        EnsureRowBuffer(destRowBytes);
        if (_rowBuffer is null)
        {
            return;
        }

        for (int y = 0; y < frame.Height; y++)
        {
            int srcIndex = y * stride;
            int destIndex = 0;
            for (int x = 0; x < frame.Width; x++)
            {
                byte lo = frame.Data[srcIndex++];
                byte hi = frame.Data[srcIndex++];
                ushort pixel = (ushort)(lo | (hi << 8));

                int r = (pixel >> 11) & 0x1F;
                int g = (pixel >> 5) & 0x3F;
                int b = pixel & 0x1F;

                _rowBuffer[destIndex++] = (byte)((b << 3) | (b >> 2));
                _rowBuffer[destIndex++] = (byte)((g << 2) | (g >> 4));
                _rowBuffer[destIndex++] = (byte)((r << 3) | (r >> 2));
                _rowBuffer[destIndex++] = 255;
            }

            Marshal.Copy(_rowBuffer, 0, buffer.Address + y * buffer.RowBytes, destRowBytes);
        }
    }

    private void EnsureRowBuffer(int requiredBytes)
    {
        if (_rowBuffer is not null && _rowBuffer.Length >= requiredBytes)
        {
            return;
        }

        _rowBuffer = new byte[requiredBytes];
    }
}
