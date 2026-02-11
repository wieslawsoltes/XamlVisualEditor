using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Core;
using Xunit;
using XamlVisualEditor.App;
using XamlVisualEditor.Shell;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Tests.UI;

public sealed class ExtensionManagerMenuTests
{
    [AvaloniaFact]
    public async Task ExtensionsManager_Menu_Toggles_Panel()
    {
        MainWindowViewModel vm = new();
        MainWindow window = new(vm);

        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => window.ApplyTemplate());

        MenuItem? menuItem = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .FirstOrDefault(item => string.Equals(item.Header as string, "_Extensions Manager", System.StringComparison.Ordinal));

        Assert.NotNull(menuItem);
        Assert.NotNull(menuItem!.Command);

        bool initial = vm.IsExtensionsManagerVisible;
        menuItem.Command!.Execute(null);

        Assert.NotEqual(initial, vm.IsExtensionsManagerVisible);

        await Dispatcher.UIThread.InvokeAsync(() => window.Close());
    }

    [AvaloniaFact]
    public async Task ExtensionsManager_Menu_Is_Ordered_Before_Animation()
    {
        MainWindowViewModel vm = new();
        MainWindow window = new(vm);

        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => window.ApplyTemplate());

        MenuItem? viewMenu = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .FirstOrDefault(item => string.Equals(item.Header as string, "_View", System.StringComparison.Ordinal));

        Assert.NotNull(viewMenu);

        var items = viewMenu!.Items?.OfType<MenuItem>().ToList() ?? new List<MenuItem>();
        int extensionsIndex = items.FindIndex(item => string.Equals(item.Header as string, "_Extensions Manager", System.StringComparison.Ordinal));
        int animationIndex = items.FindIndex(item => string.Equals(item.Header as string, "_Animation", System.StringComparison.Ordinal));

        Assert.True(extensionsIndex >= 0, "Extensions Manager menu item not found.");
        Assert.True(animationIndex >= 0, "Animation menu item not found.");
        Assert.True(extensionsIndex < animationIndex, "Extensions Manager should appear before Animation.");

        await Dispatcher.UIThread.InvokeAsync(() => window.Close());
    }

    [AvaloniaFact]
    public void ExtensionsManager_Dock_Toggles_Visibility()
    {
        MainWindowViewModel vm = new();

        ExtensionManagerTool? tool = XamlEditorDockFactory
            .FindDockable<ExtensionManagerTool>(vm.DockLayout, "ExtensionsManager");
        Assert.NotNull(tool);

        IDock? dock = XamlEditorDockFactory.FindDockable<IDock>(vm.DockLayout, "BottomToolDock");
        Assert.NotNull(dock);
        Assert.NotNull(dock!.VisibleDockables);

        vm.ToggleExtensionsManagerCommand.Execute().Subscribe();
        bool visibleAfterShow = dock.VisibleDockables!.Contains(tool);

        vm.ToggleExtensionsManagerCommand.Execute().Subscribe();
        bool visibleAfterHide = dock.VisibleDockables!.Contains(tool);

        vm.ToggleExtensionsManagerCommand.Execute().Subscribe();
        bool visibleAfterReshow = dock.VisibleDockables!.Contains(tool);

        Assert.True(visibleAfterShow);
        Assert.False(visibleAfterHide);
        Assert.True(visibleAfterReshow);
    }
}
