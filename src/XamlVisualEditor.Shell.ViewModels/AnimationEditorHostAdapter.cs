using System;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Disposables;
using System.Reactive.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Shell-backed animation editor host adapter.</summary>
public sealed class AnimationEditorHostAdapter : IAnimationEditorHost
{
    private readonly MainWindowViewModel _mainViewModel;
    private int _transactionDepth;

    public AnimationEditorHostAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public IAnimationEditorPanelModel? PanelModel => _mainViewModel.AnimationEditor;

    public IDisposable BeginTransaction(string name)
    {
        Interlocked.Increment(ref _transactionDepth);
        return Disposable.Create(EndTransaction);
    }

    public async Task RefreshPreviewAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _mainViewModel.AnimationEditor.RefreshResourcesCommand
            .Execute()
            .ToTask(cancellationToken)
            .ConfigureAwait(false);
        await _mainViewModel.AnimationEditor.StopPreviewCommand
            .Execute()
            .ToTask(cancellationToken)
            .ConfigureAwait(false);
        await _mainViewModel.AnimationEditor.PlayPreviewCommand
            .Execute()
            .ToTask(cancellationToken)
            .ConfigureAwait(false);
    }

    private void EndTransaction()
    {
        int depth = Interlocked.Decrement(ref _transactionDepth);
        if (depth != 0)
        {
            return;
        }

        _ = RefreshPreviewAsync(CancellationToken.None);
    }
}
