using System.Text;
using XamlVisualEditor.Terminal;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class TerminalEmulatorTests
{
    [Fact]
    public void AppliesSgrForeground()
    {
        TerminalEmulator emulator = new(10, 5);
        byte[] data = Encoding.UTF8.GetBytes("\u001b[31mA");
        emulator.ProcessInput(data);

        TerminalCell cell = emulator.ActiveBuffer.GetLine(0).Cells[0];
        Assert.Equal(TerminalColor.FromIndex(1), cell.Attributes.Foreground);
    }

    [Fact]
    public void SwitchesAltBuffer()
    {
        TerminalEmulator emulator = new(10, 5);
        byte[] data = Encoding.UTF8.GetBytes("\u001b[?1049h");
        emulator.ProcessInput(data);

        Assert.True(emulator.State.AltBufferActive);

        data = Encoding.UTF8.GetBytes("\u001b[?1049l");
        emulator.ProcessInput(data);

        Assert.False(emulator.State.AltBufferActive);
    }

    [Fact]
    public void ScrollsWhenReachingBottom()
    {
        TerminalEmulator emulator = new(5, 2);
        byte[] data = Encoding.UTF8.GetBytes("Line1\r\nLine2\r\nLine3");
        emulator.ProcessInput(data);

        TerminalLine top = emulator.ActiveBuffer.GetLine(0);
        TerminalLine bottom = emulator.ActiveBuffer.GetLine(1);

        Assert.Equal('L', top.Cells[0].Rune.Value);
        Assert.Equal('L', bottom.Cells[0].Rune.Value);
    }

    [Fact]
    public void WrapsAfterLastColumn()
    {
        TerminalEmulator emulator = new(3, 2);
        byte[] data = Encoding.UTF8.GetBytes("ABC" + "D");
        emulator.ProcessInput(data);

        TerminalLine top = emulator.ActiveBuffer.GetLine(0);
        TerminalLine bottom = emulator.ActiveBuffer.GetLine(1);

        Assert.Equal('C', top.Cells[2].Rune.Value);
        Assert.Equal('D', bottom.Cells[0].Rune.Value);
    }

    [Fact]
    public void UsesDefaultTabStops()
    {
        TerminalEmulator emulator = new(16, 1);
        byte[] data = Encoding.UTF8.GetBytes("\tA");
        emulator.ProcessInput(data);

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('A', line.Cells[8].Rune.Value);
    }

    [Fact]
    public void HonorsCustomTabStop()
    {
        TerminalEmulator emulator = new(16, 1);
        byte[] data = Encoding.UTF8.GetBytes("\u001b[3g\u001b[1;6H\u001bH\r\tB");
        emulator.ProcessInput(data);

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('B', line.Cells[5].Rune.Value);
    }

    [Fact]
    public void InsertsCharactersInInsertMode()
    {
        TerminalEmulator emulator = new(5, 1);
        byte[] data = Encoding.UTF8.GetBytes("abcde\u001b[1;3H\u001b[4hZ");
        emulator.ProcessInput(data);

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('a', line.Cells[0].Rune.Value);
        Assert.Equal('b', line.Cells[1].Rune.Value);
        Assert.Equal('Z', line.Cells[2].Rune.Value);
        Assert.Equal('c', line.Cells[3].Rune.Value);
    }

    [Fact]
    public void OriginModeConstrainsCursorToScrollRegion()
    {
        TerminalEmulator emulator = new(5, 4);
        byte[] data = Encoding.UTF8.GetBytes("\u001b[2;3r\u001b[?6h\u001b[1;1HX");
        emulator.ProcessInput(data);

        TerminalLine line = emulator.ActiveBuffer.GetLine(1);
        Assert.Equal('X', line.Cells[0].Rune.Value);
    }
}
