using System.IO.Pipes;
using System.Text.Json;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using XamlVisualEditor.Extensions.Hosting.IdeBridge;
using XamlVisualEditor.Extensions.Hosting.VscodeCompat;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class VscodeCompatSessionTests
{
    [Fact]
    public async Task RegisterCommandRegistersHandler()
    {
        using var inputServer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
        using var inputClient = new AnonymousPipeClientStream(PipeDirection.In, inputServer.ClientSafePipeHandle);
        using var outputServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
        using var outputClient = new AnonymousPipeClientStream(PipeDirection.Out, outputServer.ClientSafePipeHandle);

        var connection = new IdeBridgeJsonRpcConnection(inputClient, outputServer);
        var commands = new CommandRegistry();
        var window = new InMemoryWindow();
        var settings = new InMemorySettingsStore();
        var logger = new TestLogger();

        var session = new VscodeCompatSession(connection, commands, window, settings, logger);
        session.Start(CancellationToken.None);

        await IdeBridgeMessageFraming.WriteMessageAsync(
            inputServer,
            new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "vscode.commands.register",
                @params = new { id = "xve.test" }
            },
            CancellationToken.None);

        using JsonDocument response = await IdeBridgeMessageFraming.ReadMessageAsync(outputClient, CancellationToken.None);
        Assert.True(response.RootElement.TryGetProperty("result", out _));

        IReadOnlyList<string> registered = await commands.GetCommandsAsync(CancellationToken.None);
        Assert.Contains("xve.test", registered);

        await session.DisposeAsync();
    }

    private sealed class TestLogger : IExtensionLogger
    {
        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
