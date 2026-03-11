using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.AnimationEditorExtension;

public sealed class AnimationEditorExtension : IXveExtension
{
    private const string AnimationViewId = "animationEditor.panel";
    private const string ToggleAnimationCommandId = "animationEditor.toggleView";
    private const string RefreshPreviewCommandId = "animationEditor.refreshPreview";

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        context.Subscriptions.Add(context.Commands.Register(
            ToggleAnimationCommandId,
            _ => context.ViewHost.ToggleAsync(AnimationViewId, CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            RefreshPreviewCommandId,
            _ => RefreshPreviewAsync(context, CancellationToken.None)));

        context.Subscriptions.Add(context.CommandMetadata.Register(
            ToggleAnimationCommandId,
            new CommandMetadata(
                Title: "View: Toggle Animation Editor",
                Category: "View",
                Priority: 95)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            RefreshPreviewCommandId,
            new CommandMetadata(
                Title: "Animation: Refresh Preview",
                Category: "Animation",
                Priority: 30)));

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

        context.Subscriptions.Add(context.Contributions.RegisterCommandPaletteItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionCommandPaletteContribution(
                    ToggleAnimationCommandId,
                    "Toggle Animation Editor",
                    "View"),
                new ExtensionCommandPaletteContribution(
                    RefreshPreviewCommandId,
                    "Animation: Refresh Preview",
                    "Animation")
            }));

        IAnimationEditorPanelModel? panelModel = context.AnimationEditor.PanelModel;
        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            AnimationViewId,
            new AnimationEditorViewProvider(panelModel)));

        return Task.CompletedTask;
    }

    private static async Task RefreshPreviewAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        using IDisposable transaction = context.AnimationEditor.BeginTransaction("Animation preview refresh");
        await context.AnimationEditor.RefreshPreviewAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class AnimationEditorViewProvider : ICustomViewProvider
    {
        private readonly IAnimationEditorPanelModel? _panelModel;

        public AnimationEditorViewProvider(IAnimationEditorPanelModel? panelModel)
        {
            _panelModel = panelModel;
        }

        public object? CreateViewModel()
        {
            return _panelModel;
        }
    }
}
