using System.Text;

namespace XamlVisualEditor.Terminal;

public struct TerminalCell
{
    public Rune Rune;
    public byte Width;
    public TerminalAttributes Attributes;
    public int? HyperlinkId;

    public TerminalCell(Rune rune, byte width, TerminalAttributes attributes, int? hyperlinkId = null)
    {
        Rune = rune;
        Width = width;
        Attributes = attributes;
        HyperlinkId = hyperlinkId;
    }

    public static TerminalCell Empty(TerminalAttributes attributes)
    {
        return new TerminalCell(new Rune(' '), 1, attributes, null);
    }
}
