namespace XamlVisualEditor.Terminal;

public sealed class TerminalLine
{
    public TerminalCell[] Cells { get; }
    public bool IsWrapped { get; set; }

    public TerminalLine(int columns, TerminalAttributes attributes)
    {
        Cells = new TerminalCell[columns];
        Clear(attributes);
    }

    public void Clear(TerminalAttributes attributes)
    {
        for (int i = 0; i < Cells.Length; i++)
        {
            Cells[i] = TerminalCell.Empty(attributes);
        }
        IsWrapped = false;
    }
}
