using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Shell-backed collaboration panel host adapter.</summary>
public sealed class CollaborationPanelHostAdapter : ICollaborationPanelHost
{
    private readonly MainWindowViewModel _mainViewModel;

    public CollaborationPanelHostAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public object? ViewModel => _mainViewModel.Collaboration;
}
