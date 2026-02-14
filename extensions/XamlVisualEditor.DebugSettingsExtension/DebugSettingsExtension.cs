using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.DebugSettingsExtension;

public sealed class DebugSettingsExtension : IXveExtension
{
    private const string DebugSettingsViewId = "debugSettings.panel";
    private const string ToggleDebugSettingsCommandId = "debugSettings.toggleView";

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        context.Subscriptions.Add(context.Commands.Register(
            ToggleDebugSettingsCommandId,
            _ => context.ViewHost.ToggleAsync(DebugSettingsViewId, CancellationToken.None)));

        context.Subscriptions.Add(context.Contributions.RegisterViews(
            context.ExtensionId,
            new[]
            {
                new ExtensionViewContribution(
                    DebugSettingsViewId,
                    "Debug Settings",
                    ExtensionViewType.Custom,
                    ExtensionViewLocation.Bottom,
                    60,
                    ActivateByDefault: true)
            }));

        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionMenuContribution(
                    ToggleDebugSettingsCommandId,
                    "Debug Settings",
                    ExtensionMenuLocations.View,
                    "views.bottom",
                    80)
            }));

        object? viewModel = context.DebugSettings.ViewModel;
        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            DebugSettingsViewId,
            new DebugSettingsViewProvider(viewModel)));

        return Task.CompletedTask;
    }

    private sealed class DebugSettingsViewProvider : ICustomViewProvider
    {
        private readonly object? _viewModel;

        public DebugSettingsViewProvider(object? viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }
}
