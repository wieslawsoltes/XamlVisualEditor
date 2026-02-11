using System;
using System.Linq;
using Avalonia.Threading;
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

    public async Task<TerminalInfo> CreateAsync(TerminalCreateRequest request, CancellationToken ct)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return CreateTerminal(request);
        }

        return await Dispatcher.UIThread.InvokeAsync(
            () => CreateTerminal(request),
            DispatcherPriority.Background,
            ct);
    }

    public async Task SendTextAsync(Guid terminalId, string text, CancellationToken ct)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            SendTextCore(terminalId, text);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(
            () => SendTextCore(terminalId, text),
            DispatcherPriority.Background,
            ct);
    }

    private TerminalInfo CreateTerminal(TerminalCreateRequest request)
    {
        TerminalSessionOptions options = new()
        {
            WorkingDirectory = request.WorkingDirectory,
            Command = request.ShellPath,
            Arguments = request.Arguments ?? Array.Empty<string>()
        };

        TerminalViewModel terminal = _mainViewModel.CreateTerminalSession(options);
        return new TerminalInfo(terminal.Id, terminal.Title);
    }

    private void SendTextCore(Guid terminalId, string text)
    {
        TerminalViewModel? terminal = _mainViewModel.Terminals
            .FirstOrDefault(vm => vm.Id == terminalId);
        if (terminal is null)
        {
            return;
        }

        terminal.SendText(text);
    }
}
