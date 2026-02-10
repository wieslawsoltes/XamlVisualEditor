using System.Collections.Generic;

namespace XamlVisualEditor.Terminal;

public static class TerminalKeyMapper
{
    private static readonly Dictionary<TerminalKey, string> s_basicMap = new()
    {
        [TerminalKey.Enter] = "\r",
        [TerminalKey.Backspace] = "\x7F",
        [TerminalKey.Tab] = "\t",
        [TerminalKey.Escape] = "\x1B",
        [TerminalKey.Up] = "\x1B[A",
        [TerminalKey.Down] = "\x1B[B",
        [TerminalKey.Right] = "\x1B[C",
        [TerminalKey.Left] = "\x1B[D",
        [TerminalKey.Home] = "\x1B[H",
        [TerminalKey.End] = "\x1B[F",
        [TerminalKey.PageUp] = "\x1B[5~",
        [TerminalKey.PageDown] = "\x1B[6~",
        [TerminalKey.Insert] = "\x1B[2~",
        [TerminalKey.Delete] = "\x1B[3~",
        [TerminalKey.F1] = "\x1BOP",
        [TerminalKey.F2] = "\x1BOQ",
        [TerminalKey.F3] = "\x1BOR",
        [TerminalKey.F4] = "\x1BOS",
        [TerminalKey.F5] = "\x1B[15~",
        [TerminalKey.F6] = "\x1B[17~",
        [TerminalKey.F7] = "\x1B[18~",
        [TerminalKey.F8] = "\x1B[19~",
        [TerminalKey.F9] = "\x1B[20~",
        [TerminalKey.F10] = "\x1B[21~",
        [TerminalKey.F11] = "\x1B[23~",
        [TerminalKey.F12] = "\x1B[24~",
        [TerminalKey.Keypad0] = "0",
        [TerminalKey.Keypad1] = "1",
        [TerminalKey.Keypad2] = "2",
        [TerminalKey.Keypad3] = "3",
        [TerminalKey.Keypad4] = "4",
        [TerminalKey.Keypad5] = "5",
        [TerminalKey.Keypad6] = "6",
        [TerminalKey.Keypad7] = "7",
        [TerminalKey.Keypad8] = "8",
        [TerminalKey.Keypad9] = "9",
        [TerminalKey.KeypadDecimal] = ".",
        [TerminalKey.KeypadEnter] = "\r",
        [TerminalKey.KeypadAdd] = "+",
        [TerminalKey.KeypadSubtract] = "-",
        [TerminalKey.KeypadMultiply] = "*",
        [TerminalKey.KeypadDivide] = "/"
    };

    private static readonly Dictionary<TerminalKey, string> s_applicationCursorMap = new()
    {
        [TerminalKey.Up] = "\x1BOA",
        [TerminalKey.Down] = "\x1BOB",
        [TerminalKey.Right] = "\x1BOC",
        [TerminalKey.Left] = "\x1BOD",
        [TerminalKey.Home] = "\x1BOH",
        [TerminalKey.End] = "\x1BOF"
    };

    private static readonly Dictionary<TerminalKey, string> s_applicationKeypadMap = new()
    {
        [TerminalKey.Keypad0] = "\x1BOp",
        [TerminalKey.Keypad1] = "\x1BOq",
        [TerminalKey.Keypad2] = "\x1BOr",
        [TerminalKey.Keypad3] = "\x1BOs",
        [TerminalKey.Keypad4] = "\x1BOt",
        [TerminalKey.Keypad5] = "\x1BOu",
        [TerminalKey.Keypad6] = "\x1BOv",
        [TerminalKey.Keypad7] = "\x1BOw",
        [TerminalKey.Keypad8] = "\x1BOx",
        [TerminalKey.Keypad9] = "\x1BOy",
        [TerminalKey.KeypadDecimal] = "\x1BOn",
        [TerminalKey.KeypadEnter] = "\x1BOM",
        [TerminalKey.KeypadAdd] = "\x1BOk",
        [TerminalKey.KeypadSubtract] = "\x1BOm",
        [TerminalKey.KeypadMultiply] = "\x1BOj",
        [TerminalKey.KeypadDivide] = "\x1BOo"
    };

    public static string? Map(TerminalKeyInfo key)
    {
        TerminalState state = new();
        return Map(key, state);
    }

    public static string? Map(TerminalKeyInfo key, TerminalState state)
    {
        if (key.Key == TerminalKey.Tab && key.Shift)
        {
            return "\x1B[Z";
        }

        if (state.ApplicationKeypad && s_applicationKeypadMap.TryGetValue(key.Key, out string? keypadSequence))
        {
            if (key.Ctrl || key.Alt || key.Shift)
            {
                return ApplyModifier(keypadSequence, key);
            }

            return keypadSequence;
        }

        if (state.ApplicationCursorKeys && s_applicationCursorMap.TryGetValue(key.Key, out string? cursorSequence))
        {
            if (key.Ctrl || key.Alt || key.Shift)
            {
                return ApplyModifier(cursorSequence, key);
            }

            return cursorSequence;
        }

        if (!s_basicMap.TryGetValue(key.Key, out string? sequence))
        {
            return null;
        }

        if (key.Ctrl || key.Alt || key.Shift)
        {
            return ApplyModifier(sequence, key);
        }

        return sequence;
    }

    private static string ApplyModifier(string sequence, TerminalKeyInfo key)
    {
        int modifier = 1;
        if (key.Shift) modifier += 1;
        if (key.Alt) modifier += 2;
        if (key.Ctrl) modifier += 4;

        if (sequence.StartsWith("\x1B[") && sequence.EndsWith("~"))
        {
            string core = sequence[2..^1];
            return $"\x1B[{core};{modifier}~";
        }

        if (sequence.StartsWith("\x1B[") && sequence.Length == 3)
        {
            char code = sequence[^1];
            return $"\x1B[1;{modifier}{code}";
        }

        if (sequence.StartsWith("\x1BO"))
        {
            char code = sequence[^1];
            return $"\x1B[1;{modifier}{code}";
        }

        return sequence;
    }
}
