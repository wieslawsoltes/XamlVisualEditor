using System;
using System.Linq;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Terminal;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Adapts terminal operations for the IDE bridge.</summary>
public sealed class TerminalBridgeAdapter : ITerminalBridge
{
    private readonly MainWindowViewModel _mainViewModel;

    public TerminalBridgeAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public Task<TerminalInfo> CreateAsync(TerminalCreateRequest request, CancellationToken ct)
    {
        TerminalSessionOptions options = new()
        {
            WorkingDirectory = request.WorkingDirectory,
            Command = request.ShellPath,
            Arguments = request.Arguments ?? Array.Empty<string>()
        };

        TerminalViewModel terminal = _mainViewModel.CreateTerminalSession(options);
        return Task.FromResult(new TerminalInfo(terminal.Id, terminal.Title));
    }

    public Task SendTextAsync(Guid terminalId, string text, CancellationToken ct)
    {
        TerminalViewModel? terminal = _mainViewModel.Terminals
            .FirstOrDefault(vm => vm.Id == terminalId);
        if (terminal is null)
        {
            return Task.CompletedTask;
        }

        terminal.SendText(text);
        return Task.CompletedTask;
    }
}
