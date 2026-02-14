using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.AnimationEditorExtension;

public sealed class AnimationEditorExtension : IXveExtension
{
    private const string AnimationViewId = "animationEditor.panel";
    private const string ToggleAnimationCommandId = "animationEditor.toggleView";

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        context.Subscriptions.Add(context.Commands.Register(
            ToggleAnimationCommandId,
            _ => context.ViewHost.ToggleAsync(AnimationViewId, CancellationToken.None)));

        context.Subscriptions.Add(context.Contributions.RegisterViews(
            context.ExtensionId,
            new[]
            {
                new ExtensionViewContribution(
                    AnimationViewId,
                    "Animation",
                    ExtensionViewType.Custom,
                    ExtensionViewLocation.Bottom,
                    40,
                    ActivateByDefault: true)
            }));

        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionMenuContribution(
                    ToggleAnimationCommandId,
                    "Animation",
                    ExtensionMenuLocations.View,
                    "views.bottom",
                    60)
            }));

        object? viewModel = context.AnimationEditor.ViewModel;
        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            AnimationViewId,
            new AnimationEditorViewProvider(viewModel)));

        return Task.CompletedTask;
    }

    private sealed class AnimationEditorViewProvider : ICustomViewProvider
    {
        private readonly object? _viewModel;

        public AnimationEditorViewProvider(object? viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }
}
