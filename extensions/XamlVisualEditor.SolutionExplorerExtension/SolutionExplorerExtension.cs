using XamlVisualEditor.Extensions;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.SolutionExplorerExtension;

public sealed class SolutionExplorerExtension : IXveExtension
{
    private const string ViewId = "solutionExplorer.panel";
    private readonly MainWindowViewModel _mainViewModel;

    public SolutionExplorerExtension(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        ExtensionViewContribution view = new(
            ViewId,
            "Solution Explorer",
            ExtensionViewType.Custom,
            ExtensionViewLocation.Left,
            5,
            ActivateByDefault: true);

        context.Subscriptions.Add(context.Contributions.RegisterViews(context.ExtensionId, new[] { view }));
        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            ViewId,
            new SolutionExplorerViewProvider(_mainViewModel.SolutionExplorer)));

        return Task.CompletedTask;
    }

    private sealed class SolutionExplorerViewProvider : ICustomViewProvider
    {
        private readonly SolutionExplorerViewModel _viewModel;

        public SolutionExplorerViewProvider(SolutionExplorerViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }
}
