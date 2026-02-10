using Microsoft.Extensions.Logging;

namespace XamlVisualEditor.Terminal;

public interface ITerminalService
{
    ITerminalSession CreateSession(TerminalSessionOptions options);
}

public sealed class TerminalService : ITerminalService
{
    private readonly IPtyProvider _ptyProvider;
    private readonly ILoggerFactory? _loggerFactory;

    public TerminalService(IPtyProvider ptyProvider, ILoggerFactory? loggerFactory = null)
    {
        _ptyProvider = ptyProvider;
        _loggerFactory = loggerFactory;
    }

    public ITerminalSession CreateSession(TerminalSessionOptions options)
    {
        return new TerminalSession(
            options,
            _ptyProvider,
            _loggerFactory?.CreateLogger<TerminalSession>());
    }
}
