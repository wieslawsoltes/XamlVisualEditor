using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Shell-backed solution explorer panel host adapter.</summary>
public sealed class SolutionExplorerPanelHostAdapter : ISolutionExplorerPanelHost
{
    private readonly MainWindowViewModel _mainViewModel;

    public SolutionExplorerPanelHostAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public object? ViewModel => _mainViewModel.SolutionExplorer;
}
