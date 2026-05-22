using System;
using System.IO;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Acp;
using Xunit;

namespace XamlVisualEditor.Tests.Integration;

public sealed class AcpClientCapabilityTests
{
    [Fact]
    public async Task FileSystemReadWriteRoundTrip()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        await using AcpProtocolClient client = pipe.CreateClient();

        AcpFileSystemHandler handler = new();
        handler.Register(client);
        client.Start(cts.Token);

        string directory = Path.Combine(Path.GetTempPath(), "xve-acp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "note.txt");

        await SendRequestAsync(pipe.ServerWriter, 1, "fs/write_text_file", new
        {
            path,
            content = "hello"
        }, cts.Token);

        JsonElement writeResponse = await ReadResponseAsync(pipe.ServerReader, cts.Token);
        Assert.True(writeResponse.TryGetProperty("result", out _));
        Assert.Equal("hello", await File.ReadAllTextAsync(path, cts.Token));

        await SendRequestAsync(pipe.ServerWriter, 2, "fs/read_text_file", new
        {
            path,
            line = 1,
            limit = 10
        }, cts.Token);

        JsonElement readResponse = await ReadResponseAsync(pipe.ServerReader, cts.Token);
        string content = readResponse.GetProperty("result").GetProperty("content").GetString() ?? string.Empty;
        Assert.Equal("hello", content);
    }

    [Fact]
    public async Task TerminalCreateOutputReleaseRoundTrip()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        await using AcpProtocolClient client = pipe.CreateClient();

        AcpTerminalManager terminalManager = new();
        terminalManager.Register(client);
        client.Start(cts.Token);

        string command = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/echo";
        string[] args = OperatingSystem.IsWindows() ? new[] { "/c", "echo", "hello" } : new[] { "hello" };

        await SendRequestAsync(pipe.ServerWriter, 1, "terminal/create", new
        {
            command,
            args,
            outputByteLimit = 4096
        }, cts.Token);

        JsonElement createResponse = await ReadResponseAsync(pipe.ServerReader, cts.Token);
        Assert.True(createResponse.TryGetProperty("result", out JsonElement createResult), createResponse.ToString());
        string terminalId = createResult.GetProperty("terminalId").GetString() ?? string.Empty;
        Assert.False(string.IsNullOrWhiteSpace(terminalId));

        string output = string.Empty;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            await SendRequestAsync(pipe.ServerWriter, 2 + attempt, "terminal/output", new
            {
                terminalId
            }, cts.Token);

            JsonElement outputResponse = await ReadResponseAsync(pipe.ServerReader, cts.Token);
            output = outputResponse.GetProperty("result").GetProperty("output").GetString() ?? string.Empty;
            if (output.Contains("hello", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cts.Token);
        }

        Assert.Contains("hello", output, StringComparison.OrdinalIgnoreCase);

        await SendRequestAsync(pipe.ServerWriter, 20, "terminal/release", new
        {
            terminalId
        }, cts.Token);

        JsonElement releaseResponse = await ReadResponseAsync(pipe.ServerReader, cts.Token);
        Assert.True(releaseResponse.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task PermissionRequestRoundTrip()
    {
        using DuplexPipe pipe = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        await using AcpProtocolClient client = pipe.CreateClient();

        client.RegisterRequestHandler("session/request_permission", (_, _) =>
        {
            return Task.FromResult<JsonElement?>(JsonSerializer.SerializeToElement(new
            {
                outcome = new
                {
                    selected = new
                    {
                        optionId = "allow_once"
                    }
                }
            }));
        });

        client.Start(cts.Token);

        await SendRequestAsync(pipe.ServerWriter, 1, "session/request_permission", new
        {
            sessionId = "s1",
            options = new[]
            {
                new { optionId = "allow_once", name = "Allow once", kind = "allow_once" },
                new { optionId = "reject_once", name = "Reject once", kind = "reject_once" }
            },
            toolCall = new
            {
                toolCallId = "tool-1",
                title = "Write file"
            }
        }, cts.Token);

        JsonElement response = await ReadResponseAsync(pipe.ServerReader, cts.Token);
        string optionId = response.GetProperty("result")
            .GetProperty("outcome")
            .GetProperty("selected")
            .GetProperty("optionId")
            .GetString() ?? string.Empty;

        Assert.Equal("allow_once", optionId);
    }

    private static Task SendRequestAsync(AcpMessageWriter writer, int id, string method, object parameters, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params = parameters
        });

        return writer.WriteMessageAsync(json, ct);
    }

    private static async Task<JsonElement> ReadResponseAsync(AcpMessageReader reader, CancellationToken ct)
    {
        string? line = await reader.ReadMessageAsync(ct).ConfigureAwait(false);
        Assert.False(string.IsNullOrWhiteSpace(line));
        using JsonDocument doc = JsonDocument.Parse(line!);
        return doc.RootElement.Clone();
    }

    private sealed class DuplexPipe : IDisposable
    {
        private readonly Pipe _clientToServer = new();
        private readonly Pipe _serverToClient = new();
        private AcpMessageReader? _serverReader;
        private AcpMessageWriter? _serverWriter;

        public AcpMessageReader ServerReader
        {
            get
            {
                _serverReader ??= new AcpMessageReader(_clientToServer.Reader.AsStream());
                return _serverReader;
            }
        }

        public AcpMessageWriter ServerWriter
        {
            get
            {
                _serverWriter ??= new AcpMessageWriter(_serverToClient.Writer.AsStream());
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
            _clientToServer.Reader.Complete();
            _clientToServer.Writer.Complete();
            _serverToClient.Reader.Complete();
            _serverToClient.Writer.Complete();
        }
    }
}
