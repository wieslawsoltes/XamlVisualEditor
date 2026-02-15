using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Adapts shell command execution for extension-owned command registrations.</summary>
public sealed class ShellCommandBridgeAdapter : IShellCommandBridge
{
    private readonly MainWindowViewModel _mainViewModel;

    public ShellCommandBridgeAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public Task ExecuteAsync(ShellCommandKind command, CancellationToken cancellationToken)
    {
        return _mainViewModel.ExecuteShellCommandAsync(command, cancellationToken);
    }
}
