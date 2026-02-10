using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Terminal;

namespace XamlVisualEditor.Shell.ViewModels;

public sealed class TerminalViewModel : ReactiveObject, IDisposable
{
    private readonly ITerminalSession _session;
    private TerminalMetrics _metrics;
    private int _scrollOffset;
    private int _lastScrollbackCount;
    private readonly Action<string> _clipboardHandler;

    public Guid Id { get; } = Guid.NewGuid();

    [Reactive]
    public string Title { get; private set; } = "Terminal";

    [Reactive]
    public bool IsConnected { get; private set; }

    public TerminalTheme Theme { get; } = TerminalTheme.DefaultDark;

    public TerminalEmulator Emulator => _session.Emulator;

    public TerminalSelection Selection { get; private set; }

    public bool HasSelection => Selection.IsActive;

    public int ScrollOffset => _scrollOffset;

    public event Action? FrameInvalidated;
    public event Action<string>? ClipboardCopyRequested;

    public ReactiveCommand<Unit, Unit> ClearSelectionCommand { get; }

    public TerminalViewModel(ITerminalSession session)
    {
        _session = session;
        _session.ScreenUpdated += OnScreenUpdated;
        _session.TitleChanged += title => Title = string.IsNullOrWhiteSpace(title) ? "Terminal" : title;
        _clipboardHandler = text => ClipboardCopyRequested?.Invoke(text);
        Emulator.ClipboardCopyRequested += _clipboardHandler;
        ClearSelectionCommand = ReactiveCommand.Create(ClearSelection);
    }

    public void Start()
    {
        if (IsConnected)
        {
            return;
        }

        _session.Start();
        IsConnected = true;
    }

    public void Resize(int columns, int rows)
    {
        if (columns <= 0 || rows <= 0)
        {
            return;
        }

        int pixelWidth = (int)Math.Round(_metrics.CellWidth * columns);
        int pixelHeight = (int)Math.Round(_metrics.CellHeight * rows);

        if (Selection.IsActive)
        {
            TerminalCellPosition start = new(Selection.StartRow, Selection.StartColumn);
            TerminalCellPosition end = new(Selection.EndRow, Selection.EndColumn);
            IReadOnlyList<TerminalCellPosition> mapped = _session.ResizeWithMappingGlobal(columns, rows, new[] { start, end }, pixelWidth, pixelHeight);
            if (mapped.Count == 2)
            {
                Selection = TerminalSelection.Start(mapped[0].Row, mapped[0].Column)
                    .WithEnd(mapped[1].Row, mapped[1].Column);
            }
            else
            {
                Selection = TerminalSelection.Empty;
            }
        }
        else
        {
            _session.Resize(columns, rows, pixelWidth, pixelHeight);
        }

        FrameInvalidated?.Invoke();
    }

    public void SetMetrics(TerminalMetrics metrics)
    {
        _metrics = metrics;
        Emulator.SetDisplayMetrics(
            (int)Math.Round(metrics.CellWidth),
            (int)Math.Round(metrics.CellHeight),
            (int)Math.Round(metrics.CellWidth * Emulator.ActiveBuffer.Columns),
            (int)Math.Round(metrics.CellHeight * Emulator.ActiveBuffer.Rows));
    }

    public void SendText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ResetScrollback();

        if (Selection.IsActive)
        {
            ClearSelection();
        }

        byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
        _session.Write(data);
    }

    public void SendKey(TerminalKeyInfo key)
    {
        string? sequence = TerminalKeyMapper.Map(key, Emulator.State);
        if (sequence is null)
        {
            return;
        }

        ResetScrollback();

        if (Selection.IsActive)
        {
            ClearSelection();
        }

        SendText(sequence);
    }

    public void SendMouseReport(int row, int col, TerminalMouseButton button, TerminalMouseAction action)
    {
        TerminalState state = Emulator.State;
        if (state.MouseMode == TerminalMouseMode.None)
        {
            return;
        }

        if (state.MouseProtocol == TerminalMouseProtocol.X10 && action != TerminalMouseAction.Press)
        {
            return;
        }

        string sequence = state.MouseProtocol switch
        {
            TerminalMouseProtocol.Sgr => TerminalMouseEncoding.BuildSgr(button, action, col + 1, row + 1),
            TerminalMouseProtocol.X10 => TerminalMouseEncoding.BuildX10(button, col + 1, row + 1),
            _ => TerminalMouseEncoding.BuildVt200(button, action, col + 1, row + 1)
        };
        SendText(sequence);
    }

    public async Task SendPasteAsync(string text, int chunkSize = 4096, int delayMs = 5)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ResetScrollback();

        if (Selection.IsActive)
        {
            ClearSelection();
        }

        bool bracketed = Emulator.State.BracketedPaste;
        if (bracketed)
        {
            SendText("\x1B[200~");
        }

        int index = 0;
        while (index < text.Length)
        {
            int length = Math.Min(chunkSize, text.Length - index);
            string chunk = text.Substring(index, length);
            SendText(chunk);
            index += length;
            if (index < text.Length)
            {
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
        }

        if (bracketed)
        {
            SendText("\x1B[201~");
        }
    }

    public bool TryGetCellFromPoint(double x, double y, out int row, out int col)
    {
        row = 0;
        col = 0;
        if (_metrics.CellWidth <= 0 || _metrics.CellHeight <= 0)
        {
            return false;
        }

        x -= _metrics.OffsetX;
        y -= _metrics.OffsetY;
        if (x < 0 || y < 0)
        {
            return false;
        }

        col = (int)(x / _metrics.CellWidth);
        row = (int)(y / _metrics.CellHeight);
        return true;
    }

    public void StartSelection(int row, int col)
    {
        Emulator.Read((buffer, _) =>
        {
            int startIndex = GetScrollbackStartIndex(buffer);
            Selection = TerminalSelection.Start(startIndex + row, col);
        });
        FrameInvalidated?.Invoke();
    }

    public void UpdateSelection(int row, int col)
    {
        if (!Selection.IsActive)
        {
            return;
        }

        Emulator.Read((buffer, _) =>
        {
            int startIndex = GetScrollbackStartIndex(buffer);
            Selection = Selection.WithEnd(startIndex + row, col);
        });
        FrameInvalidated?.Invoke();
    }

    public void ClearSelection()
    {
        Selection = TerminalSelection.Empty;
        FrameInvalidated?.Invoke();
    }

    public void ScrollByLines(int delta)
    {
        int scrollback = Emulator.ActiveBuffer.Scrollback.Count;
        if (scrollback <= 0)
        {
            return;
        }

        int next = Math.Clamp(_scrollOffset + delta, 0, scrollback);
        if (next == _scrollOffset)
        {
            return;
        }

        _scrollOffset = next;
        FrameInvalidated?.Invoke();
    }

    public void ResetScrollback()
    {
        if (_scrollOffset == 0)
        {
            return;
        }

        _scrollOffset = 0;
        FrameInvalidated?.Invoke();
    }

    public string GetSelectedText()
    {
        if (!Selection.IsActive)
        {
            return string.Empty;
        }

        TerminalSelection normalized = Selection.Normalize();
        List<string> lines = new();

        Emulator.Read((buffer, _) =>
        {
            int totalLines = buffer.TotalLines;
            for (int row = normalized.StartRow; row <= normalized.EndRow; row++)
            {
                int clampedRow = Math.Clamp(row, 0, Math.Max(0, totalLines - 1));
                TerminalLine line = buffer.GetLineGlobal(clampedRow);
                int startCol = row == normalized.StartRow ? normalized.StartColumn : 0;
                int endCol = row == normalized.EndRow ? normalized.EndColumn : buffer.Columns - 1;
                startCol = Math.Clamp(startCol, 0, buffer.Columns - 1);
                endCol = Math.Clamp(endCol, 0, buffer.Columns - 1);

                System.Text.StringBuilder builder = new();
                for (int col = startCol; col <= endCol; col++)
                {
                    TerminalCell cell = line.Cells[col];
                    if (cell.Width == 0)
                    {
                        continue;
                    }

                    builder.Append(cell.Rune.ToString());
                }

                lines.Add(builder.ToString().TrimEnd());
            }
        });

        return string.Join("\n", lines);
    }

    private void OnScreenUpdated()
    {
        Emulator.Read((buffer, _) =>
        {
            int scrollbackCount = buffer.ScrollbackCount;
            int totalLines = buffer.TotalLines;
            int delta = scrollbackCount - _lastScrollbackCount;
            if (_scrollOffset > 0 && delta != 0)
            {
                _scrollOffset = Math.Clamp(_scrollOffset + delta, 0, scrollbackCount);
            }

            if (Selection.IsActive && delta != 0)
            {
                Selection = Selection.ShiftForScrollback(_lastScrollbackCount, delta, totalLines);
            }

            _lastScrollbackCount = scrollbackCount;
        });
        FrameInvalidated?.Invoke();
    }

    private int GetScrollbackStartIndex(TerminalBuffer buffer)
    {
        int totalLines = buffer.TotalLines;
        int startIndex = Math.Max(0, buffer.ScrollbackCount - _scrollOffset);
        if (startIndex + buffer.Rows > totalLines)
        {
            startIndex = Math.Max(0, totalLines - buffer.Rows);
        }

        return startIndex;
    }


    public void Dispose()
    {
        _session.ScreenUpdated -= OnScreenUpdated;
        Emulator.ClipboardCopyRequested -= _clipboardHandler;
        _session.Dispose();
    }
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
