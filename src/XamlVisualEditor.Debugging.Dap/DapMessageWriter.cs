using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Debugging.Dap;

public sealed class DapMessageWriter
{
    private readonly Stream _stream;

    public DapMessageWriter(Stream stream)
    {
        _stream = stream;
    }

    public async Task WriteMessageAsync(string json, CancellationToken ct)
    {
        byte[] payload = Encoding.UTF8.GetBytes(json);
        string header = $"Content-Length: {payload.Length}\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        await _stream.WriteAsync(headerBytes.AsMemory(0, headerBytes.Length), ct).ConfigureAwait(false);
        await _stream.WriteAsync(payload.AsMemory(0, payload.Length), ct).ConfigureAwait(false);
        await _stream.FlushAsync(ct).ConfigureAwait(false);
    }
}
