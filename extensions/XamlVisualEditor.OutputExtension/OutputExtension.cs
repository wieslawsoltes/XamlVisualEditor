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
    private const string ShowProblemsCommandId = "problems.show";
    private const string NextProblemCommandId = "problems.next";
    private const string PreviousProblemCommandId = "problems.previous";

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
        context.Subscriptions.Add(context.Commands.Register(
            ShowProblemsCommandId,
            _ => context.ViewHost.ShowAsync(ProblemsViewId, CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            NextProblemCommandId,
            _ => NavigateProblemAsync(context, problemsViewModel, 1, CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            PreviousProblemCommandId,
            _ => NavigateProblemAsync(context, problemsViewModel, -1, CancellationToken.None)));

        context.Subscriptions.Add(context.CommandMetadata.Register(
            ShowProblemsCommandId,
            new CommandMetadata(
                "Problems: Show Problems",
                "View",
                When: "hasWorkspace",
                Keybinding: "Ctrl+Shift+M",
                Priority: 36)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            NextProblemCommandId,
            new CommandMetadata(
                "Problems: Next Problem",
                "Navigation",
                When: "hasWorkspace",
                Keybinding: "F8",
                Priority: 37)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            PreviousProblemCommandId,
            new CommandMetadata(
                "Problems: Previous Problem",
                "Navigation",
                When: "hasWorkspace",
                Keybinding: "Shift+F8",
                Priority: 38)));

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
        context.Subscriptions.Add(context.Contributions.RegisterCommandPaletteItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionCommandPaletteContribution(ShowProblemsCommandId, "Problems: Show Problems", "View"),
                new ExtensionCommandPaletteContribution(NextProblemCommandId, "Problems: Next Problem", "Navigation"),
                new ExtensionCommandPaletteContribution(PreviousProblemCommandId, "Problems: Previous Problem", "Navigation")
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

    private static async Task NavigateProblemAsync(
        ExtensionContext context,
        ProblemsPanelViewModel problemsViewModel,
        int delta,
        CancellationToken cancellationToken)
    {
        await context.ViewHost.ShowAsync(ProblemsViewId, cancellationToken).ConfigureAwait(false);
        bool navigated = await problemsViewModel.NavigateToRelativeAsync(delta, cancellationToken).ConfigureAwait(false);
        if (!navigated)
        {
            await context.Window.ShowInformationMessageAsync("No diagnostics available.", cancellationToken).ConfigureAwait(false);
        }
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
