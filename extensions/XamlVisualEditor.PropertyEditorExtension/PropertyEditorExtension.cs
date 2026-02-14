using System;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.PropertyEditorExtension;

public sealed class PropertyEditorExtension : IXveExtension
{
    private const string ViewId = "propertyEditor.panel";
    private const string ToggleViewCommandId = "propertyEditor.toggleView";

    public async Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        PropertyEditorPanelViewModel viewModel = new(context.Designer, context.PropertyEditors);

        context.Subscriptions.Add(context.Commands.Register(ToggleViewCommandId, _ =>
            context.ViewHost.ToggleAsync(ViewId, CancellationToken.None)));

        context.Subscriptions.Add(context.Contributions.RegisterViews(
            context.ExtensionId,
            new[]
            {
                new ExtensionViewContribution(
                    ViewId,
                    "Properties",
                    ExtensionViewType.Custom,
                    ExtensionViewLocation.Right,
                    20,
                    ActivateByDefault: true)
            }));

        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionMenuContribution(
                    ToggleViewCommandId,
                    "Properties",
                    ExtensionMenuLocations.View,
                    "views.right",
                    10)
            }));

        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            ViewId,
            new PropertyEditorPanelViewProvider(viewModel)));

        EventHandler<DesignerSelectionChangedEventArgs> selectionHandler = (_, e) =>
        {
            RunBackground(viewModel.HandleSelectionChangedAsync(e.SelectedNodes, CancellationToken.None));
        };

        EventHandler<DesignerDocumentChangedEventArgs> documentHandler = (_, e) =>
        {
            RunBackground(viewModel.HandleDocumentChangedAsync(e.DocumentPath, CancellationToken.None));
        };

        context.Designer.SelectionChanged += selectionHandler;
        context.Designer.ActiveDocumentChanged += documentHandler;
        context.Subscriptions.Add(Disposable.Create(() =>
        {
            context.Designer.SelectionChanged -= selectionHandler;
            context.Designer.ActiveDocumentChanged -= documentHandler;
        }));
        context.Subscriptions.Add(Disposable.Create(viewModel.Dispose));

        await viewModel.InitializeAsync(cancellationToken);
    }

    private static void RunBackground(Task task)
    {
        _ = task.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private sealed class PropertyEditorPanelViewProvider : ICustomViewProvider
    {
        private readonly PropertyEditorPanelViewModel _viewModel;

        public PropertyEditorPanelViewProvider(PropertyEditorPanelViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }
}
