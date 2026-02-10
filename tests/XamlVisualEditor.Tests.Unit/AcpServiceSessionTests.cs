using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Acp;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class AcpServiceSessionTests
{
    [Fact]
    public async Task InitializeAndCreateSessionSetsActiveSessionId()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await using AcpProtocolClient client = pipe.CreateClient();
        client.Start(cts.Token);

        await using AcpAgentHost host = AcpAgentHost.CreateForTests(Process.GetCurrentProcess(), client);
        IAcpAgentHostFactory hostFactory = new StubHostFactory(host);
        AcpService service = new(hostFactory);

        _ = Task.Run(() => HandleSessionLifecycleAsync(pipe, cts.Token), cts.Token);

        await service.ConnectAsync(new AcpAgentProcessOptions { FileName = "test" }, cts.Token);
        _ = await service.InitializeAsync(new { clientInfo = new { name = "XVE", version = "test" } }, cts.Token);

        string sessionId = await service.CreateSessionAsync(new { cwd = "/", mcpServers = Array.Empty<object>() }, cts.Token);

        Assert.Equal("session-123", sessionId);
        Assert.Equal("session-123", service.ActiveSessionId);
    }

    [Fact]
    public async Task PromptAndCancelSendExpectedMessages()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await using AcpProtocolClient client = pipe.CreateClient();
        client.Start(cts.Token);

        await using AcpAgentHost host = AcpAgentHost.CreateForTests(Process.GetCurrentProcess(), client);
        IAcpAgentHostFactory hostFactory = new StubHostFactory(host);
        AcpService service = new(hostFactory);

        Task serverTask = Task.Run(() => HandlePromptAndCancelAsync(pipe, cts.Token), cts.Token);

        await service.ConnectAsync(new AcpAgentProcessOptions { FileName = "test" }, cts.Token);

        _ = await service.PromptAsync("session-456", new { content = "hello" }, cts.Token);
        await service.CancelAsync("session-456", cts.Token);

        await serverTask;
    }

    [Fact]
    public async Task ErrorResponseThrowsJsonRpcException()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await using AcpProtocolClient client = pipe.CreateClient();
        client.Start(cts.Token);

        Task<JsonElement> requestTask = client.SendRequestAsync("initialize", new { }, cts.Token);

        JsonElement request = await ReadMessageAsync(pipe.ServerReader, cts.Token);
        long id = request.GetProperty("id").GetInt64();

        await pipe.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            error = new { code = -32000, message = "auth required" }
        }));

        JsonRpcException ex = await Assert.ThrowsAsync<JsonRpcException>(async () =>
        {
            _ = await requestTask;
        });
        Assert.Equal(-32000, ex.Code);
    }

    private static async Task HandleSessionLifecycleAsync(DuplexPipe pipe, CancellationToken ct)
    {
        JsonElement initialize = await ReadMessageAsync(pipe.ServerReader, ct);
        Assert.Equal("initialize", initialize.GetProperty("method").GetString());

        long initId = initialize.GetProperty("id").GetInt64();
        await pipe.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = initId,
            result = new { agentInfo = new { name = "mock" } }
        }));

        JsonElement sessionNew = await ReadMessageAsync(pipe.ServerReader, ct);
        Assert.Equal("session/new", sessionNew.GetProperty("method").GetString());

        long sessionId = sessionNew.GetProperty("id").GetInt64();
        await pipe.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = sessionId,
            result = new { sessionId = "session-123" }
        }));
    }

    private static async Task HandlePromptAndCancelAsync(DuplexPipe pipe, CancellationToken ct)
    {
        JsonElement prompt = await ReadMessageAsync(pipe.ServerReader, ct);
        Assert.Equal("session/prompt", prompt.GetProperty("method").GetString());

        JsonElement parameters = prompt.GetProperty("params");
        Assert.Equal("session-456", parameters.GetProperty("sessionId").GetString());

        long promptId = prompt.GetProperty("id").GetInt64();
        await pipe.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = promptId,
            result = new { stopReason = "end_turn" }
        }));

        JsonElement cancel = await ReadMessageAsync(pipe.ServerReader, ct);
        Assert.Equal("session/cancel", cancel.GetProperty("method").GetString());
    }

    private static async Task<JsonElement> ReadMessageAsync(StreamReader reader, CancellationToken ct)
    {
        string? line = await reader.ReadLineAsync(ct);
        Assert.False(string.IsNullOrWhiteSpace(line));
        using JsonDocument doc = JsonDocument.Parse(line!);
        return doc.RootElement.Clone();
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

    private sealed class StubHostFactory : IAcpAgentHostFactory
    {
        private readonly AcpAgentHost _host;

        public StubHostFactory(AcpAgentHost host)
        {
            _host = host;
        }

        public Task<AcpAgentHost> StartAsync(AcpAgentProcessOptions options, CancellationToken ct)
        {
            return Task.FromResult(_host);
        }
    }
}
