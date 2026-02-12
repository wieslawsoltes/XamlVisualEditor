using System;
using System.Threading.Tasks;

namespace XamlVisualEditor.Terminal;

public interface ITerminalViewModel
{
    ITerminalEmulator Emulator { get; }
    TerminalTheme Theme { get; }
    TerminalSelection Selection { get; }
    bool HasSelection { get; }
    int ScrollOffset { get; }

    event Action? FrameInvalidated;
    event Action<string>? ClipboardCopyRequested;

    void Resize(int columns, int rows);
    void SetMetrics(TerminalMetrics metrics);
    void SendText(string text);
    void SendKey(TerminalKeyInfo key);
    void SendMouseReport(int row, int col, TerminalMouseButton button, TerminalMouseAction action);
    Task SendPasteAsync(string text, int chunkSize = 4096, int delayMs = 5);
    bool TryGetCellFromPoint(double x, double y, out int row, out int col);
    void StartSelection(int row, int col);
    void UpdateSelection(int row, int col);
    void ClearSelection();
    void ScrollByLines(int delta);
    void SetScrollOffset(int offset);
    void ResetScrollback();
    string GetSelectedText();
}

public readonly struct TerminalMetrics
{
    public double CellWidth { get; }
    public double CellHeight { get; }
    public double OffsetX { get; }
    public double OffsetY { get; }

    public TerminalMetrics(double cellWidth, double cellHeight, double offsetX, double offsetY)
    {
        CellWidth = cellWidth;
        CellHeight = cellHeight;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }
}

public readonly struct TerminalSelection
{
    public static TerminalSelection Empty => new(false, 0, 0, 0, 0);

    public bool IsActive { get; }
    public int StartRow { get; }
    public int StartColumn { get; }
    public int EndRow { get; }
    public int EndColumn { get; }

    private TerminalSelection(bool active, int startRow, int startColumn, int endRow, int endColumn)
    {
        IsActive = active;
        StartRow = startRow;
        StartColumn = startColumn;
        EndRow = endRow;
        EndColumn = endColumn;
    }

    public static TerminalSelection Start(int row, int col)
    {
        return new TerminalSelection(true, row, col, row, col);
    }

    public TerminalSelection WithEnd(int row, int col)
    {
        return new TerminalSelection(IsActive, StartRow, StartColumn, row, col);
    }

    public TerminalSelection Normalize()
    {
        if (StartRow < EndRow || (StartRow == EndRow && StartColumn <= EndColumn))
        {
            return this;
        }

        return new TerminalSelection(IsActive, EndRow, EndColumn, StartRow, StartColumn);
    }

    public TerminalSelection ShiftForScrollback(int oldScrollbackCount, int delta, int totalLines)
    {
        if (!IsActive || delta == 0)
        {
            return this;
        }

        int startRow = ShiftRow(StartRow, oldScrollbackCount, delta, totalLines);
        int endRow = ShiftRow(EndRow, oldScrollbackCount, delta, totalLines);
        return new TerminalSelection(IsActive, startRow, StartColumn, endRow, EndColumn);
    }

    private static int ShiftRow(int row, int oldScrollbackCount, int delta, int totalLines)
    {
        int shifted = row >= oldScrollbackCount ? row + delta : row;
        int maxRow = Math.Max(0, totalLines - 1);
        return Math.Clamp(shifted, 0, maxRow);
    }
}
