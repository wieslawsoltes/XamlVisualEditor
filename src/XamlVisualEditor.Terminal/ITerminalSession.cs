using System;
using System.Collections.Generic;

namespace XamlVisualEditor.Terminal;

public interface ITerminalSession : IDisposable
{
    ITerminalEmulator Emulator { get; }
    event Action? ScreenUpdated;
    event Action<string>? TitleChanged;
    event Action<ReadOnlyMemory<byte>>? OutputReceived;
    event Action<int?>? Exited;
    void Start();
    void Write(ReadOnlySpan<byte> data);
    void Resize(int columns, int rows, int pixelWidth = 0, int pixelHeight = 0);
    IReadOnlyList<TerminalCellPosition> ResizeWithMapping(int columns, int rows, IReadOnlyList<TerminalCellPosition> positions, int pixelWidth = 0, int pixelHeight = 0);
    IReadOnlyList<TerminalCellPosition> ResizeWithMappingGlobal(int columns, int rows, IReadOnlyList<TerminalCellPosition> positions, int pixelWidth = 0, int pixelHeight = 0);
}
