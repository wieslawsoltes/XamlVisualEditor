using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Shell-backed animation editor host adapter.</summary>
public sealed class AnimationEditorHostAdapter : IAnimationEditorHost
{
    private readonly MainWindowViewModel _mainViewModel;

    public AnimationEditorHostAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public object? ViewModel => _mainViewModel.AnimationEditor;
}
