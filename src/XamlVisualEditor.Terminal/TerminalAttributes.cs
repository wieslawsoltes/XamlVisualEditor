namespace XamlVisualEditor.Terminal;

public readonly struct TerminalAttributes
{
    public TerminalColor Foreground { get; }
    public TerminalColor Background { get; }
    public bool Bold { get; }
    public bool Dim { get; }
    public bool Italic { get; }
    public bool Underline { get; }
    public bool Blink { get; }
    public bool Inverse { get; }
    public bool Strikethrough { get; }

    public TerminalAttributes(
        TerminalColor foreground,
        TerminalColor background,
        bool bold,
        bool dim,
        bool italic,
        bool underline,
        bool blink,
        bool inverse,
        bool strikethrough)
    {
        Foreground = foreground;
        Background = background;
        Bold = bold;
        Dim = dim;
        Italic = italic;
        Underline = underline;
        Blink = blink;
        Inverse = inverse;
        Strikethrough = strikethrough;
    }

    public static TerminalAttributes Default => new(
        TerminalColor.Default,
        TerminalColor.Default,
        bold: false,
        dim: false,
        italic: false,
        underline: false,
        blink: false,
        inverse: false,
        strikethrough: false);

    public TerminalAttributes With(
        TerminalColor? foreground = null,
        TerminalColor? background = null,
        bool? bold = null,
        bool? dim = null,
        bool? italic = null,
        bool? underline = null,
        bool? blink = null,
        bool? inverse = null,
        bool? strikethrough = null)
    {
        return new TerminalAttributes(
            foreground ?? Foreground,
            background ?? Background,
            bold ?? Bold,
            dim ?? Dim,
            italic ?? Italic,
            underline ?? Underline,
            blink ?? Blink,
            inverse ?? Inverse,
            strikethrough ?? Strikethrough);
    }
}
