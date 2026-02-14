using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.CollaborationExtension;

public sealed class CollaborationExtension : IXveExtension
{
    private const string CollaborationViewId = "collaboration.panel";
    private const string ToggleCollaborationCommandId = "collaboration.toggleView";

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        context.Subscriptions.Add(context.Commands.Register(
            ToggleCollaborationCommandId,
            _ => context.ViewHost.ToggleAsync(CollaborationViewId, CancellationToken.None)));

        context.Subscriptions.Add(context.Contributions.RegisterViews(
            context.ExtensionId,
            new[]
            {
                new ExtensionViewContribution(
                    CollaborationViewId,
                    "Collaboration",
                    ExtensionViewType.Custom,
                    ExtensionViewLocation.Bottom,
                    50,
                    ActivateByDefault: false)
            }));

        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionMenuContribution(
                    ToggleCollaborationCommandId,
                    "Collaboration",
                    ExtensionMenuLocations.View,
                    "views.bottom",
                    65)
            }));

        object? viewModel = context.CollaborationPanel.ViewModel;
        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            CollaborationViewId,
            new CollaborationPanelViewProvider(viewModel)));

        return Task.CompletedTask;
    }

    private sealed class CollaborationPanelViewProvider : ICustomViewProvider
    {
        private readonly object? _viewModel;

        public CollaborationPanelViewProvider(object? viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }
}
