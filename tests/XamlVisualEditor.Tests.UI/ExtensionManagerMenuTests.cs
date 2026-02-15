using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Tests.UI;

public sealed class ExtensionManagerMenuTests
{
    private static string ResolveMainWindowXamlPath()
    {
        string current = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            string candidate = Path.Combine(current, "src", "XamlVisualEditor.App", "MainWindow.axaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            DirectoryInfo? parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        throw new FileNotFoundException("Could not locate MainWindow.axaml from test base directory.");
    }

    [Fact]
    public void ExtensionsManager_Menu_Wiring_Exists_In_MainWindowXaml()
    {
        string xamlPath = ResolveMainWindowXamlPath();
        string xaml = File.ReadAllText(xamlPath);

        Assert.Contains("Header=\"_Extensions Manager\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{CompiledBinding ToggleExtensionsManagerCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtensionsManager_Menu_Is_Ordered_Before_DocumentCanvas_In_MainWindowXaml()
    {
        string xamlPath = ResolveMainWindowXamlPath();
        string xaml = File.ReadAllText(xamlPath);

        int extensionsIndex = xaml.IndexOf("Header=\"_Extensions Manager\"", StringComparison.Ordinal);
        int canvasIndex = xaml.IndexOf("Header=\"Document _Canvas\"", StringComparison.Ordinal);

        Assert.True(extensionsIndex >= 0, "Extensions Manager menu entry is missing.");
        Assert.True(canvasIndex >= 0, "Document Canvas menu entry is missing.");
        Assert.True(extensionsIndex < canvasIndex, "Extensions Manager should appear before Document Canvas.");
    }

    [AvaloniaFact]
    public async Task ExtensionsManager_Command_Toggles_VisibilityFlag()
    {
        using MainWindowViewModel vm = new();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            bool initial = vm.IsExtensionsManagerVisible;
            vm.ToggleExtensionsManagerCommand.Execute().Subscribe();
            bool afterFirstToggle = vm.IsExtensionsManagerVisible;

            vm.ToggleExtensionsManagerCommand.Execute().Subscribe();
            bool afterSecondToggle = vm.IsExtensionsManagerVisible;

            Assert.NotEqual(initial, afterFirstToggle);
            Assert.Equal(initial, afterSecondToggle);
        });
    }
}
