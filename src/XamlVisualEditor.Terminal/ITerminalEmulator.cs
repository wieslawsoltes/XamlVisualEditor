using System;
using System.Collections.Generic;

namespace XamlVisualEditor.Terminal;

public interface ITerminalEmulator
{
    event Action? ScreenUpdated;
    event Action<string>? TitleChanged;
    event Action<string>? ResponseRequested;
    event Action<string>? UnhandledSequence;
    event Action<string>? ClipboardCopyRequested;

    TerminalState State { get; }
    TerminalBuffer ActiveBuffer { get; }

    void SetDisplayMetrics(int cellWidthPx, int cellHeightPx, int pixelWidthPx, int pixelHeightPx);
    void SetScrollbackLimit(int limit);
    void Read(Action<TerminalBuffer, TerminalState> reader);
    void ProcessInput(ReadOnlySpan<byte> data);
    void Resize(int columns, int rows);
    IReadOnlyList<TerminalCellPosition> ResizeWithMapping(int columns, int rows, IReadOnlyList<TerminalCellPosition> positions);
    IReadOnlyList<TerminalCellPosition> ResizeWithMappingGlobal(int columns, int rows, IReadOnlyList<TerminalCellPosition> positions);
    string? GetHyperlink(int? id);
}
