using System.Text.Json;

internal static class Program
{
    private const string JsonRpcVersion = "2.0";

    private static async Task<int> Main()
    {
        using var writer = Console.Out;
        using var reader = Console.In;

        await SendRequestAsync(writer, 1, "xve.commands.register", new { id = "extension.sayHello" });
        await ExpectResponseAsync(reader, 1);

        await SendRequestAsync(writer, 2, "xve.workspace.getConfiguration", new { section = "extension" });
        await ExpectResponseAsync(reader, 2);

        await SendRequestAsync(writer, 3, "xve.window.showInformationMessage", new { text = "Hello World" });
        await ExpectResponseAsync(reader, 3);

        return 0;
    }

    private static async Task SendRequestAsync(TextWriter writer, int id, string method, object parameters)
    {
        string json = JsonSerializer.Serialize(new
        {
            jsonrpc = JsonRpcVersion,
            id,
            method,
            @params = parameters
        });

        await writer.WriteLineAsync(json);
        await writer.FlushAsync();
    }

    private static async Task ExpectResponseAsync(TextReader reader, int expectedId)
    {
        string? line = await reader.ReadLineAsync();
        if (line is null)
        {
            Console.Error.WriteLine("No response from host.");
            return;
        }

        using JsonDocument doc = JsonDocument.Parse(line);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("id", out JsonElement idElement) || !idElement.TryGetInt32(out int id))
        {
            Console.Error.WriteLine("Invalid response id.");
            return;
        }

        if (id != expectedId)
        {
            Console.Error.WriteLine("Unexpected response id: " + id);
            return;
        }
    }
}
