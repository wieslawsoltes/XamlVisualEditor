using System;

namespace XamlVisualEditor.Terminal;

public static class TerminalPalette
{
    private static readonly TerminalRgb[] s_palette = BuildPalette();

    public static TerminalRgb Resolve(TerminalColor color, TerminalTheme theme)
    {
        return color.Kind switch
        {
            TerminalColorKind.Default => theme.Foreground,
            TerminalColorKind.Indexed => ResolveIndex(color.Index),
            TerminalColorKind.Rgb => new TerminalRgb(color.R, color.G, color.B),
            _ => theme.Foreground
        };
    }

    public static TerminalRgb ResolveBackground(TerminalColor color, TerminalTheme theme)
    {
        return color.Kind switch
        {
            TerminalColorKind.Default => theme.Background,
            TerminalColorKind.Indexed => ResolveIndex(color.Index),
            TerminalColorKind.Rgb => new TerminalRgb(color.R, color.G, color.B),
            _ => theme.Background
        };
    }

    public static TerminalRgb ResolveIndex(byte index)
    {
        return s_palette[index];
    }

    private static TerminalRgb[] BuildPalette()
    {
        TerminalRgb[] palette = new TerminalRgb[256];
        TerminalRgb[] baseColors =
        {
            new(0x00, 0x00, 0x00),
            new(0xCD, 0x00, 0x00),
            new(0x00, 0xCD, 0x00),
            new(0xCD, 0xCD, 0x00),
            new(0x00, 0x00, 0xEE),
            new(0xCD, 0x00, 0xCD),
            new(0x00, 0xCD, 0xCD),
            new(0xE5, 0xE5, 0xE5),
            new(0x7F, 0x7F, 0x7F),
            new(0xFF, 0x00, 0x00),
            new(0x00, 0xFF, 0x00),
            new(0xFF, 0xFF, 0x00),
            new(0x5C, 0x5C, 0xFF),
            new(0xFF, 0x00, 0xFF),
            new(0x00, 0xFF, 0xFF),
            new(0xFF, 0xFF, 0xFF)
        };

        Array.Copy(baseColors, 0, palette, 0, baseColors.Length);

        int index = 16;
        int[] steps = { 0x00, 0x5F, 0x87, 0xAF, 0xD7, 0xFF };
        for (int r = 0; r < steps.Length; r++)
        {
            for (int g = 0; g < steps.Length; g++)
            {
                for (int b = 0; b < steps.Length; b++)
                {
                    palette[index++] = new TerminalRgb((byte)steps[r], (byte)steps[g], (byte)steps[b]);
                }
            }
        }

        for (int i = 0; i < 24; i++)
        {
            byte level = (byte)(8 + i * 10);
            palette[index++] = new TerminalRgb(level, level, level);
        }

        return palette;
    }
}
