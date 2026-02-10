using XamlVisualEditor.Terminal;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class TerminalKeyMapperTests
{
    [Fact]
    public void ShiftTabMapsToBacktab()
    {
        TerminalKeyInfo key = new(TerminalKey.Tab, ctrl: false, alt: false, shift: true);
        string? sequence = TerminalKeyMapper.Map(key, new TerminalState());

        Assert.Equal("\x1B[Z", sequence);
    }

    [Fact]
    public void ApplicationCursorKeysUseSs3()
    {
        TerminalState state = new() { ApplicationCursorKeys = true };
        TerminalKeyInfo key = new(TerminalKey.Up, ctrl: false, alt: false, shift: false);
        string? sequence = TerminalKeyMapper.Map(key, state);

        Assert.Equal("\x1BOA", sequence);
    }

    [Fact]
    public void ApplicationKeypadUsesSs3()
    {
        TerminalState state = new() { ApplicationKeypad = true };
        TerminalKeyInfo key = new(TerminalKey.Keypad1, ctrl: false, alt: false, shift: false);
        string? sequence = TerminalKeyMapper.Map(key, state);

        Assert.Equal("\x1BOq", sequence);
    }
}
