using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.TreeInspectorExtension;

public sealed class TreeInspectorExtension : IXveExtension
{
    private const string VisualRevealCommandId = "tree.visual.revealSelection";
    private const string LogicalRevealCommandId = "tree.logical.revealSelection";
    private const string VisualRefreshCommandId = "tree.visual.refresh";
    private const string LogicalRefreshCommandId = "tree.logical.refresh";
    private const string VisualViewId = "visualTree.panel";
    private const string LogicalViewId = "logicalTree.panel";
    private const string VisualToggleCommandId = "tree.visual.toggleView";
    private const string LogicalToggleCommandId = "tree.logical.toggleView";

    public async Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        TreeInspectorPanelViewModel visualViewModel = new(context.Designer, TreeKind.Visual);
        TreeInspectorPanelViewModel logicalViewModel = new(context.Designer, TreeKind.Logical);

        context.Subscriptions.Add(context.Contributions.RegisterViews(
            context.ExtensionId,
            new[]
            {
                new ExtensionViewContribution(
                    VisualViewId,
                    "Visual Tree",
                    ExtensionViewType.Custom,
                    ExtensionViewLocation.Right,
                    30,
                    ActivateByDefault: true),
                new ExtensionViewContribution(
                    LogicalViewId,
                    "Logical Tree",
                    ExtensionViewType.Custom,
                    ExtensionViewLocation.Right,
                    35,
                    ActivateByDefault: false)
            }));

        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            VisualViewId,
            new TreeInspectorViewProvider(visualViewModel)));
        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            LogicalViewId,
            new TreeInspectorViewProvider(logicalViewModel)));

        context.Subscriptions.Add(context.Commands.Register(
            VisualRevealCommandId,
            _ => RevealSelectionAsync(context, TreeKind.Visual, CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            LogicalRevealCommandId,
            _ => RevealSelectionAsync(context, TreeKind.Logical, CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            VisualRefreshCommandId,
            _ => RefreshTreeAsync(visualViewModel)));
        context.Subscriptions.Add(context.Commands.Register(
            LogicalRefreshCommandId,
            _ => RefreshTreeAsync(logicalViewModel)));
        context.Subscriptions.Add(context.Commands.Register(
            VisualToggleCommandId,
            _ => context.ViewHost.ToggleAsync(VisualViewId, CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            LogicalToggleCommandId,
            _ => context.ViewHost.ToggleAsync(LogicalViewId, CancellationToken.None)));

        context.Subscriptions.Add(context.CommandMetadata.Register(
            VisualRevealCommandId,
            new CommandMetadata(
                Title: "Visual Tree: Reveal Selection",
                Category: "Tree",
                Priority: 50)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            LogicalRevealCommandId,
            new CommandMetadata(
                Title: "Logical Tree: Reveal Selection",
                Category: "Tree",
                Priority: 60)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            VisualRefreshCommandId,
            new CommandMetadata(
                Title: "Visual Tree: Refresh",
                Category: "Tree",
                Priority: 70)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            LogicalRefreshCommandId,
            new CommandMetadata(
                Title: "Logical Tree: Refresh",
                Category: "Tree",
                Priority: 80)));

        context.Subscriptions.Add(context.Contributions.RegisterCommandPaletteItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionCommandPaletteContribution(VisualRevealCommandId, "Visual Tree: Reveal Selection", "Tree"),
                new ExtensionCommandPaletteContribution(LogicalRevealCommandId, "Logical Tree: Reveal Selection", "Tree"),
                new ExtensionCommandPaletteContribution(VisualRefreshCommandId, "Visual Tree: Refresh", "Tree"),
                new ExtensionCommandPaletteContribution(LogicalRefreshCommandId, "Logical Tree: Refresh", "Tree")
            }));

        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionMenuContribution(
                    VisualToggleCommandId,
                    "Visual Tree",
                    ExtensionMenuLocations.View,
                    "views.right",
                    20),
                new ExtensionMenuContribution(
                    LogicalToggleCommandId,
                    "Logical Tree",
                    ExtensionMenuLocations.View,
                    "views.right",
                    25)
            }));

        EventHandler<DesignerSelectionChangedEventArgs> selectionHandler = (_, _) =>
        {
            _ = RefreshSelectionAsync(context, visualViewModel, logicalViewModel, CancellationToken.None);
        };

        EventHandler<DesignerDocumentChangedEventArgs> documentHandler = (_, _) =>
        {
            _ = visualViewModel.RefreshAsync(CancellationToken.None);
            _ = logicalViewModel.RefreshAsync(CancellationToken.None);
        };

        context.Designer.SelectionChanged += selectionHandler;
        context.Designer.ActiveDocumentChanged += documentHandler;
        context.Subscriptions.Add(Disposable.Create(() =>
        {
            context.Designer.SelectionChanged -= selectionHandler;
            context.Designer.ActiveDocumentChanged -= documentHandler;
        }));
        context.Subscriptions.Add(Disposable.Create(visualViewModel.Dispose));
        context.Subscriptions.Add(Disposable.Create(logicalViewModel.Dispose));

        await visualViewModel.InitializeAsync(cancellationToken);
        await logicalViewModel.InitializeAsync(cancellationToken);
    }

    private static async Task RefreshSelectionAsync(
        ExtensionContext context,
        TreeInspectorPanelViewModel visualViewModel,
        TreeInspectorPanelViewModel logicalViewModel,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DesignerNodeSummary> selected = await context.Designer.GetSelectedNodesAsync(cancellationToken);
        visualViewModel.UpdateSelection(selected);
        logicalViewModel.UpdateSelection(selected);
    }

    private static Task RefreshTreeAsync(TreeInspectorPanelViewModel viewModel)
    {
        return viewModel.RefreshAsync(CancellationToken.None);
    }

    private static async Task RevealSelectionAsync(ExtensionContext context, TreeKind kind, CancellationToken cancellationToken)
    {
        IReadOnlyList<DesignerNodeSummary> selected = await context.Designer.GetSelectedNodesAsync(cancellationToken);
        if (selected.Count == 0)
        {
            await context.Window.ShowWarningMessageAsync(
                "No selected nodes to reveal in the tree.",
                cancellationToken);
            return;
        }

        string nodeId = selected[0].NodeId;
        bool revealed = await context.Designer.RevealNodeAsync(nodeId, cancellationToken);
        if (!revealed)
        {
            string treeName = kind == TreeKind.Visual ? "visual" : "logical";
            await context.Window.ShowWarningMessageAsync(
                $"Unable to reveal selection in the {treeName} tree. Open a designer document first.",
                cancellationToken);
        }
    }

    private sealed class TreeInspectorViewProvider : ICustomViewProvider
    {
        private readonly TreeInspectorPanelViewModel _viewModel;

        public TreeInspectorViewProvider(TreeInspectorPanelViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }
}
