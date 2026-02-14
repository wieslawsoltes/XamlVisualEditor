using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Shell-backed debug settings host adapter.</summary>
public sealed class DebugSettingsHostAdapter : IDebugSettingsHost
{
    private readonly MainWindowViewModel _mainViewModel;

    public DebugSettingsHostAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public object? ViewModel => _mainViewModel.DebugSettings;
}
