using System;
using System.Collections.Generic;

namespace XamlVisualEditor.Terminal;

public sealed class TerminalBuffer
{
    private readonly List<TerminalLine> _scrollback;
    private TerminalLine[] _lines;

    public int Columns { get; private set; }
    public int Rows { get; private set; }
    public int ScrollbackLimit { get; set; } = 10000;

    public TerminalBuffer(int columns, int rows, TerminalAttributes attributes)
    {
        Columns = Math.Max(1, columns);
        Rows = Math.Max(1, rows);
        _scrollback = new List<TerminalLine>(1024);
        _lines = CreateLines(Columns, Rows, attributes);
    }

    public TerminalLine GetLine(int row)
    {
        return _lines[row];
    }

    public IReadOnlyList<TerminalLine> Lines => _lines;

    public IReadOnlyList<TerminalLine> Scrollback => _scrollback;

    public int ScrollbackCount => _scrollback.Count;

    public int TotalLines => _scrollback.Count + Rows;

    public TerminalLine GetLineGlobal(int row)
    {
        int total = _scrollback.Count + Rows;
        if (total == 0)
        {
            return new TerminalLine(Columns, TerminalAttributes.Default);
        }

        int clamped = Math.Clamp(row, 0, total - 1);
        if (clamped < _scrollback.Count)
        {
            return _scrollback[clamped];
        }

        return _lines[clamped - _scrollback.Count];
    }

    public void Resize(int columns, int rows, TerminalAttributes attributes)
    {
        columns = Math.Max(1, columns);
        rows = Math.Max(1, rows);

        if (columns == Columns && rows == Rows)
        {
            return;
        }

        TerminalLine[] newLines = CreateLines(columns, rows, attributes);
        int copyRows = Math.Min(rows, Rows);
        int copyCols = Math.Min(columns, Columns);

        for (int r = 0; r < copyRows; r++)
        {
            TerminalLine source = _lines[r];
            TerminalLine target = newLines[r];
            for (int c = 0; c < copyCols; c++)
            {
                target.Cells[c] = source.Cells[c];
            }
            target.IsWrapped = source.IsWrapped;
        }

        Columns = columns;
        Rows = rows;
        _lines = newLines;
    }

    public (int Row, int Column) ReflowResize(int columns, int rows, TerminalAttributes attributes, int cursorRow, int cursorColumn)
    {
        TerminalCellPosition[] positions = { new TerminalCellPosition(cursorRow, cursorColumn) };
        IReadOnlyList<TerminalCellPosition> mapped = ReflowResizeWithMapping(columns, rows, attributes, positions);
        if (mapped.Count == 0)
        {
            return (cursorRow, cursorColumn);
        }

        return (mapped[0].Row, mapped[0].Column);
    }

    public IReadOnlyList<TerminalCellPosition> ReflowResizeWithMapping(
        int columns,
        int rows,
        TerminalAttributes attributes,
        IReadOnlyList<TerminalCellPosition> positions)
    {
        return ReflowResizeWithMappingInternal(columns, rows, attributes, positions, positionsAreGlobal: false, returnGlobal: false);
    }

    public IReadOnlyList<TerminalCellPosition> ReflowResizeWithMappingGlobal(
        int columns,
        int rows,
        TerminalAttributes attributes,
        IReadOnlyList<TerminalCellPosition> positions)
    {
        return ReflowResizeWithMappingInternal(columns, rows, attributes, positions, positionsAreGlobal: true, returnGlobal: true);
    }

    private IReadOnlyList<TerminalCellPosition> ReflowResizeWithMappingInternal(
        int columns,
        int rows,
        TerminalAttributes attributes,
        IReadOnlyList<TerminalCellPosition> positions,
        bool positionsAreGlobal,
        bool returnGlobal)
    {
        columns = Math.Max(1, columns);
        rows = Math.Max(1, rows);
        int totalLines = _scrollback.Count + Rows;

        int positionCount = positions.Count;
        int[] sourceRows = new int[positionCount];
        int[] sourceCols = new int[positionCount];
        int[] logicalLinesForPos = new int[positionCount];
        int[] logicalColsForPos = new int[positionCount];
        bool[] hasLogical = new bool[positionCount];
        int[] mappedRows = new int[positionCount];
        int[] mappedCols = new int[positionCount];

        for (int i = 0; i < positionCount; i++)
        {
            int row = positionsAreGlobal
                ? Math.Clamp(positions[i].Row, 0, Math.Max(0, totalLines - 1))
                : Math.Clamp(positions[i].Row, 0, Rows - 1);
            int col = Math.Clamp(positions[i].Column, 0, Columns - 1);
            sourceRows[i] = row;
            sourceCols[i] = col;
            mappedRows[i] = positionsAreGlobal
                ? Math.Clamp(row, 0, Math.Max(0, totalLines - 1))
                : Math.Clamp(row, 0, rows - 1);
            mappedCols[i] = Math.Clamp(col, 0, columns - 1);
            logicalLinesForPos[i] = -1;
            logicalColsForPos[i] = -1;
        }

            Dictionary<int, List<int>> positionsByLine = new();
            for (int i = 0; i < positionCount; i++)
            {
                int lineIndex = positionsAreGlobal ? sourceRows[i] : _scrollback.Count + sourceRows[i];
                if (!positionsByLine.TryGetValue(lineIndex, out List<int>? list))
                {
                    list = new List<int>();
                    positionsByLine[lineIndex] = list;
                }
                list.Add(i);
            }

            List<List<TerminalCell>> logicalLines = new();
            List<TerminalCell> current = new();
            int logicalLineIndex = 0;
            int currentColumn = 0;

            void FlushLogicalLine(bool wrapped)
            {
                if (!wrapped)
                {
                    logicalLines.Add(current);
                    current = new List<TerminalCell>();
                    currentColumn = 0;
                    logicalLineIndex++;
                }
            }

            void ProcessLine(TerminalLine line, int lineIndex)
            {
                int length = GetTrimmedLength(line, attributes);
                positionsByLine.TryGetValue(lineIndex, out List<int>? linePositions);

                for (int i = 0; i < length; i++)
                {
                    TerminalCell cell = line.Cells[i];
                    if (cell.Width == 0)
                    {
                        if (linePositions is not null)
                        {
                            foreach (int posIndex in linePositions)
                            {
                                if (!hasLogical[posIndex] && sourceCols[posIndex] == i)
                                {
                                    hasLogical[posIndex] = true;
                                    logicalLinesForPos[posIndex] = logicalLineIndex;
                                    logicalColsForPos[posIndex] = Math.Max(0, currentColumn - 1);
                                }
                            }
                        }
                        continue;
                    }

                    if (linePositions is not null)
                    {
                        foreach (int posIndex in linePositions)
                        {
                            if (!hasLogical[posIndex] && sourceCols[posIndex] == i)
                            {
                                hasLogical[posIndex] = true;
                                logicalLinesForPos[posIndex] = logicalLineIndex;
                                logicalColsForPos[posIndex] = currentColumn;
                            }
                        }
                    }

                    current.Add(cell);
                    currentColumn += cell.Width;
                }

                if (linePositions is not null)
                {
                    foreach (int posIndex in linePositions)
                    {
                        if (hasLogical[posIndex])
                        {
                            continue;
                        }

                        hasLogical[posIndex] = true;
                        logicalLinesForPos[posIndex] = logicalLineIndex;
                        if (length == 0)
                        {
                            logicalColsForPos[posIndex] = 0;
                        }
                        else if (sourceCols[posIndex] >= length)
                        {
                            logicalColsForPos[posIndex] = currentColumn + (sourceCols[posIndex] - length);
                        }
                        else
                        {
                            logicalColsForPos[posIndex] = Math.Max(0, currentColumn - 1);
                        }
                    }
                }

                FlushLogicalLine(line.IsWrapped);
            }

            int index = 0;
            foreach (TerminalLine line in _scrollback)
            {
                ProcessLine(line, index++);
            }

            foreach (TerminalLine line in _lines)
            {
                ProcessLine(line, index++);
            }

            if (current.Count > 0 || logicalLines.Count == 0)
            {
                logicalLines.Add(current);
            }

            List<int>[] positionsByLogical = new List<int>[logicalLines.Count];
            for (int i = 0; i < positionCount; i++)
            {
                if (!hasLogical[i])
                {
                    continue;
                }

                int logicalLine = logicalLinesForPos[i];
                if (logicalLine < 0 || logicalLine >= logicalLines.Count)
                {
                    continue;
                }

                positionsByLogical[logicalLine] ??= new List<int>();
                positionsByLogical[logicalLine]!.Add(i);
            }

            List<TerminalLine> reflowed = new();
            bool[] mapped = new bool[positionCount];

            for (int li = 0; li < logicalLines.Count; li++)
            {
                List<TerminalCell> logical = logicalLines[li];
                List<int>? linePositions = positionsByLogical[li];
                if (logical.Count == 0)
                {
                    TerminalLine empty = new(columns, attributes);
                    int emptyRow = reflowed.Count;
                    if (linePositions is not null)
                    {
                        foreach (int posIndex in linePositions)
                        {
                            mappedRows[posIndex] = emptyRow;
                            mappedCols[posIndex] = 0;
                            mapped[posIndex] = true;
                        }
                    }

                    reflowed.Add(empty);
                    continue;
                }

                TerminalLine target = new(columns, attributes);
                int col = 0;
                int logicalCol = 0;
                int currentOutputRow = reflowed.Count;

                foreach (TerminalCell cell in logical)
                {
                    int width = Math.Max(1, (int)cell.Width);
                    if (col >= columns || (width == 2 && col == columns - 1))
                    {
                        target.IsWrapped = true;
                        reflowed.Add(target);
                        target = new TerminalLine(columns, attributes);
                        col = 0;
                        currentOutputRow = reflowed.Count;
                    }

                    if (linePositions is not null)
                    {
                        foreach (int posIndex in linePositions)
                        {
                            if (mapped[posIndex])
                            {
                                continue;
                            }

                            int posLogicalCol = logicalColsForPos[posIndex];
                            if (posLogicalCol >= logicalCol && posLogicalCol < logicalCol + width)
                            {
                                mappedRows[posIndex] = currentOutputRow;
                                mappedCols[posIndex] = Math.Min(col + (posLogicalCol - logicalCol), columns - 1);
                                mapped[posIndex] = true;
                            }
                        }
                    }

                    target.Cells[col] = cell;
                    if (width == 2 && col + 1 < columns)
                    {
                        target.Cells[col + 1] = new TerminalCell(new System.Text.Rune(' '), 0, cell.Attributes, cell.HyperlinkId);
                    }

                    col += width;
                    logicalCol += width;
                }

                if (linePositions is not null)
                {
                    foreach (int posIndex in linePositions)
                    {
                        if (mapped[posIndex])
                        {
                            continue;
                        }

                        mappedRows[posIndex] = currentOutputRow;
                        mappedCols[posIndex] = Math.Min(col, columns - 1);
                        mapped[posIndex] = true;
                    }
                }

                reflowed.Add(target);
            }

            int splitIndex = Math.Max(0, reflowed.Count - rows);
            int scrollbackStart = Math.Max(0, splitIndex - ScrollbackLimit);
            _scrollback.Clear();
            for (int i = scrollbackStart; i < splitIndex; i++)
            {
                _scrollback.Add(reflowed[i]);
            }

            TerminalLine[] newLines = CreateLines(columns, rows, attributes);
            int destRow = 0;
            for (int i = splitIndex; i < reflowed.Count && destRow < rows; i++)
            {
                newLines[destRow++] = reflowed[i];
            }

            Columns = columns;
            Rows = rows;
            _lines = newLines;

            TerminalCellPosition[] result = new TerminalCellPosition[positionCount];
            int maxGlobalRow = Math.Max(0, reflowed.Count - 1);
            for (int i = 0; i < positionCount; i++)
            {
                int row = returnGlobal ? mappedRows[i] : mappedRows[i] - splitIndex;
                if (returnGlobal)
                {
                    row = Math.Clamp(row, 0, maxGlobalRow);
                }
                else
                {
                    if (row < 0)
                    {
                        row = 0;
                    }
                    else if (row >= rows)
                    {
                        row = rows - 1;
                    }
                }

                int col = Math.Clamp(mappedCols[i], 0, columns - 1);
                result[i] = new TerminalCellPosition(row, col);
            }

            return result;
    }

    public void Clear(TerminalAttributes attributes)
    {
        foreach (TerminalLine line in _lines)
        {
            line.Clear(attributes);
        }
    }

    public void ClearScrollback()
    {
        _scrollback.Clear();
    }

    public void ClearLine(int row, TerminalAttributes attributes)
    {
        _lines[row].Clear(attributes);
    }

    public void ScrollUp(int top, int bottom, TerminalAttributes attributes)
    {
        if (top < 0 || bottom >= Rows || top >= bottom)
        {
            return;
        }

        TerminalLine outgoing = _lines[top];
        _scrollback.Add(outgoing);
        if (_scrollback.Count > ScrollbackLimit)
        {
            _scrollback.RemoveAt(0);
        }

        for (int row = top; row < bottom; row++)
        {
            _lines[row] = _lines[row + 1];
        }

        _lines[bottom] = new TerminalLine(Columns, attributes);
    }

    public void ScrollDown(int top, int bottom, TerminalAttributes attributes)
    {
        if (top < 0 || bottom >= Rows || top >= bottom)
        {
            return;
        }

        for (int row = bottom; row > top; row--)
        {
            _lines[row] = _lines[row - 1];
        }

        _lines[top] = new TerminalLine(Columns, attributes);
    }

    public void InsertLines(int row, int count, int top, int bottom, TerminalAttributes attributes)
    {
        if (count <= 0 || row < top || row > bottom)
        {
            return;
        }

        int linesToMove = Math.Min(count, bottom - row + 1);
        for (int i = bottom; i >= row + linesToMove; i--)
        {
            _lines[i] = _lines[i - linesToMove];
        }

        for (int i = 0; i < linesToMove; i++)
        {
            _lines[row + i] = new TerminalLine(Columns, attributes);
        }
    }

    public void DeleteLines(int row, int count, int top, int bottom, TerminalAttributes attributes)
    {
        if (count <= 0 || row < top || row > bottom)
        {
            return;
        }

        int linesToMove = Math.Min(count, bottom - row + 1);
        for (int i = row; i <= bottom - linesToMove; i++)
        {
            _lines[i] = _lines[i + linesToMove];
        }

        for (int i = 0; i < linesToMove; i++)
        {
            _lines[bottom - i] = new TerminalLine(Columns, attributes);
        }
    }

    public void InsertChars(int row, int column, int count, TerminalAttributes attributes)
    {
        if (row < 0 || row >= Rows || count <= 0)
        {
            return;
        }

        TerminalLine line = _lines[row];
        int max = Columns - 1;
        column = Math.Clamp(column, 0, max);
        count = Math.Min(count, Columns - column);

        for (int i = max; i >= column + count; i--)
        {
            line.Cells[i] = line.Cells[i - count];
        }

        for (int i = 0; i < count; i++)
        {
            line.Cells[column + i] = TerminalCell.Empty(attributes);
        }
    }

    public void DeleteChars(int row, int column, int count, TerminalAttributes attributes)
    {
        if (row < 0 || row >= Rows || count <= 0)
        {
            return;
        }

        TerminalLine line = _lines[row];
        int max = Columns - 1;
        column = Math.Clamp(column, 0, max);
        count = Math.Min(count, Columns - column);

        for (int i = column; i <= max - count; i++)
        {
            line.Cells[i] = line.Cells[i + count];
        }

        for (int i = 0; i < count; i++)
        {
            line.Cells[max - i] = TerminalCell.Empty(attributes);
        }
    }

    public void EraseChars(int row, int column, int count, TerminalAttributes attributes)
    {
        if (row < 0 || row >= Rows || count <= 0)
        {
            return;
        }

        TerminalLine line = _lines[row];
        int max = Columns - 1;
        column = Math.Clamp(column, 0, max);
        count = Math.Min(count, Columns - column);

        for (int i = 0; i < count; i++)
        {
            line.Cells[column + i] = TerminalCell.Empty(attributes);
        }
    }

    private static TerminalLine[] CreateLines(int columns, int rows, TerminalAttributes attributes)
    {
        TerminalLine[] lines = new TerminalLine[rows];
        for (int i = 0; i < rows; i++)
        {
            lines[i] = new TerminalLine(columns, attributes);
        }
        return lines;
    }

    private static int GetTrimmedLength(TerminalLine line, TerminalAttributes attributes)
    {
        int last = line.Cells.Length - 1;
        while (last >= 0 && IsEmptyCell(line.Cells[last], attributes))
        {
            last--;
        }
        return last + 1;
    }

    private static bool IsEmptyCell(TerminalCell cell, TerminalAttributes attributes)
    {
        return cell.Width == 1
            && cell.Rune.Value == ' '
            && cell.HyperlinkId is null
            && cell.Attributes.Equals(attributes);
    }
}
