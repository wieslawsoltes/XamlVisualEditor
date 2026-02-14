using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.ToolboxExtension;

public sealed class ToolboxExtension : IXveExtension
{
    private const string InsertButtonCommandId = "toolbox.insertButton";
    private const string InsertSelectedCommandId = "toolbox.insertSelected";
    private const string ToolboxViewId = "toolbox.panel";
    private const string ToggleViewCommandId = "toolbox.toggleView";

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        ToolboxPanelViewModel viewModel = new(context.Commands, context.Settings);

        context.Subscriptions.Add(context.Commands.Register(InsertButtonCommandId, _ => InsertButtonAsync(context, cancellationToken)));
        context.Subscriptions.Add(context.Commands.Register(InsertSelectedCommandId, commandContext =>
            InsertSelectedAsync(context, commandContext, cancellationToken)));
        context.Subscriptions.Add(context.Commands.Register(ToggleViewCommandId, _ =>
            context.ViewHost.ToggleAsync(ToolboxViewId, CancellationToken.None)));

        context.Subscriptions.Add(context.Contributions.RegisterViews(
            context.ExtensionId,
            new[]
            {
                new ExtensionViewContribution(
                    ToolboxViewId,
                    "Toolbox",
                    ExtensionViewType.Custom,
                    ExtensionViewLocation.Left,
                    12,
                    ActivateByDefault: true)
            }));

        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            ToolboxViewId,
            new ToolboxPanelViewProvider(viewModel)));

        context.Subscriptions.Add(context.CommandMetadata.Register(
            InsertButtonCommandId,
            new CommandMetadata(
                Title: "Toolbox: Insert Button",
                Category: "Toolbox",
                Priority: 50)));

        context.Subscriptions.Add(context.CommandMetadata.Register(
            InsertSelectedCommandId,
            new CommandMetadata(
                Title: "Toolbox: Insert Selected",
                Category: "Toolbox",
                Priority: 60)));

        context.Subscriptions.Add(context.Contributions.RegisterCommandPaletteItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionCommandPaletteContribution(InsertButtonCommandId, "Toolbox: Insert Button", "Toolbox"),
                new ExtensionCommandPaletteContribution(InsertSelectedCommandId, "Toolbox: Insert Selected", "Toolbox")
            }));

        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionMenuContribution(
                    InsertButtonCommandId,
                    "Insert Button",
                    ExtensionMenuLocations.Tools,
                    "toolbox",
                    10),
                new ExtensionMenuContribution(
                    InsertSelectedCommandId,
                    "Insert Selected Toolbox Item",
                    ExtensionMenuLocations.Tools,
                    "toolbox",
                    20),
                new ExtensionMenuContribution(
                    ToggleViewCommandId,
                    "Toolbox",
                    ExtensionMenuLocations.View,
                    "views.left",
                    20)
            }));

        return Task.CompletedTask;
    }

    private static async Task InsertButtonAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        string? insertedNodeId = await context.Designer.InsertElementAsync(
            "Button",
            "https://github.com/avaloniaui",
            parentNodeId: null,
            cancellationToken);

        if (insertedNodeId is null)
        {
            await context.Window.ShowWarningMessageAsync(
                "No active designer document. Open a XAML designer document first.",
                cancellationToken);
        }
    }

    private static async Task InsertSelectedAsync(
        ExtensionContext context,
        CommandContext commandContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetStringArg(commandContext.Arguments, 0, out string? typeName)
            || !TryGetStringArg(commandContext.Arguments, 1, out string? xmlNamespace))
        {
            await context.Window.ShowWarningMessageAsync(
                "toolbox.insertSelected expects arguments: [typeName, xmlNamespace, optional parentNodeId].",
                cancellationToken);
            return;
        }

        string? parentNodeId = null;
        _ = TryGetStringArg(commandContext.Arguments, 2, out parentNodeId);

        string? insertedNodeId = await context.Designer.InsertElementAsync(
            typeName!,
            xmlNamespace!,
            parentNodeId,
            cancellationToken);

        if (insertedNodeId is null)
        {
            await context.Window.ShowWarningMessageAsync(
                "Insert failed. Ensure an active designer document is open and arguments are valid.",
                cancellationToken);
        }
    }

    private static bool TryGetStringArg(IReadOnlyList<object?>? arguments, int index, out string? value)
    {
        value = null;
        if (arguments is null || index < 0 || index >= arguments.Count)
        {
            return false;
        }

        if (arguments[index] is not string text || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text;
        return true;
    }

    private sealed class ToolboxPanelViewProvider : ICustomViewProvider
    {
        private readonly ToolboxPanelViewModel _viewModel;

        public ToolboxPanelViewProvider(ToolboxPanelViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }
}
