using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Shell-backed LSP settings host adapter.</summary>
public sealed class LspSettingsHostAdapter : ILspSettingsHost
{
    private readonly MainWindowViewModel _mainViewModel;

    public LspSettingsHostAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public object? ViewModel => _mainViewModel.LspSettings;
}
