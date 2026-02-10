using System;
using System.IO;

namespace XamlVisualEditor.Terminal;

public sealed class TerminalSequenceLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _sync = new();

    public TerminalSequenceLogger(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
        _writer = new StreamWriter(File.Open(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    public void LogInput(ReadOnlySpan<byte> data)
    {
        WriteLine("IN", data);
    }

    public void LogOutput(ReadOnlySpan<byte> data)
    {
        WriteLine("OUT", data);
    }

    private void WriteLine(string prefix, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        string encoded = Convert.ToBase64String(data);
        lock (_sync)
        {
            _writer.Write(prefix);
            _writer.Write(' ');
            _writer.WriteLine(encoded);
        }
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
