namespace XamlVisualEditor.Terminal;

public readonly struct TerminalCellPosition
{
    public int Row { get; }
    public int Column { get; }

    public TerminalCellPosition(int row, int column)
    {
        Row = row;
        Column = column;
    }
}
