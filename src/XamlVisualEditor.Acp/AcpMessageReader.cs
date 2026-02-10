using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp;

public sealed class AcpMessageReader
{
    private readonly StreamReader _reader;

    public AcpMessageReader(Stream stream)
    {
        _reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);
    }

    public async Task<string?> ReadMessageAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string? line = await _reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                return null;
            }

            if (line.Length == 0)
            {
                continue;
            }

            return line;
        }

        return null;
    }
}
