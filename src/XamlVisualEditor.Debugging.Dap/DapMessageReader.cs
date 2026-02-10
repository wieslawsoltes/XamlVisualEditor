using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Debugging.Dap;

public sealed class DapMessageReader
{
    private readonly Stream _stream;

    public DapMessageReader(Stream stream)
    {
        _stream = stream;
    }

    public async Task<string?> ReadMessageAsync(CancellationToken ct)
    {
        int contentLength = await ReadContentLengthAsync(ct).ConfigureAwait(false);
        if (contentLength <= 0)
        {
            return null;
        }

        byte[] buffer = new byte[contentLength];
        int read = 0;
        while (read < contentLength)
        {
            int count = await _stream.ReadAsync(buffer.AsMemory(read, contentLength - read), ct)
                .ConfigureAwait(false);
            if (count == 0)
            {
                return null;
            }
            read += count;
        }

        return Encoding.UTF8.GetString(buffer);
    }

    private async Task<int> ReadContentLengthAsync(CancellationToken ct)
    {
        int? contentLength = null;
        while (true)
        {
            string? line = await ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                return 0;
            }

            if (line.Length == 0)
            {
                break;
            }

            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                string value = line.Substring("Content-Length:".Length).Trim();
                if (int.TryParse(value, out int parsed))
                {
                    contentLength = parsed;
                }
            }
        }

        return contentLength ?? 0;
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        using MemoryStream buffer = new();
        while (true)
        {
            byte[] one = new byte[1];
            int read = await _stream.ReadAsync(one.AsMemory(0, 1), ct).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            byte value = one[0];
            if (value == '\n')
            {
                break;
            }

            if (value != '\r')
            {
                buffer.WriteByte(value);
            }
        }

        return Encoding.ASCII.GetString(buffer.ToArray());
    }
}
