using System.Collections.Generic;
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

    [Fact]
    public void EscPercentSelectsCharsetEncoding()
    {
        TerminalEmulator emulator = new(3, 1);
        byte[] data = { 0x1B, (byte)'%', (byte)'@', 0xA3 };
        emulator.ProcessInput(data);

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal(0x00A3, line.Cells[0].Rune.Value);
        Assert.False(emulator.State.Utf8Mode);
    }

    [Fact]
    public void EscParenBarSelectsDecSupplemental()
    {
        TerminalEmulator emulator = new(3, 1);
        byte[] data = { 0x1B, (byte)'(', (byte)'|', (byte)'!' };
        emulator.ProcessInput(data);

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal(0x00A1, line.Cells[0].Rune.Value);
    }

    [Fact]
    public void BracketedPasteModeToggles()
    {
        TerminalEmulator emulator = new(5, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?2004h"));
        Assert.True(emulator.State.BracketedPaste);

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?2004l"));
        Assert.False(emulator.State.BracketedPaste);
    }

    [Fact]
    public void PrivateModeSaveDoesNotOverrideSavedCursor()
    {
        TerminalEmulator emulator = new(5, 3);
        string input = "\u001b[2;3H\u001b[s\u001b[1;1H\u001b[?1001s\u001b[uX";
        emulator.ProcessInput(Encoding.UTF8.GetBytes(input));

        TerminalLine line = emulator.ActiveBuffer.GetLine(1);
        Assert.Equal('X', line.Cells[2].Rune.Value);
    }

    [Fact]
    public void PrivateModeRestoreRestoresBracketedPaste()
    {
        TerminalEmulator emulator = new(5, 2);
        string input = "\u001b[?2004h\u001b[?1001s\u001b[?2004l\u001b[?1001r";
        emulator.ProcessInput(Encoding.UTF8.GetBytes(input));

        Assert.True(emulator.State.BracketedPaste);
    }

    [Fact]
    public void C1CsiSequencesAreHandled()
    {
        TerminalEmulator emulator = new(2, 1);
        byte[] data = { 0x9B, (byte)'3', (byte)'1', (byte)'m', (byte)'A' };
        emulator.ProcessInput(data);

        TerminalCell cell = emulator.ActiveBuffer.GetLine(0).Cells[0];
        Assert.Equal(TerminalColor.FromIndex(1), cell.Attributes.Foreground);
    }

    [Fact]
    public void C1NelMovesToNextLine()
    {
        TerminalEmulator emulator = new(2, 2);
        byte[] data = { 0x85, (byte)'B' };
        emulator.ProcessInput(data);

        TerminalLine line = emulator.ActiveBuffer.GetLine(1);
        Assert.Equal('B', line.Cells[0].Rune.Value);
    }

    [Fact]
    public void Utf8BoxDrawingIsNotTreatedAsC1()
    {
        TerminalEmulator emulator = new(2, 1);
        byte[] data = { 0xE2, 0x94, 0x80 };
        emulator.ProcessInput(data);

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('─', (char)line.Cells[0].Rune.Value);
    }

    [Fact]
    public void IncompleteUtf8DoesNotDropEscapeSequences()
    {
        TerminalEmulator emulator = new(2, 1);
        emulator.ProcessInput(new byte[] { 0xE2 });
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[31mA"));

        TerminalCell cell = emulator.ActiveBuffer.GetLine(0).Cells[0];
        Assert.Equal('A', (char)cell.Rune.Value);
        Assert.Equal(TerminalColor.FromIndex(1), cell.Attributes.Foreground);
    }

    [Fact]
    public void VerticalGrowKeepsTopAnchoredPromptAfterInitialShrink()
    {
        TerminalEmulator emulator = new(120, 40);
        emulator.Resize(120, 12);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("PROMPT"));

        emulator.Resize(120, 30);

        TerminalLine top = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('P', (char)top.Cells[0].Rune.Value);
    }

    public static IEnumerable<object[]> NrcCharsetCases => new[]
    {
        new object[] { 'A', "£@[\\]^_`{|}~" },
        new object[] { '4', "£¾ĳ½|^_`¨ƒ¼´" },
        new object[] { 'C', "#@ÄÖÅÜ_éäöåü" },
        new object[] { '5', "#@ÄÖÅÜ_éäöåü" },
        new object[] { 'R', "£à°ç§^_`éùè¨" },
        new object[] { 'Q', "#àâçêî_ôéùèû" },
        new object[] { 'K', "#§ÄÖÜ^_`äöüß" },
        new object[] { 'Y', "£§°çé^_ùàòèì" },
        new object[] { 'E', "#ÄÆØÅÜ_äæøåü" },
        new object[] { '6', "#ÄÆØÅÜ_äæøåü" },
        new object[] { 'Z', "£§¡Ñ¿^_`°ñç~" },
        new object[] { 'H', "#ÉÄÖÅÜ_éäöåü" },
        new object[] { '7', "#ÉÄÖÅÜ_éäöåü" },
        new object[] { '=', "ùàéçêîèôäöüû" }
    };

    [Theory]
    [MemberData(nameof(NrcCharsetCases))]
    public void EscParenSelectsNrcCharset(char designator, string expected)
    {
        TerminalEmulator emulator = new(12, 1);
        string input = "\u001b(" + designator + "#@[\\]^_`{|}~";
        emulator.ProcessInput(Encoding.UTF8.GetBytes(input));

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], (char)line.Cells[i].Rune.Value);
        }
    }
}
