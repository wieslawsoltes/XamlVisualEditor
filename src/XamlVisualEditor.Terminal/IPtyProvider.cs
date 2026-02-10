using System;
using System.IO;

namespace XamlVisualEditor.Terminal;

public interface IPtyProcess : IDisposable
{
    Stream Input { get; }
    Stream Output { get; }
    int Pid { get; }
    void Resize(int columns, int rows, int pixelWidth = 0, int pixelHeight = 0);
}

public interface IPtyProvider
{
    IPtyProcess StartProcess(TerminalSessionOptions options);
}
