using System;

namespace XamlVisualEditor.Terminal;

public enum TerminalColorKind : byte
{
    Default,
    Indexed,
    Rgb
}

public readonly struct TerminalColor : IEquatable<TerminalColor>
{
    public TerminalColorKind Kind { get; }
    public byte Index { get; }
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }

    private TerminalColor(TerminalColorKind kind, byte index, byte r, byte g, byte b)
    {
        Kind = kind;
        Index = index;
        R = r;
        G = g;
        B = b;
    }

    public static TerminalColor Default => new(TerminalColorKind.Default, 0, 0, 0, 0);

    public static TerminalColor FromIndex(byte index)
    {
        return new TerminalColor(TerminalColorKind.Indexed, index, 0, 0, 0);
    }

    public static TerminalColor FromRgb(byte r, byte g, byte b)
    {
        return new TerminalColor(TerminalColorKind.Rgb, 0, r, g, b);
    }

    public bool Equals(TerminalColor other)
    {
        return Kind == other.Kind && Index == other.Index && R == other.R && G == other.G && B == other.B;
    }

    public override bool Equals(object? obj)
    {
        return obj is TerminalColor other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)Kind, Index, R, G, B);
    }

    public static bool operator ==(TerminalColor left, TerminalColor right) => left.Equals(right);
    public static bool operator !=(TerminalColor left, TerminalColor right) => !left.Equals(right);
}

public readonly struct TerminalRgb
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }

    public TerminalRgb(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }
}
