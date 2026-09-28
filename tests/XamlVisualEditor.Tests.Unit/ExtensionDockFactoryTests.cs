using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Shell;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class ExtensionDockFactoryTests
{
    [Fact]
    public void CollapseBottomToolDock_ClosesPanelWithoutRemovingTools()
    {
        using MainWindowViewModel viewModel = new();
        IRootDock layout = viewModel.DockFactory.CreateDefaultLayout();
        viewModel.DockFactory.InitLayout(layout);
        ToolDock? bottomDock = XamlEditorDockFactory.FindDockable<ToolDock>(
            layout,
            "BottomToolDock");
        Assert.NotNull(bottomDock);
        Assert.NotEmpty(bottomDock!.VisibleDockables!);
        bottomDock.ActiveDockable = bottomDock.VisibleDockables![0];
        bottomDock.IsExpanded = true;

        viewModel.DockFactory.CollapseBottomToolDock(layout);

        Assert.Null(bottomDock.ActiveDockable);
        Assert.False(bottomDock.IsExpanded);
        Assert.Equal(0, bottomDock.Proportion);
        Assert.Equal(0.25, bottomDock.CollapsedProportion);
        Assert.Empty(bottomDock.VisibleDockables!);
        Assert.NotEmpty(layout.BottomPinnedDockables!);
    }

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
