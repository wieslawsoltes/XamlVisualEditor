using System;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.OutputExtension;

public sealed class OutputExtension : IXveExtension
{
    private const string ViewId = "output.panel";
    private const string ProblemsViewId = "problems.panel";
    private const string ToggleOutputCommandId = "output.toggleView";
    private const string ToggleProblemsCommandId = "problems.toggleView";

    public async Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        OutputPanelViewModel viewModel = new(context.Window);
        ProblemsPanelViewModel problemsViewModel = new(context.Diagnostics, context.Editor);

        context.Subscriptions.Add(context.Commands.Register(
            ToggleOutputCommandId,
            _ => context.ViewHost.ToggleAsync(ViewId, CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            ToggleProblemsCommandId,
            _ => context.ViewHost.ToggleAsync(ProblemsViewId, CancellationToken.None)));

        context.Subscriptions.Add(context.Contributions.RegisterViews(
            context.ExtensionId,
            new[]
            {
                new ExtensionViewContribution(
                    ViewId,
                    "Output",
                    ExtensionViewType.Custom,
                    ExtensionViewLocation.Bottom,
                    10,
                    ActivateByDefault: true),
                new ExtensionViewContribution(
                    ProblemsViewId,
                    "Problems",
                    ExtensionViewType.Custom,
                    ExtensionViewLocation.Bottom,
                    20,
                    ActivateByDefault: true)
            }));

        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionMenuContribution(
                    ToggleOutputCommandId,
                    "Output",
                    ExtensionMenuLocations.View,
                    "views.bottom",
                    30),
                new ExtensionMenuContribution(
                    ToggleProblemsCommandId,
                    "Problems",
                    ExtensionMenuLocations.View,
                    "views.bottom",
                    35)
            }));

        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            ViewId,
            new OutputPanelViewProvider(viewModel)));
        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            ProblemsViewId,
            new ProblemsPanelViewProvider(problemsViewModel)));

        EventHandler<OutputChannelEventArgs> createdHandler = (_, args) =>
            viewModel.HandleChannelCreated(args.Channel);
        EventHandler<OutputChannelEventArgs> removedHandler = (_, args) =>
            viewModel.HandleChannelRemoved(args.Channel);
        EventHandler<OutputChannelClearedEventArgs> clearedHandler = (_, args) =>
            viewModel.HandleChannelCleared(args.Channel);
        EventHandler<OutputChannelMessageEventArgs> messageHandler = (_, args) =>
            viewModel.HandleChannelMessage(args.Channel, args.Message, args.IsLine);

        context.Window.OutputChannelCreated += createdHandler;
        context.Window.OutputChannelRemoved += removedHandler;
        context.Window.OutputChannelCleared += clearedHandler;
        context.Window.OutputChannelMessage += messageHandler;

        context.Subscriptions.Add(Disposable.Create(() => context.Window.OutputChannelCreated -= createdHandler));
        context.Subscriptions.Add(Disposable.Create(() => context.Window.OutputChannelRemoved -= removedHandler));
        context.Subscriptions.Add(Disposable.Create(() => context.Window.OutputChannelCleared -= clearedHandler));
        context.Subscriptions.Add(Disposable.Create(() => context.Window.OutputChannelMessage -= messageHandler));
        context.Subscriptions.Add(Disposable.Create(viewModel.Dispose));

        await viewModel.InitializeAsync(cancellationToken);

        EventHandler<DiagnosticsChannelsChangedEventArgs> channelsHandler = (_, args) =>
            problemsViewModel.HandleChannelsChanged(args.Channels);
        EventHandler<DiagnosticsChannelPublishedEventArgs> diagnosticsHandler = (_, args) =>
            problemsViewModel.HandleDiagnosticsPublished(args.ChannelId, args.Diagnostics);
        EventHandler<DiagnosticsSnapshotPublishedEventArgs> snapshotHandler = (_, args) =>
            problemsViewModel.HandleSnapshotsPublished(args.Snapshots);

        context.Diagnostics.ChannelsChanged += channelsHandler;
        context.Diagnostics.DiagnosticsChannelPublished += diagnosticsHandler;
        context.Diagnostics.DiagnosticsSnapshotPublished += snapshotHandler;

        context.Subscriptions.Add(Disposable.Create(() => context.Diagnostics.ChannelsChanged -= channelsHandler));
        context.Subscriptions.Add(Disposable.Create(() => context.Diagnostics.DiagnosticsChannelPublished -= diagnosticsHandler));
        context.Subscriptions.Add(Disposable.Create(() => context.Diagnostics.DiagnosticsSnapshotPublished -= snapshotHandler));
        context.Subscriptions.Add(Disposable.Create(problemsViewModel.Dispose));

        await problemsViewModel.InitializeAsync(cancellationToken);
    }

    private sealed class OutputPanelViewProvider : ICustomViewProvider
    {
        private readonly OutputPanelViewModel _viewModel;

        public OutputPanelViewProvider(OutputPanelViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }

    private sealed class ProblemsPanelViewProvider : ICustomViewProvider
    {
        private readonly ProblemsPanelViewModel _viewModel;

        public ProblemsPanelViewProvider(ProblemsPanelViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }
}
