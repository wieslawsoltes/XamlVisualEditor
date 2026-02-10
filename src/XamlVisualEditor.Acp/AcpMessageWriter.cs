using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp;

public sealed class AcpMessageWriter
{
    private static readonly byte[] NewlineBytes = Encoding.UTF8.GetBytes("\n");
    private readonly Stream _stream;

    public AcpMessageWriter(Stream stream)
    {
        _stream = stream;
    }

    public async Task WriteMessageAsync(string json, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        if (json.IndexOf('\n') >= 0 || json.IndexOf('\r') >= 0)
        {
            throw new InvalidOperationException("ACP stdio messages must be single-line JSON.");
        }

        byte[] payload = Encoding.UTF8.GetBytes(json);
        await _stream.WriteAsync(payload.AsMemory(0, payload.Length), ct).ConfigureAwait(false);
        await _stream.WriteAsync(NewlineBytes.AsMemory(0, NewlineBytes.Length), ct).ConfigureAwait(false);
        await _stream.FlushAsync(ct).ConfigureAwait(false);
    }
}
