using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.SolutionExplorerExtension;

public sealed class SolutionExplorerExtension : IXveExtension
{
    private const string ViewId = "solutionExplorer.panel";
    private const string ToggleViewCommandId = "solutionExplorer.toggleView";
    private readonly ISolutionExplorerPanelHost _solutionExplorerHost;

    public SolutionExplorerExtension(ISolutionExplorerPanelHost solutionExplorerHost)
    {
        _solutionExplorerHost = solutionExplorerHost;
    }

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        context.Subscriptions.Add(context.Commands.Register(ToggleViewCommandId, _ =>
            context.ViewHost.ToggleAsync(ViewId, CancellationToken.None)));

        ExtensionViewContribution view = new(
            ViewId,
            "Solution Explorer",
            ExtensionViewType.Custom,
            ExtensionViewLocation.Left,
            5,
            ActivateByDefault: true);

        context.Subscriptions.Add(context.Contributions.RegisterViews(context.ExtensionId, new[] { view }));
        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionMenuContribution(
                    ToggleViewCommandId,
                    "Solution Explorer",
                    ExtensionMenuLocations.View,
                    "views.left",
                    0)
            }));
        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            ViewId,
            new SolutionExplorerViewProvider(_solutionExplorerHost.ViewModel)));

        return Task.CompletedTask;
    }

    private sealed class SolutionExplorerViewProvider : ICustomViewProvider
    {
        private readonly object? _viewModel;

        public SolutionExplorerViewProvider(object? viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }
}
