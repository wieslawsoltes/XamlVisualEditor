using System.Collections.Generic;
using System.Text;
using XamlVisualEditor.Terminal;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class TerminalBufferTests
{
    [Fact]
    public void GetLineGlobalReturnsScrollbackAndLive()
    {
        TerminalAttributes attrs = TerminalAttributes.Default;
        TerminalBuffer buffer = new(4, 2, attrs);
        FillLine(buffer.GetLine(0), "ABCD", attrs);
        FillLine(buffer.GetLine(1), "EFGH", attrs);

        buffer.ScrollUp(0, 1, attrs);

        TerminalLine scrollbackLine = buffer.GetLineGlobal(0);
        TerminalLine liveLine = buffer.GetLineGlobal(1);

        Assert.Equal('A', scrollbackLine.Cells[0].Rune.Value);
        Assert.Equal('E', liveLine.Cells[0].Rune.Value);
    }

    [Fact]
    public void ReflowResizeWithMappingGlobalKeepsScrollbackPosition()
    {
        TerminalAttributes attrs = TerminalAttributes.Default;
        TerminalBuffer buffer = new(4, 2, attrs);
        FillLine(buffer.GetLine(0), "ABCD", attrs);
        FillLine(buffer.GetLine(1), "EFGH", attrs);

        buffer.ScrollUp(0, 1, attrs);

        TerminalCellPosition[] positions = { new TerminalCellPosition(0, 1) };
        IReadOnlyList<TerminalCellPosition> mapped = buffer.ReflowResizeWithMappingGlobal(2, 2, attrs, positions);

        Assert.Single(mapped);
        Assert.Equal(0, mapped[0].Row);
        Assert.Equal(1, mapped[0].Column);
    }

    private static void FillLine(TerminalLine line, string text, TerminalAttributes attrs)
    {
        for (int i = 0; i < line.Cells.Length; i++)
        {
            line.Cells[i] = TerminalCell.Empty(attrs);
        }

        int length = Math.Min(text.Length, line.Cells.Length);
        for (int i = 0; i < length; i++)
        {
            line.Cells[i] = new TerminalCell(new Rune(text[i]), 1, attrs);
        }

        line.IsWrapped = false;
    }
}
