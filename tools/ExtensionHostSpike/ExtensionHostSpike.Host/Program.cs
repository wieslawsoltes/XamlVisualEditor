using System.Diagnostics;
using System.Text.Json;

internal static class Program
{
    private const string JsonRpcVersion = "2.0";

    private static async Task<int> Main(string[] args)
    {
        string? extensionPath = TryGetExtensionPath(args) ?? TryGetDefaultExtensionPath();
        if (string.IsNullOrWhiteSpace(extensionPath) || !File.Exists(extensionPath))
        {
            Console.Error.WriteLine("Extension executable not found. Pass --extension-path <path>.");
            return 2;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = extensionPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Console.Error.WriteLine("Failed to start extension process.");
            return 3;
        }

        using var writer = process.StandardInput;
        using var reader = process.StandardOutput;
        using var errorReader = process.StandardError;

        var errorTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await errorReader.ReadLineAsync()) != null)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                Console.Error.WriteLine(line);
            }
        });

        var handler = new HostHandler();
        string? requestLine;
        while ((requestLine = await reader.ReadLineAsync()) != null)
        {
            if (requestLine.Length == 0)
            {
                continue;
            }

            string? response = handler.Handle(requestLine);
            if (response is not null)
            {
                await writer.WriteLineAsync(response);
            }
        }

        await process.WaitForExitAsync();
        await errorTask;
        return process.ExitCode;
    }

    private static string? TryGetExtensionPath(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--extension-path", StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string? TryGetDefaultExtensionPath()
    {
        string baseDir = AppContext.BaseDirectory;
        var current = new DirectoryInfo(baseDir);
        while (current is not null)
        {
            string toolsDir = Path.Combine(current.FullName, "tools", "ExtensionHostSpike");
            if (Directory.Exists(toolsDir))
            {
                string candidate = Path.Combine(
                    toolsDir,
                    "ExtensionHostSpike.Extension",
                    "bin",
                    "Debug",
                    "net10.0",
                    "ExtensionHostSpike.Extension");

                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private sealed class HostHandler
    {
        private readonly HashSet<string> _commands = new(StringComparer.Ordinal);

        public string? Handle(string jsonLine)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(jsonLine);
                JsonElement root = doc.RootElement;
                if (!TryGetString(root, "method", out string? method) || method is null)
                {
                    return null;
                }

                int id = root.TryGetProperty("id", out JsonElement idElement) && idElement.TryGetInt32(out int value)
                    ? value
                    : 0;

                JsonElement? parameters = root.TryGetProperty("params", out JsonElement paramElement)
                    ? paramElement
                    : null;

                object? result = method switch
                {
                    "xve.commands.register" => HandleRegister(parameters),
                    "xve.workspace.getConfiguration" => HandleGetConfiguration(parameters),
                    "xve.window.showInformationMessage" => HandleShowInformationMessage(parameters),
                    _ => new JsonRpcError(-32601, "Method not found")
                };

                if (result is JsonRpcError error)
                {
                    return JsonSerializer.Serialize(new
                    {
                        jsonrpc = JsonRpcVersion,
                        id,
                        error = new { code = error.Code, message = error.Message }
                    });
                }

                return JsonSerializer.Serialize(new
                {
                    jsonrpc = JsonRpcVersion,
                    id,
                    result
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    jsonrpc = JsonRpcVersion,
                    id = 0,
                    error = new { code = -32603, message = ex.Message }
                });
            }
        }

        private object HandleRegister(JsonElement? parameters)
        {
            if (parameters is null || !TryGetString(parameters.Value, "id", out string? id) || id is null)
            {
                return new JsonRpcError(-32602, "Invalid params");
            }

            _commands.Add(id);
            Console.WriteLine("Registered command: " + id);
            return new { ok = true };
        }

        private object HandleGetConfiguration(JsonElement? parameters)
        {
            string section = "";
            if (parameters is not null && TryGetString(parameters.Value, "section", out string? value) && value is not null)
            {
                section = value;
            }

            return new
            {
                section,
                values = new Dictionary<string, object?>
                {
                    ["extension.sampleSetting"] = true,
                    ["extension.sampleText"] = "hello"
                }
            };
        }

        private object HandleShowInformationMessage(JsonElement? parameters)
        {
            string text = "";
            if (parameters is not null && TryGetString(parameters.Value, "text", out string? value) && value is not null)
            {
                text = value;
            }

            Console.WriteLine("Info: " + text);
            return new { ok = true };
        }

        private static bool TryGetString(JsonElement element, string name, out string? value)
        {
            if (element.TryGetProperty(name, out JsonElement prop) && prop.ValueKind == JsonValueKind.String)
            {
                value = prop.GetString();
                return true;
            }

            value = null;
            return false;
        }
    }

    private readonly record struct JsonRpcError(int Code, string Message);
}
