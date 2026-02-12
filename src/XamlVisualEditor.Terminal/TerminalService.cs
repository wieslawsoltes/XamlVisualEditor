using Microsoft.Extensions.Logging;

namespace XamlVisualEditor.Terminal;

public interface ITerminalService
{
    ITerminalSession CreateSession(TerminalSessionOptions options);
}

public sealed class TerminalService : ITerminalService
{
    private readonly IPtyProvider _ptyProvider;
    private readonly ITerminalEmulatorFactory _emulatorFactory;
    private readonly ILoggerFactory? _loggerFactory;

    public TerminalService(
        IPtyProvider ptyProvider,
        ITerminalEmulatorFactory emulatorFactory,
        ILoggerFactory? loggerFactory = null)
    {
        _ptyProvider = ptyProvider;
        _emulatorFactory = emulatorFactory;
        _loggerFactory = loggerFactory;
    }

    public ITerminalSession CreateSession(TerminalSessionOptions options)
    {
        return new TerminalSession(
            options,
            _ptyProvider,
            _emulatorFactory,
            _loggerFactory?.CreateLogger<TerminalSession>());
    }
}
