using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using XamlVisualEditor.Debugging.Dap;

namespace XamlVisualEditor.Tests.Integration;

public sealed class DapProtocolClientTests
{
    [Fact]
    public async Task Client_Receives_Response_Body()
    {
        using AnonymousPipeServerStream serverToClient = new(PipeDirection.Out, HandleInheritability.Inheritable);
        using AnonymousPipeServerStream clientToServer = new(PipeDirection.In, HandleInheritability.Inheritable);
        using AnonymousPipeClientStream clientRead = new(PipeDirection.In, serverToClient.GetClientHandleAsString());
        using AnonymousPipeClientStream clientWrite = new(PipeDirection.Out, clientToServer.GetClientHandleAsString());

        DapMessageReader serverReader = new(clientToServer);
        DapMessageWriter serverWriter = new(serverToClient);

        CancellationTokenSource cts = new();
        Task serverTask = Task.Run(async () =>
        {
            string? requestJson = await serverReader.ReadMessageAsync(cts.Token);
            Assert.NotNull(requestJson);

            using JsonDocument requestDoc = JsonDocument.Parse(requestJson!);
            int seq = requestDoc.RootElement.GetProperty("seq").GetInt32();

            var response = new
            {
                seq = 1,
                type = "response",
                request_seq = seq,
                success = true,
                command = "initialize",
                body = new { supports = true }
            };

            string responseJson = JsonSerializer.Serialize(response);
            await serverWriter.WriteMessageAsync(responseJson, cts.Token);
        }, cts.Token);

        DapProtocolClient client = new(new DapMessageReader(clientRead), new DapMessageWriter(clientWrite));
        client.Start(cts.Token);

        JsonElement body = await client.SendRequestAsync("initialize", new { }, cts.Token);
        Assert.True(body.GetProperty("supports").GetBoolean());

        cts.Cancel();
        await serverTask;
        await client.DisposeAsync();
    }
}
