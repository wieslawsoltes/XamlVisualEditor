using System;

namespace XamlVisualEditor.Terminal;

public enum TerminalMouseButton
{
    Left = 0,
    Middle = 1,
    Right = 2,
    None = 3,
    WheelUp = 64,
    WheelDown = 65
}

public enum TerminalMouseAction
{
    Press,
    Release,
    Move,
    Drag
}

public static class TerminalMouseEncoding
{
    public static string BuildX10(TerminalMouseButton button, int column, int row)
    {
        int code = (int)button;
        return $"\x1B[M{EncodeX10Byte(code + 32)}{EncodeX10Byte(column + 32)}{EncodeX10Byte(row + 32)}";
    }

    public static string BuildVt200(TerminalMouseButton button, TerminalMouseAction action, int column, int row)
    {
        int code = (int)button;
        if (action == TerminalMouseAction.Drag)
        {
            code += 32;
        }
        else if (action == TerminalMouseAction.Move && button == TerminalMouseButton.None)
        {
            code = 35;
        }

        return $"\x1B[M{EncodeX10Byte(code + 32)}{EncodeX10Byte(column + 32)}{EncodeX10Byte(row + 32)}";
    }

    public static string BuildSgr(TerminalMouseButton button, TerminalMouseAction action, int column, int row)
    {
        int code = (int)button;
        if (action == TerminalMouseAction.Drag)
        {
            code += 32;
        }
        else if (action == TerminalMouseAction.Move && button == TerminalMouseButton.None)
        {
            code = 35;
        }

        char suffix = action == TerminalMouseAction.Release ? 'm' : 'M';
        return $"\x1B[<{code};{column};{row}{suffix}";
    }

    private static char EncodeX10Byte(int value)
    {
        int clamped = Math.Clamp(value, 0, 255);
        return (char)clamped;
    }
}
