using System.IO.Pipelines;
using System.Text.Json;
using XamlVisualEditor.Core.Lsp;
using XamlVisualEditor.Lsp;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class LspClientSessionTests
{
    [Fact]
    public async Task InitializeAndShutdownRoundTrip()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        (Stream clientInput, Stream clientOutput, Stream serverInput, Stream serverOutput) = CreateDuplexStreams();

        LspServerConfiguration config = new()
        {
            LanguageId = "csharp",
            ServerPath = "unused"
        };

        LspClientSession session = LspTestHooks.CreateSessionForTesting(config, clientInput, clientOutput);

        Task serverTask = Task.Run(async () =>
        {
            JsonDocument initRequest = await LspMessageFraming.ReadMessageAsync(serverInput, cts.Token);
            int initId = initRequest.RootElement.GetProperty("id").GetInt32();

            await LspMessageFraming.WriteMessageAsync(serverOutput, new
            {
                jsonrpc = "2.0",
                id = initId,
                result = new { capabilities = new { } }
            }, cts.Token);

            JsonDocument initialized = await LspMessageFraming.ReadMessageAsync(serverInput, cts.Token);
            Assert.Equal("initialized", initialized.RootElement.GetProperty("method").GetString());

            JsonDocument shutdown = await LspMessageFraming.ReadMessageAsync(serverInput, cts.Token);
            int shutdownId = shutdown.RootElement.GetProperty("id").GetInt32();
            Assert.Equal("shutdown", shutdown.RootElement.GetProperty("method").GetString());

            await LspMessageFraming.WriteMessageAsync(serverOutput, new
            {
                jsonrpc = "2.0",
                id = shutdownId,
                result = (object?)null
            }, cts.Token);

            JsonDocument exit = await LspMessageFraming.ReadMessageAsync(serverInput, cts.Token);
            Assert.Equal("exit", exit.RootElement.GetProperty("method").GetString());
        }, cts.Token);

        LspInitializeParams initializeParams = new()
        {
            ProcessId = Environment.ProcessId,
            RootUri = "file:///tmp",
            ClientInfo = new LspClientInfo { Name = "tests" },
            Capabilities = new { }
        };

        await session.InitializeAsync(initializeParams, cts.Token);
        await session.ShutdownAsync(cts.Token);

        await serverTask;
    }

    [Fact]
    public async Task DiagnosticsAreCachedFromNotifications()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        (Stream clientInput, Stream clientOutput, Stream serverInput, Stream serverOutput) = CreateDuplexStreams();

        LspServerConfiguration config = new()
        {
            LanguageId = "csharp",
            ServerPath = "unused"
        };

        LspClientSession session = LspTestHooks.CreateSessionForTesting(config, clientInput, clientOutput);

        Task serverTask = Task.Run(async () =>
        {
            JsonDocument initRequest = await LspMessageFraming.ReadMessageAsync(serverInput, cts.Token);
            int initId = initRequest.RootElement.GetProperty("id").GetInt32();

            await LspMessageFraming.WriteMessageAsync(serverOutput, new
            {
                jsonrpc = "2.0",
                id = initId,
                result = new { capabilities = new { } }
            }, cts.Token);

            JsonDocument initialized = await LspMessageFraming.ReadMessageAsync(serverInput, cts.Token);
            Assert.Equal("initialized", initialized.RootElement.GetProperty("method").GetString());

            await LspMessageFraming.WriteMessageAsync(serverOutput, new
            {
                jsonrpc = "2.0",
                method = "textDocument/publishDiagnostics",
                @params = new
                {
                    uri = "file:///tmp/Test.cs",
                    diagnostics = new[]
                    {
                        new
                        {
                            range = new
                            {
                                start = new { line = 0, character = 0 },
                                end = new { line = 0, character = 5 }
                            },
                            severity = 1,
                            message = "Test error"
                        }
                    }
                }
            }, cts.Token);

            JsonDocument shutdown = await LspMessageFraming.ReadMessageAsync(serverInput, cts.Token);
            int shutdownId = shutdown.RootElement.GetProperty("id").GetInt32();
            Assert.Equal("shutdown", shutdown.RootElement.GetProperty("method").GetString());

            await LspMessageFraming.WriteMessageAsync(serverOutput, new
            {
                jsonrpc = "2.0",
                id = shutdownId,
                result = (object?)null
            }, cts.Token);

            JsonDocument exit = await LspMessageFraming.ReadMessageAsync(serverInput, cts.Token);
            Assert.Equal("exit", exit.RootElement.GetProperty("method").GetString());
        }, cts.Token);

        await session.InitializeAsync(new LspInitializeParams
        {
            ProcessId = Environment.ProcessId,
            RootUri = "file:///tmp",
            ClientInfo = new LspClientInfo { Name = "tests" },
            Capabilities = new { }
        }, cts.Token);

        Uri uri = new("file:///tmp/Test.cs");
        IReadOnlyList<LspDiagnostic> diagnostics = await WaitForDiagnosticsAsync(session, uri, cts.Token);

        Assert.Single(diagnostics);
        Assert.Equal("Test error", diagnostics[0].Message);

        await session.ShutdownAsync(cts.Token);
        await serverTask;
    }

    private static (Stream clientInput, Stream clientOutput, Stream serverInput, Stream serverOutput) CreateDuplexStreams()
    {
        Pipe clientToServer = new();
        Pipe serverToClient = new();

        Stream clientOutput = clientToServer.Writer.AsStream();
        Stream serverInput = clientToServer.Reader.AsStream();

        Stream serverOutput = serverToClient.Writer.AsStream();
        Stream clientInput = serverToClient.Reader.AsStream();

        return (clientInput, clientOutput, serverInput, serverOutput);
    }

    private static async Task<IReadOnlyList<LspDiagnostic>> WaitForDiagnosticsAsync(
        LspClientSession session,
        Uri uri,
        CancellationToken ct)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            IReadOnlyList<LspDiagnostic> diagnostics = await session.GetDiagnosticsAsync(uri, ct);
            if (diagnostics.Count > 0)
            {
                return diagnostics;
            }

            await Task.Delay(50, ct);
        }

        return Array.Empty<LspDiagnostic>();
    }
}
