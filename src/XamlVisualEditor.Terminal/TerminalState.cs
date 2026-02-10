namespace XamlVisualEditor.Terminal;

public sealed class TerminalState
{
    public int CursorRow { get; set; }
    public int CursorColumn { get; set; }
    public int SavedCursorRow { get; set; }
    public int SavedCursorColumn { get; set; }
    public TerminalAttributes Attributes { get; set; } = TerminalAttributes.Default;
    public bool InsertMode { get; set; }
    public bool OriginMode { get; set; }
    public bool AutoWrap { get; set; } = true;
    public bool WrapPending { get; set; }
    public int ScrollTop { get; set; }
    public int ScrollBottom { get; set; }
    public bool AltBufferActive { get; set; }
    public TerminalMouseMode MouseMode { get; set; } = TerminalMouseMode.None;
    public bool MouseSgr { get; set; }
    public TerminalMouseProtocol MouseProtocol { get; set; } = TerminalMouseProtocol.Vt200;
    public bool MouseX10 { get; set; }
    public bool CursorVisible { get; set; } = true;
    public bool CursorBlink { get; set; } = true;
    public TerminalCursorShape CursorShape { get; set; } = TerminalCursorShape.Block;
    public bool BracketedPaste { get; set; }
    public bool ApplicationKeypad { get; set; }
    public bool ApplicationCursorKeys { get; set; }
    public TerminalCharset CharsetG0 { get; set; } = TerminalCharset.Ascii;
    public TerminalCharset CharsetG1 { get; set; } = TerminalCharset.Ascii;
    public bool UseG1Charset { get; set; }
    public string? WindowTitle { get; set; }
    public int? ActiveHyperlinkId { get; set; }
}

public enum TerminalMouseMode
{
    None,
    Click,
    Drag,
    Move
}

public enum TerminalMouseProtocol
{
    X10,
    Vt200,
    Sgr
}

public enum TerminalCursorShape
{
    Block,
    Underline,
    Bar
}

public enum TerminalCharset
{
    Ascii,
    DecSpecialGraphics
}
