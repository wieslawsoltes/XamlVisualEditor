using System;
using System.Text;

namespace XamlVisualEditor.Terminal;

public static class UnicodeWidth
{
    private static readonly (int Start, int End)[] WideRanges =
    {
        (0x1100, 0x115F),
        (0x2329, 0x232A),
        (0x2E80, 0xA4CF),
        (0xAC00, 0xD7A3),
        (0xF900, 0xFAFF),
        (0xFE10, 0xFE19),
        (0xFE30, 0xFE6F),
        (0xFF00, 0xFF60),
        (0xFFE0, 0xFFE6),
        (0x1F300, 0x1F64F),
        (0x1F900, 0x1F9FF),
        (0x20000, 0x3FFFD)
    };

    public static int GetWidth(Rune rune)
    {
        if (rune.Value == 0)
        {
            return 0;
        }

        if (rune.Value < 0x20 || (rune.Value >= 0x7F && rune.Value < 0xA0))
        {
            return 0;
        }

        return IsWide(rune.Value) ? 2 : 1;
    }

    private static bool IsWide(int codePoint)
    {
        int lo = 0;
        int hi = WideRanges.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            (int start, int end) = WideRanges[mid];
            if (codePoint < start)
            {
                hi = mid - 1;
            }
            else if (codePoint > end)
            {
                lo = mid + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
