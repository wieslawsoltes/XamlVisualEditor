namespace XamlVisualEditor.Terminal;

public enum TerminalKey
{
    Unknown,
    Enter,
    Backspace,
    Tab,
    Escape,
    Up,
    Down,
    Left,
    Right,
    Home,
    End,
    PageUp,
    PageDown,
    Insert,
    Delete,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,
    Keypad0,
    Keypad1,
    Keypad2,
    Keypad3,
    Keypad4,
    Keypad5,
    Keypad6,
    Keypad7,
    Keypad8,
    Keypad9,
    KeypadDecimal,
    KeypadEnter,
    KeypadAdd,
    KeypadSubtract,
    KeypadMultiply,
    KeypadDivide
}

public readonly struct TerminalKeyInfo
{
    public TerminalKey Key { get; }
    public bool Ctrl { get; }
    public bool Alt { get; }
    public bool Shift { get; }

    public TerminalKeyInfo(TerminalKey key, bool ctrl, bool alt, bool shift)
    {
        Key = key;
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
    }
}
