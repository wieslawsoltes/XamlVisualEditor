namespace XamlVisualEditor.Terminal;

public interface ITerminalEmulatorFactory
{
    ITerminalEmulator Create(int columns, int rows);
}

public sealed class ManagedTerminalEmulatorFactory : ITerminalEmulatorFactory
{
    public ITerminalEmulator Create(int columns, int rows)
    {
        return new TerminalEmulator(columns, rows);
    }
}
