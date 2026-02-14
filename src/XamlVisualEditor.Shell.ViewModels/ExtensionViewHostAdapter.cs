using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

public sealed class ExtensionViewHostAdapter : IExtensionViewHost
{
    private readonly MainWindowViewModel _mainViewModel;

    public ExtensionViewHostAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public Task ShowAsync(string viewId, CancellationToken cancellationToken)
    {
        _mainViewModel.ShowExtensionView(viewId);
        return Task.CompletedTask;
    }

    public Task ToggleAsync(string viewId, CancellationToken cancellationToken)
    {
        _mainViewModel.ToggleExtensionView(viewId);
        return Task.CompletedTask;
    }

    public Task<bool> IsVisibleAsync(string viewId, CancellationToken cancellationToken)
    {
        bool visible = _mainViewModel.IsExtensionViewVisible(viewId);
        return Task.FromResult(visible);
    }

    public Task ActivateAsync(string viewId, CancellationToken cancellationToken)
    {
        _mainViewModel.ActivateExtensionView(viewId);
        return Task.CompletedTask;
    }
}
