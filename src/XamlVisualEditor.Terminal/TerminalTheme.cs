namespace XamlVisualEditor.Terminal;

public readonly struct TerminalTheme
{
    public TerminalRgb Foreground { get; }
    public TerminalRgb Background { get; }
    public TerminalRgb SelectionForeground { get; }
    public TerminalRgb SelectionBackground { get; }

    public TerminalTheme(
        TerminalRgb foreground,
        TerminalRgb background,
        TerminalRgb selectionForeground,
        TerminalRgb selectionBackground)
    {
        Foreground = foreground;
        Background = background;
        SelectionForeground = selectionForeground;
        SelectionBackground = selectionBackground;
    }

    public static TerminalTheme DefaultDark => new(
        new TerminalRgb(0xD4, 0xD4, 0xD4),
        new TerminalRgb(0x1E, 0x1E, 0x1E),
        new TerminalRgb(0x1E, 0x1E, 0x1E),
        new TerminalRgb(0xC5, 0xC5, 0xC5));
}
