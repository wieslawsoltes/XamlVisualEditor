using System;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions.Hosting.IdeBridge;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class IdeBridgeConnectionTests
{
    [Fact]
    public async Task RequestHandlerSendsResult()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await using IdeBridgeJsonRpcConnection connection = pipe.CreateConnection();

        connection.RegisterRequestHandler("ping", (_, _) => Task.FromResult<object?>(new { pong = true }));
        connection.Start(cts.Token);

        await IdeBridgeMessageFraming.WriteMessageAsync(pipe.ServerWriter, new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "ping",
            @params = new { }
        }, cts.Token);

        using JsonDocument response = await IdeBridgeMessageFraming.ReadMessageAsync(pipe.ServerReader, cts.Token);
        JsonElement root = response.RootElement;
        Assert.Equal(1, root.GetProperty("id").GetInt32());
        Assert.True(root.GetProperty("result").GetProperty("pong").GetBoolean());
    }

    [Fact]
    public async Task NotificationIsRaised()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await using IdeBridgeJsonRpcConnection connection = pipe.CreateConnection();

        TaskCompletionSource<(string Method, JsonElement? Params)> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.NotificationReceived += (method, parameters) => tcs.TrySetResult((method, parameters));
        connection.Start(cts.Token);

        await IdeBridgeMessageFraming.WriteMessageAsync(pipe.ServerWriter, new
        {
            jsonrpc = "2.0",
            method = "workspace.changed",
            @params = new { workspaceId = "w1" }
        }, cts.Token);

        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(2), cts.Token));
        Assert.True(tcs.Task.IsCompleted, "Expected notification was not received.");
        var result = await tcs.Task;
        Assert.Equal("workspace.changed", result.Method);
    }

    private sealed class DuplexPipe : IDisposable
    {
        private readonly Pipe _clientToServer = new();
        private readonly Pipe _serverToClient = new();

        public Stream ServerReader => _clientToServer.Reader.AsStream();
        public Stream ServerWriter => _serverToClient.Writer.AsStream();

        public IdeBridgeJsonRpcConnection CreateConnection()
        {
            return new IdeBridgeJsonRpcConnection(_serverToClient.Reader.AsStream(), _clientToServer.Writer.AsStream());
        }

        public void Dispose()
        {
            _clientToServer.Reader.Complete();
            _clientToServer.Writer.Complete();
            _serverToClient.Reader.Complete();
            _serverToClient.Writer.Complete();
        }
    }
}
