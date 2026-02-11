using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XamlVisualEditor.Extensions.Hosting.IdeBridge;

/// <summary>Provides Content-Length framed JSON-RPC message IO.</summary>
public static class IdeBridgeMessageFraming
{
    /// <summary>Serializer options for JSON-RPC payloads.</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Writes a framed JSON-RPC message.</summary>
    public static async Task WriteMessageAsync(Stream output, object payload, CancellationToken ct)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
        string header = $"Content-Length: {body.Length}\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(header);

        await output.WriteAsync(headerBytes, ct).ConfigureAwait(false);
        await output.WriteAsync(body, ct).ConfigureAwait(false);
        await output.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Reads a framed JSON-RPC message.</summary>
    public static async Task<JsonDocument> ReadMessageAsync(Stream input, CancellationToken ct)
    {
        int contentLength = await ReadContentLengthAsync(input, ct).ConfigureAwait(false);
        byte[] body = new byte[contentLength];
        await ReadExactAsync(input, body, ct).ConfigureAwait(false);
        return JsonDocument.Parse(body);
    }

    private static async Task<int> ReadContentLengthAsync(Stream input, CancellationToken ct)
    {
        List<byte> headerBytes = new();
        int matchState = 0;
        byte[] buffer = new byte[1];

        while (matchState < 4)
        {
            int read = await input.ReadAsync(buffer, 0, 1, ct).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading header.");
            }

            byte value = buffer[0];
            headerBytes.Add(value);

            matchState = value switch
            {
                (byte)'\r' when matchState == 0 || matchState == 2 => matchState + 1,
                (byte)'\n' when matchState == 1 || matchState == 3 => matchState + 1,
                _ => 0
            };
        }

        string headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
        using StringReader reader = new(headerText);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                string value = line["Content-Length:".Length..].Trim();
                return int.Parse(value, CultureInfo.InvariantCulture);
            }
        }

        throw new InvalidOperationException("Content-Length header not found.");
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, ct).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading body.");
            }

            offset += read;
        }
    }
}
