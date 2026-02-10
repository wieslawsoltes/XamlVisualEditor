using System;
using System.IO;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Acp;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class AcpProtocolClientTests
{
    [Fact]
    public async Task SendRequestReceivesResponse()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await using AcpProtocolClient client = pipe.CreateClient();

        client.Start(cts.Token);

        Task<JsonElement> responseTask = client.SendRequestAsync("initialize", new { foo = "bar" }, cts.Token);

        string? requestLine = await pipe.ServerReader.ReadLineAsync(cts.Token);
        Assert.False(string.IsNullOrWhiteSpace(requestLine));

        using JsonDocument requestDoc = JsonDocument.Parse(requestLine!);
        long id = requestDoc.RootElement.GetProperty("id").GetInt64();

        var response = new
        {
            jsonrpc = "2.0",
            id,
            result = new { ok = true }
        };

        await pipe.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(response));

        JsonElement result = await responseTask;
        Assert.True(result.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task NotificationIsRaised()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await using AcpProtocolClient client = pipe.CreateClient();

        TaskCompletionSource<(string Method, JsonElement? Params)> tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        client.NotificationReceived += (method, parameters) => tcs.TrySetResult((method, parameters));

        client.Start(cts.Token);

        var payload = new
        {
            jsonrpc = "2.0",
            method = "session/update",
            @params = new { sessionId = "s1" }
        };

        await pipe.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(payload));

        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(2), cts.Token));
        Assert.True(tcs.Task.IsCompleted, "Expected notification was not received.");
        var result = await tcs.Task;
        Assert.Equal("session/update", result.Method);
    }

    [Fact]
    public async Task RequestHandlerSendsResult()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await using AcpProtocolClient client = pipe.CreateClient();

        client.RegisterRequestHandler("ping", (_, _) =>
        {
            using JsonDocument doc = JsonDocument.Parse("{\"pong\":true}");
            JsonElement result = doc.RootElement.Clone();
            return Task.FromResult<JsonElement?>(result);
        });

        client.Start(cts.Token);

        var request = new
        {
            jsonrpc = "2.0",
            id = 99,
            method = "ping",
            @params = new { }
        };

        await pipe.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(request));

        string? responseLine = await pipe.ServerReader.ReadLineAsync(cts.Token);
        Assert.False(string.IsNullOrWhiteSpace(responseLine));

        using JsonDocument responseDoc = JsonDocument.Parse(responseLine!);
        JsonElement root = responseDoc.RootElement;
        Assert.Equal(99, root.GetProperty("id").GetInt32());
        Assert.True(root.GetProperty("result").GetProperty("pong").GetBoolean());
    }

    private sealed class DuplexPipe : IDisposable
    {
        private readonly Pipe _clientToServer = new();
        private readonly Pipe _serverToClient = new();
        private StreamReader? _serverReader;
        private StreamWriter? _serverWriter;

        public StreamReader ServerReader
        {
            get
            {
                _serverReader ??= new StreamReader(_clientToServer.Reader.AsStream());
                return _serverReader;
            }
        }

        public StreamWriter ServerWriter
        {
            get
            {
                _serverWriter ??= new StreamWriter(_serverToClient.Writer.AsStream()) { AutoFlush = true };
                return _serverWriter;
            }
        }

        public AcpProtocolClient CreateClient()
        {
            AcpMessageReader reader = new(_serverToClient.Reader.AsStream());
            AcpMessageWriter writer = new(_clientToServer.Writer.AsStream());
            return new AcpProtocolClient(reader, writer);
        }

        public void Dispose()
        {
            _serverReader?.Dispose();
            _serverWriter?.Dispose();
        }
    }
}
