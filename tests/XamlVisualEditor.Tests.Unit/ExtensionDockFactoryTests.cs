using Dock.Model.Core;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Shell;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class ExtensionDockFactoryTests
{
    [Fact]
    public void AddExtensionTool_WiresOwnerAndFactory_WhenInsertedIntoLeftDock()
    {
        using MainWindowViewModel viewModel = new();
        ExtensionViewContribution contribution = new(
            "test.left.panel",
            "Test Left Panel",
            ExtensionViewType.Webview,
            ExtensionViewLocation.Left,
            10);
        ExtensionWebviewViewModel extensionView = new(contribution, "Placeholder");

        ExtensionTool? tool = viewModel.DockFactory.AddExtensionTool(viewModel.DockLayout, extensionView);

        ExtensionTool actual = Assert.IsType<ExtensionTool>(tool);
        IDock owner = Assert.IsAssignableFrom<IDock>(actual.Owner);
        Assert.Same(viewModel.DockFactory, owner.Factory);
        Assert.NotNull(actual.DockCapabilityOverrides);
        Assert.NotNull(owner.DockCapabilityPolicy);
    }
}
