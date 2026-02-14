using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.LspSettingsExtension;

public sealed class LspSettingsExtension : IXveExtension
{
    private const string LspSettingsViewId = "lspSettings.panel";
    private const string ToggleLspSettingsCommandId = "lspSettings.toggleView";

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        context.Subscriptions.Add(context.Commands.Register(
            ToggleLspSettingsCommandId,
            _ => context.ViewHost.ToggleAsync(LspSettingsViewId, CancellationToken.None)));

        context.Subscriptions.Add(context.Contributions.RegisterViews(
            context.ExtensionId,
            new[]
            {
                new ExtensionViewContribution(
                    LspSettingsViewId,
                    "LSP Settings",
                    ExtensionViewType.Custom,
                    ExtensionViewLocation.Bottom,
                    70,
                    ActivateByDefault: true)
            }));

        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionMenuContribution(
                    ToggleLspSettingsCommandId,
                    "LSP Settings",
                    ExtensionMenuLocations.View,
                    "views.bottom",
                    90)
            }));

        object? viewModel = context.LspSettings.ViewModel;
        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            LspSettingsViewId,
            new LspSettingsViewProvider(viewModel)));

        return Task.CompletedTask;
    }

    private sealed class LspSettingsViewProvider : ICustomViewProvider
    {
        private readonly object? _viewModel;

        public LspSettingsViewProvider(object? viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }
}
