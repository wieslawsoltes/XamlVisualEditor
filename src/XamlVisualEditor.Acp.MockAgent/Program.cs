using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp.MockAgent;

public static class Program
{
    public static async Task Main(string[] args)
    {
        _ = args;
        using StreamReader reader = new(Console.OpenStandardInput());
        using StreamWriter writer = new(Console.OpenStandardOutput())
        {
            AutoFlush = true
        };

        while (true)
        {
            string? line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                continue;
            }

            await HandleMessageAsync(line, writer).ConfigureAwait(false);
        }
    }

    private static async Task HandleMessageAsync(string json, StreamWriter writer)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        bool hasMethod = root.TryGetProperty("method", out JsonElement methodElement);
        bool hasId = root.TryGetProperty("id", out JsonElement idElement);

        if (!hasMethod)
        {
            return;
        }

        string? method = methodElement.GetString();
        if (string.IsNullOrWhiteSpace(method))
        {
            return;
        }

        JsonElement? parameters = root.TryGetProperty("params", out JsonElement paramsElement)
            ? paramsElement.Clone()
            : null;

        if (hasId)
        {
            object? idValue = TryGetIdValue(idElement);
            object result = BuildResult(method, parameters);
            await WriteResponseAsync(writer, idValue, result).ConfigureAwait(false);

            if (string.Equals(method, "session/prompt", StringComparison.OrdinalIgnoreCase))
            {
                string? sessionId = GetSessionId(parameters);
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    await WriteNotificationAsync(writer, "session/update", new
                    {
                        sessionId,
                        update = new
                        {
                            type = "message",
                            message = new
                            {
                                role = "assistant",
                                content = new[] { new { type = "text", text = "Mock reply from ACP agent." } }
                            }
                        }
                    }).ConfigureAwait(false);
                }
            }

            return;
        }

        if (string.Equals(method, "session/cancel", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
    }

    private static object BuildResult(string method, JsonElement? parameters)
    {
        if (string.Equals(method, "initialize", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                protocolVersion = "1.0",
                agentInfo = new
                {
                    name = "XVE Mock ACP Agent",
                    version = "0.1.0"
                },
                capabilities = new
                {
                    session = new { load = false },
                    fs = new { readTextFile = false, writeTextFile = false, listDirectory = false },
                    terminal = false
                }
            };
        }

        if (string.Equals(method, "session/new", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "session/load", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                sessionId = GetSessionId(parameters) ?? "mock-session"
            };
        }

        if (string.Equals(method, "session/prompt", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                stopReason = "complete"
            };
        }

        return new { ok = true };
    }

    private static string? GetSessionId(JsonElement? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        if (parameters.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return parameters.Value.TryGetProperty("sessionId", out JsonElement sessionElement)
            ? sessionElement.GetString()
            : null;
    }

    private static object? TryGetIdValue(JsonElement idElement)
    {
        if (idElement.ValueKind == JsonValueKind.String)
        {
            return idElement.GetString();
        }

        if (idElement.ValueKind == JsonValueKind.Number && idElement.TryGetInt64(out long numeric))
        {
            return numeric;
        }

        return null;
    }

    private static async Task WriteResponseAsync(StreamWriter writer, object? idValue, object result)
    {
        var payload = new
        {
            jsonrpc = "2.0",
            id = idValue,
            result
        };

        string line = JsonSerializer.Serialize(payload);
        await writer.WriteLineAsync(line).ConfigureAwait(false);
    }

    private static async Task WriteNotificationAsync(StreamWriter writer, string method, object parameters)
    {
        var payload = new
        {
            jsonrpc = "2.0",
            method,
            @params = parameters
        };

        string line = JsonSerializer.Serialize(payload);
        await writer.WriteLineAsync(line).ConfigureAwait(false);
    }
}
