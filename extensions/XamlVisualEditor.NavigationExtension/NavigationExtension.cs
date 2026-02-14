using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.NavigationExtension;

public sealed class NavigationExtension : IXveExtension
{
    private const string ReferencesViewId = "references.panel";
    private const string FindReferencesCommandId = "navigation.findReferences";
    private const string GoToDefinitionCommandId = "navigation.goToDefinition";
    private const string NavigateBackCommandId = "navigation.history.back";
    private const string NavigateForwardCommandId = "navigation.history.forward";
    private const string ToggleReferencesCommandId = "navigation.references.toggleView";

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        ReferencesPanelViewModel viewModel = new(context.Navigation, context.Editor, context.Window);

        context.Subscriptions.Add(context.Commands.Register(
            FindReferencesCommandId,
            _ => viewModel.FindReferencesAsync(CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            GoToDefinitionCommandId,
            _ => viewModel.GoToDefinitionAsync(CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            NavigateBackCommandId,
            _ => context.NavigationHistory.NavigateBackAsync(CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            NavigateForwardCommandId,
            _ => context.NavigationHistory.NavigateForwardAsync(CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            ToggleReferencesCommandId,
            _ => context.ViewHost.ToggleAsync(ReferencesViewId, CancellationToken.None)));

        context.Subscriptions.Add(context.CommandMetadata.Register(
            FindReferencesCommandId,
            new CommandMetadata(
                Title: "Navigation: Find References",
                Category: "Navigation",
                When: "hasTextDocument",
                Keybinding: "Shift+F12",
                Priority: 50)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            GoToDefinitionCommandId,
            new CommandMetadata(
                Title: "Navigation: Go To Definition",
                Category: "Navigation",
                When: "hasTextDocument",
                Keybinding: "F12",
                Priority: 40)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            NavigateBackCommandId,
            new CommandMetadata(
                Title: "Navigation: Back",
                Category: "Navigation",
                When: "canNavigateBack",
                Keybinding: "Alt+Left",
                Priority: 10)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            NavigateForwardCommandId,
            new CommandMetadata(
                Title: "Navigation: Forward",
                Category: "Navigation",
                When: "canNavigateForward",
                Keybinding: "Alt+Right",
                Priority: 20)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            ToggleReferencesCommandId,
            new CommandMetadata(
                Title: "View: Toggle References",
                Category: "View",
                Priority: 60)));

        context.Subscriptions.Add(context.Contributions.RegisterCommandPaletteItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionCommandPaletteContribution(NavigateBackCommandId, "Navigate Back", "Navigation"),
                new ExtensionCommandPaletteContribution(NavigateForwardCommandId, "Navigate Forward", "Navigation"),
                new ExtensionCommandPaletteContribution(GoToDefinitionCommandId, "Go To Definition", "Navigation"),
                new ExtensionCommandPaletteContribution(FindReferencesCommandId, "Find References", "Navigation")
            }));

        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionMenuContribution(
                    NavigateBackCommandId,
                    "Navigate Back",
                    ExtensionMenuLocations.Edit,
                    "navigation",
                    60),
                new ExtensionMenuContribution(
                    NavigateForwardCommandId,
                    "Navigate Forward",
                    ExtensionMenuLocations.Edit,
                    "navigation",
                    65),
                new ExtensionMenuContribution(
                    GoToDefinitionCommandId,
                    "Go To Definition",
                    ExtensionMenuLocations.Edit,
                    "navigation",
                    70),
                new ExtensionMenuContribution(
                    FindReferencesCommandId,
                    "Find References",
                    ExtensionMenuLocations.Edit,
                    "navigation",
                    75),
                new ExtensionMenuContribution(
                    ToggleReferencesCommandId,
                    "References",
                    ExtensionMenuLocations.View,
                    "views.bottom",
                    40)
            }));

        context.Subscriptions.Add(context.Contributions.RegisterViews(
            context.ExtensionId,
            new[]
            {
                new ExtensionViewContribution(
                    ReferencesViewId,
                    "References",
                    ExtensionViewType.Custom,
                    ExtensionViewLocation.Bottom,
                    30,
                    ActivateByDefault: true)
            }));

        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            ReferencesViewId,
            new ReferencesPanelViewProvider(viewModel)));

        return Task.CompletedTask;
    }

    private sealed class ReferencesPanelViewProvider : ICustomViewProvider
    {
        private readonly ReferencesPanelViewModel _viewModel;

        public ReferencesPanelViewProvider(ReferencesPanelViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }
}
