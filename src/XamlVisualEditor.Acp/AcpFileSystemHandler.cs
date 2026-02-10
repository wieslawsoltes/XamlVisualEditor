using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp;

public sealed class AcpFileSystemHandler
{
    public void Register(AcpProtocolClient client)
    {
        client.RegisterRequestHandler("fs/read_text_file", HandleReadTextFileAsync);
        client.RegisterRequestHandler("fs/write_text_file", HandleWriteTextFileAsync);
    }

    public void Unregister(AcpProtocolClient client)
    {
        client.TryRemoveRequestHandler("fs/read_text_file");
        client.TryRemoveRequestHandler("fs/write_text_file");
    }

    private static Task<JsonElement?> HandleReadTextFileAsync(JsonElement? parameters, CancellationToken ct)
    {
        string path = RequireString(parameters, "path");
        EnsureAbsolutePath(path);

        int startLine = GetOptionalInt(parameters, "line", 1);
        int? limit = GetOptionalNullableInt(parameters, "limit");

        if (startLine < 1)
        {
            startLine = 1;
        }

        if (limit is not null && limit < 0)
        {
            limit = 0;
        }

        if (!File.Exists(path))
        {
            throw new JsonRpcException(-32002, "File not found.");
        }

        return ReadTextFileAsync(path, startLine, limit, ct);
    }

    private static async Task<JsonElement?> ReadTextFileAsync(string path, int startLine, int? limit, CancellationToken ct)
    {
        if (limit == 0)
        {
            return JsonSerializer.SerializeToElement(new { content = string.Empty });
        }

        StringBuilder builder = new();
        int currentLine = 1;
        int collected = 0;

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (true)
        {
            string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (currentLine >= startLine)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(line);
                collected++;

                if (limit.HasValue && collected >= limit.Value)
                {
                    break;
                }
            }

            currentLine++;
        }

        return JsonSerializer.SerializeToElement(new { content = builder.ToString() });
    }

    private static async Task<JsonElement?> HandleWriteTextFileAsync(JsonElement? parameters, CancellationToken ct)
    {
        string path = RequireString(parameters, "path");
        EnsureAbsolutePath(path);

        string content = RequireString(parameters, "content");

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            throw new JsonRpcException(-32002, "Directory not found.");
        }

        await File.WriteAllTextAsync(path, content, ct).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(new { });
    }

    private static string RequireString(JsonElement? parameters, string name)
    {
        if (parameters is null)
        {
            throw new JsonRpcException(-32602, "Missing parameters.");
        }

        if (parameters.Value.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String)
        {
            string? value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new JsonRpcException(-32602, $"Missing parameter '{name}'.");
    }

    private static int GetOptionalInt(JsonElement? parameters, string name, int defaultValue)
    {
        if (parameters is null)
        {
            return defaultValue;
        }

        if (parameters.Value.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out int value))
        {
            return value;
        }

        return defaultValue;
    }

    private static int? GetOptionalNullableInt(JsonElement? parameters, string name)
    {
        if (parameters is null)
        {
            return null;
        }

        if (parameters.Value.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out int value))
        {
            return value;
        }

        return null;
    }

    private static void EnsureAbsolutePath(string path)
    {
        if (!Path.IsPathRooted(path))
        {
            throw new JsonRpcException(-32602, "Path must be absolute.");
        }
    }
}
