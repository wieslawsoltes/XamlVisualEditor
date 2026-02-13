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
    public void AltBufferSwitchResetsWrapPendingBeforeFirstWrite()
    {
        TerminalEmulator emulator = new(5, 3);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("12345"));
        Assert.True(emulator.State.WrapPending);

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?1049hA"));

        Assert.True(emulator.State.AltBufferActive);
        TerminalLine first = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('A', first.Cells[0].Rune.Value);
    }

    [Fact]
    public void Mode1049RestoresCursorWhenLeavingAltBuffer()
    {
        TerminalEmulator emulator = new(10, 5);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[2;4H\u001b[?1049h\u001b[1;1H\u001b[?1049l"));

        Assert.False(emulator.State.AltBufferActive);
        Assert.Equal(1, emulator.State.CursorRow);
        Assert.Equal(3, emulator.State.CursorColumn);
    }

    [Fact]
    public void AltBufferResizeDoesNotReflowVisibleRows()
    {
        TerminalEmulator emulator = new(10, 4);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?1049h123456789\u001b[3;1HZZZ"));

        emulator.Resize(5, 4);

        TerminalLine row2 = emulator.ActiveBuffer.GetLine(2);
        Assert.Equal('Z', (char)row2.Cells[0].Rune.Value);
    }

    [Fact]
    public void AltBufferResizeStillReflowsMainBufferScrollback()
    {
        TerminalEmulator emulator = new(5, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("line1\nline2\nline3\nline4\n"));
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?1049h"));

        emulator.Resize(10, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?1049l"));

        emulator.Read((buffer, _) =>
        {
            Assert.Equal(10, buffer.Columns);
            Assert.NotEmpty(buffer.Scrollback);
            Assert.Equal(10, buffer.Scrollback[0].Cells.Length);
        });
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
    public void DecslrmIsAppliedWhenLeftRightMarginModeIsEnabled()
    {
        TerminalEmulator emulator = new(10, 4);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?69h\u001b[2;6s"));

        Assert.True(emulator.State.LeftRightMarginMode);
        Assert.Equal(1, emulator.State.ScrollLeft);
        Assert.Equal(5, emulator.State.ScrollRight);
    }

    [Fact]
    public void DecslrmWithoutParametersResetsMarginsToFullWidth()
    {
        TerminalEmulator emulator = new(10, 4);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?69h\u001b[2;6s\u001b[s"));

        Assert.Equal(0, emulator.State.ScrollLeft);
        Assert.Equal(9, emulator.State.ScrollRight);
    }

    [Fact]
    public void DecstbmZeroParametersResetRegionToFullHeight()
    {
        TerminalEmulator emulator = new(10, 6);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[2;4r\u001b[0;0r"));

        Assert.Equal(0, emulator.State.ScrollTop);
        Assert.Equal(5, emulator.State.ScrollBottom);
    }

    [Fact]
    public void DecstbmMissingBottomDefaultsToTerminalBottom()
    {
        TerminalEmulator emulator = new(10, 6);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[2;4r\u001b[1;r"));

        Assert.Equal(0, emulator.State.ScrollTop);
        Assert.Equal(5, emulator.State.ScrollBottom);
    }

    [Fact]
    public void DecslrmZeroRightDefaultsToTerminalWidth()
    {
        TerminalEmulator emulator = new(10, 4);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?69h\u001b[3;7s\u001b[2;0s"));

        Assert.Equal(1, emulator.State.ScrollLeft);
        Assert.Equal(9, emulator.State.ScrollRight);
    }

    [Fact]
    public void DecslrmLeadingSemicolonDefaultsToFullWidth()
    {
        TerminalEmulator emulator = new(10, 4);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?69h\u001b[3;7s\u001b[;s"));

        Assert.Equal(0, emulator.State.ScrollLeft);
        Assert.Equal(9, emulator.State.ScrollRight);
    }

    [Fact]
    public void OriginModeCarriageReturnReturnsToLeftMargin()
    {
        TerminalEmulator emulator = new(10, 3);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?69h\u001b[2;6s\u001b[?6hABC\rZ"));

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('Z', line.Cells[1].Rune.Value);
        Assert.Equal('B', line.Cells[2].Rune.Value);
    }

    [Fact]
    public void CarriageReturnRightOfLeftMarginMovesToLeftMargin()
    {
        TerminalEmulator emulator = new(8, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?69h\u001b[3;6s\u001b[1;5HX\rZ"));

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('Z', line.Cells[2].Rune.Value);
    }

    [Fact]
    public void CarriageReturnLeftOfLeftMarginMovesToColumnZero()
    {
        TerminalEmulator emulator = new(8, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?69h\u001b[3;6s\u001b[1;2H\rZ"));

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('Z', line.Cells[0].Rune.Value);
    }

    [Fact]
    public void CarriageReturnRightOfRightMarginMovesToLeftMargin()
    {
        TerminalEmulator emulator = new(10, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?69h\u001b[3;6s\u001b[1;9H\rZ"));

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('Z', line.Cells[2].Rune.Value);
    }

    [Fact]
    public void ScrollUpRespectsLeftAndRightMargins()
    {
        TerminalEmulator emulator = new(8, 3);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[1;1H11111111\u001b[2;1H22222222\u001b[3;1H33333333"));
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?69h\u001b[3;6s\u001b[3;3H\n"));

        TerminalLine row0 = emulator.ActiveBuffer.GetLine(0);
        TerminalLine row1 = emulator.ActiveBuffer.GetLine(1);
        TerminalLine row2 = emulator.ActiveBuffer.GetLine(2);

        Assert.Equal('1', (char)row0.Cells[0].Rune.Value);
        Assert.Equal('2', (char)row0.Cells[2].Rune.Value);
        Assert.Equal('1', (char)row0.Cells[7].Rune.Value);

        Assert.Equal('2', (char)row1.Cells[0].Rune.Value);
        Assert.Equal('3', (char)row1.Cells[2].Rune.Value);
        Assert.Equal('2', (char)row1.Cells[7].Rune.Value);

        Assert.Equal('3', (char)row2.Cells[0].Rune.Value);
        Assert.Equal(' ', (char)row2.Cells[2].Rune.Value);
        Assert.Equal('3', (char)row2.Cells[7].Rune.Value);
    }

    [Fact]
    public void HorizontalTabRespectsRightMargin()
    {
        TerminalEmulator emulator = new(10, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?69h\u001b[3;6s\u001b[1;2H\tA"));

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('A', (char)line.Cells[5].Rune.Value);
    }

    [Fact]
    public void BackTabInOriginModeStopsAtLeftMargin()
    {
        TerminalEmulator emulator = new(10, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?69h\u001b[3;6s\u001b[?6h\u001b[1;6H\u001b[ZA"));

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('A', (char)line.Cells[2].Rune.Value);
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
        string input = "\u001b[?2004h\u001b[?2004s\u001b[?2004l\u001b[?2004r";
        emulator.ProcessInput(Encoding.UTF8.GetBytes(input));

        Assert.True(emulator.State.BracketedPaste);
    }

    [Fact]
    public void PrivateModeRestoreUnknownModeDoesNotAffectBracketedPaste()
    {
        TerminalEmulator emulator = new(5, 2);
        string input = "\u001b[?2004h\u001b[?1001s\u001b[?2004l\u001b[?1001r";
        emulator.ProcessInput(Encoding.UTF8.GetBytes(input));

        Assert.False(emulator.State.BracketedPaste);
    }

    [Fact]
    public void PrivateModeRestoreCanRestoreCursorVisibility()
    {
        TerminalEmulator emulator = new(5, 2);
        string input = "\u001b[?25l\u001b[?25s\u001b[?25h\u001b[?25r";
        emulator.ProcessInput(Encoding.UTF8.GetBytes(input));

        Assert.False(emulator.State.CursorVisible);
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
    public void CursorColumnAbsoluteClearsPendingWrap()
    {
        TerminalEmulator emulator = new(5, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("ABCDE"));
        Assert.True(emulator.State.WrapPending);

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[1GZ"));

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('Z', (char)line.Cells[0].Rune.Value);
        Assert.False(emulator.State.WrapPending);
    }

    [Fact]
    public void Mode1048RestoreRestoresSavedAttributes()
    {
        TerminalEmulator emulator = new(5, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[31m\u001b[?1048h\u001b[32m\u001b[?1048lA"));

        TerminalCell cell = emulator.ActiveBuffer.GetLine(0).Cells[0];
        Assert.Equal(TerminalColor.FromIndex(1), cell.Attributes.Foreground);
    }

    [Fact]
    public void ResetRestoresDefaultTabStops()
    {
        TerminalEmulator emulator = new(16, 1);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[3g\u001b[1;6H\u001bH\u001bc\tA"));

        TerminalLine line = emulator.ActiveBuffer.GetLine(0);
        Assert.Equal('A', (char)line.Cells[8].Rune.Value);
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

    [Fact]
    public void CsiWindowOp18ReportsTerminalRowsAndColumns()
    {
        TerminalEmulator emulator = new(80, 24);
        string? response = null;
        emulator.ResponseRequested += data => response = data;

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[18t"));

        Assert.Equal("\u001b[8;24;80t", response);
    }

    [Fact]
    public void CsiWindowOps14And16ReportPixelAndCellMetrics()
    {
        TerminalEmulator emulator = new(80, 24);
        List<string> responses = new();
        emulator.ResponseRequested += data => responses.Add(data);
        emulator.SetDisplayMetrics(cellWidthPx: 9, cellHeightPx: 18, pixelWidthPx: 720, pixelHeightPx: 432);

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[14t\u001b[16t"));

        Assert.Contains("\u001b[4;432;720t", responses);
        Assert.Contains("\u001b[6;18;9t", responses);
    }

    [Fact]
    public void WindowOp21ReportsWindowTitle()
    {
        TerminalEmulator emulator = new(80, 24);
        string? response = null;
        emulator.ResponseRequested += data => response = data;
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b]2;demo-title\u0007"));

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[21t"));

        Assert.Equal("\u001b]ldemo-title\u001b\\", response);
    }

    [Fact]
    public void WindowOps22And23PushAndPopWindowTitle()
    {
        TerminalEmulator emulator = new(80, 24);
        List<string> titles = new();
        emulator.TitleChanged += title => titles.Add(title);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b]2;old\u0007"));
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[22;2t"));
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b]2;new\u0007"));

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[23;2t"));

        Assert.Equal("old", emulator.State.WindowTitle);
        Assert.NotEmpty(titles);
        Assert.Equal("old", titles[^1]);
    }

    [Fact]
    public void CsiSpaceQSetsCursorShapeAndBlink()
    {
        TerminalEmulator emulator = new(10, 3);

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[6 q"));

        Assert.Equal(TerminalCursorShape.Bar, emulator.State.CursorShape);
        Assert.False(emulator.State.CursorBlink);
    }

    [Fact]
    public void CsiPrivatePrefixSpaceQIsIgnored()
    {
        TerminalEmulator emulator = new(10, 3);
        List<string> unhandled = new();
        emulator.UnhandledSequence += data => unhandled.Add(data);

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?0 q"));

        Assert.Equal(TerminalCursorShape.Block, emulator.State.CursorShape);
        Assert.True(emulator.State.CursorBlink);
        Assert.Contains("CSI ?0  q", unhandled);
    }

    [Fact]
    public void CsiSpaceAtScrollLeftShiftsWithinScrollRegion()
    {
        TerminalEmulator emulator = new(6, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("ABCDEF\r\n123456\u001b[1;1H"));

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[2 @"));

        TerminalLine row0 = emulator.ActiveBuffer.GetLine(0);
        TerminalLine row1 = emulator.ActiveBuffer.GetLine(1);
        Assert.Equal('C', (char)row0.Cells[0].Rune.Value);
        Assert.Equal('D', (char)row0.Cells[1].Rune.Value);
        Assert.Equal(' ', (char)row0.Cells[4].Rune.Value);
        Assert.Equal('3', (char)row1.Cells[0].Rune.Value);
        Assert.Equal('4', (char)row1.Cells[1].Rune.Value);
        Assert.Equal(' ', (char)row1.Cells[4].Rune.Value);
    }

    [Fact]
    public void CsiSpaceAScrollRightShiftsWithinScrollRegion()
    {
        TerminalEmulator emulator = new(6, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("ABCDEF\r\n123456\u001b[1;1H"));

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[2 A"));

        TerminalLine row0 = emulator.ActiveBuffer.GetLine(0);
        TerminalLine row1 = emulator.ActiveBuffer.GetLine(1);
        Assert.Equal(' ', (char)row0.Cells[0].Rune.Value);
        Assert.Equal(' ', (char)row0.Cells[1].Rune.Value);
        Assert.Equal('A', (char)row0.Cells[2].Rune.Value);
        Assert.Equal(' ', (char)row1.Cells[0].Rune.Value);
        Assert.Equal(' ', (char)row1.Cells[1].Rune.Value);
        Assert.Equal('1', (char)row1.Cells[2].Rune.Value);
    }

    [Fact]
    public void DecicInsertsColumnsAcrossScrollRegion()
    {
        TerminalEmulator emulator = new(6, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("ABCDEF\r\n123456\u001b[1;3H"));

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[2'}"));

        TerminalLine row0 = emulator.ActiveBuffer.GetLine(0);
        TerminalLine row1 = emulator.ActiveBuffer.GetLine(1);
        Assert.Equal('A', (char)row0.Cells[0].Rune.Value);
        Assert.Equal('B', (char)row0.Cells[1].Rune.Value);
        Assert.Equal(' ', (char)row0.Cells[2].Rune.Value);
        Assert.Equal(' ', (char)row0.Cells[3].Rune.Value);
        Assert.Equal('C', (char)row0.Cells[4].Rune.Value);
        Assert.Equal('1', (char)row1.Cells[0].Rune.Value);
        Assert.Equal('2', (char)row1.Cells[1].Rune.Value);
        Assert.Equal(' ', (char)row1.Cells[2].Rune.Value);
        Assert.Equal(' ', (char)row1.Cells[3].Rune.Value);
        Assert.Equal('3', (char)row1.Cells[4].Rune.Value);
    }

    [Fact]
    public void DecdcDeletesColumnsAcrossScrollRegion()
    {
        TerminalEmulator emulator = new(6, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("ABCDEF\r\n123456\u001b[1;3H"));

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[2'~"));

        TerminalLine row0 = emulator.ActiveBuffer.GetLine(0);
        TerminalLine row1 = emulator.ActiveBuffer.GetLine(1);
        Assert.Equal('A', (char)row0.Cells[0].Rune.Value);
        Assert.Equal('B', (char)row0.Cells[1].Rune.Value);
        Assert.Equal('E', (char)row0.Cells[2].Rune.Value);
        Assert.Equal('F', (char)row0.Cells[3].Rune.Value);
        Assert.Equal(' ', (char)row0.Cells[4].Rune.Value);
        Assert.Equal('1', (char)row1.Cells[0].Rune.Value);
        Assert.Equal('2', (char)row1.Cells[1].Rune.Value);
        Assert.Equal('5', (char)row1.Cells[2].Rune.Value);
        Assert.Equal('6', (char)row1.Cells[3].Rune.Value);
        Assert.Equal(' ', (char)row1.Cells[4].Rune.Value);
    }

    [Fact]
    public void Dsr6InOriginModeReportsRelativePosition()
    {
        TerminalEmulator emulator = new(10, 6);
        string? response = null;
        emulator.ResponseRequested += data => response = data;
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[2;5r\u001b[?6h\u001b[1;1H"));

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[6n"));

        Assert.Equal("\u001b[1;1R", response);
    }

    [Fact]
    public void DecDsr6UsesQuestionMarkPrefixAndOriginRelativeMargins()
    {
        TerminalEmulator emulator = new(12, 6);
        string? response = null;
        emulator.ResponseRequested += data => response = data;
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[2;5r\u001b[?69h\u001b[3;8s\u001b[?6h\u001b[1;1H"));

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?6n"));

        Assert.Equal("\u001b[?1;1R", response);
    }

    [Fact]
    public void SgrColonFormRgbColorIsParsed()
    {
        TerminalEmulator emulator = new(4, 1);

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[38:2::1:2:3mA"));

        TerminalCell cell = emulator.ActiveBuffer.GetLine(0).Cells[0];
        Assert.Equal(TerminalColor.FromRgb(1, 2, 3), cell.Attributes.Foreground);
    }

    [Fact]
    public void DecMode3ResizesOnlyWhenMode40IsEnabled()
    {
        TerminalEmulator emulator = new(80, 4);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("ABCD"));

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?3h"));

        Assert.Equal(80, emulator.ActiveBuffer.Columns);
        Assert.False(emulator.State.Column132Mode);

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?40h\u001b[?3h"));

        Assert.Equal(132, emulator.ActiveBuffer.Columns);
        Assert.True(emulator.State.Column132Mode);
        Assert.Equal(0, emulator.State.CursorRow);
        Assert.Equal(0, emulator.State.CursorColumn);
        Assert.Equal(' ', (char)emulator.ActiveBuffer.GetLine(0).Cells[0].Rune.Value);

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?3l"));

        Assert.Equal(80, emulator.ActiveBuffer.Columns);
        Assert.False(emulator.State.Column132Mode);
    }

    [Fact]
    public void DecMode1007TogglesAlternateScrollMode()
    {
        TerminalEmulator emulator = new(80, 4);
        Assert.True(emulator.State.MouseAlternateScroll);

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?1007l"));
        Assert.False(emulator.State.MouseAlternateScroll);

        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[?1007h"));
        Assert.True(emulator.State.MouseAlternateScroll);
    }

    [Fact]
    public void DecstrSoftResetRestoresCoreModesWithoutClearingBuffer()
    {
        TerminalEmulator emulator = new(8, 2);
        emulator.ProcessInput(Encoding.UTF8.GetBytes("\u001b[31mX\u001b[4h\u001b[?6h\u001b[?1007l\u001b[!p"));

        TerminalCell cell = emulator.ActiveBuffer.GetLine(0).Cells[0];
        Assert.Equal('X', (char)cell.Rune.Value);
        Assert.False(emulator.State.InsertMode);
        Assert.False(emulator.State.OriginMode);
        Assert.True(emulator.State.AutoWrap);
        Assert.True(emulator.State.MouseAlternateScroll);
        Assert.Equal(TerminalAttributes.Default, emulator.State.Attributes);
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
