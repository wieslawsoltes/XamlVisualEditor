using System.Collections;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using XamlVisualEditor.App.Views;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Tests.UI;

public sealed class ExtensionManagerViewTests
{
    [AvaloniaFact]
    public async Task ExtensionManagerView_Renders_Packages()
    {
        FakeManager manager = new();
        ExtensionManagerViewModel vm = new(manager, () => Task.FromResult<string?>(null));
        await vm.RefreshCommand.Execute().ToTask();

        ExtensionManagerView view = new() { DataContext = vm };
        Window window = await ShowInWindowAsync(view).ConfigureAwait(false);
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                view.ApplyTemplate();

                DataGrid? grid = view.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault();
                Assert.NotNull(grid);

                IEnumerable? itemsSource = grid!.ItemsSource;
                Assert.NotNull(itemsSource);
                Assert.Single(itemsSource!.Cast<object>());
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => window.Close());
            vm.Dispose();
        }
    }

    private static async Task<Window> ShowInWindowAsync(Control control, double width = 900, double height = 600)
    {
        Window window = new()
        {
            Content = control,
            Width = width,
            Height = height
        };

        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => { });
        return window;
    }

    private sealed class FakeManager : IExtensionManager
    {
        public Task<IReadOnlyList<ExtensionPackageInfo>> GetInstalledAsync(CancellationToken ct)
        {
            ExtensionManifest manifest = new()
            {
                Name = "sample",
                Publisher = "example",
                Version = "1.0.0"
            };
            return Task.FromResult<IReadOnlyList<ExtensionPackageInfo>>(new[]
            {
                new ExtensionPackageInfo("sample.nupkg", manifest)
            });
        }

        public Task<ExtensionPackageInfo> InstallAsync(string packagePath, CancellationToken ct)
        {
            throw new System.NotImplementedException();
        }

        public Task UninstallAsync(string extensionId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task<bool> GetEnabledAsync(string extensionId, CancellationToken ct)
        {
            return Task.FromResult(true);
        }

        public Task SetEnabledAsync(string extensionId, bool enabled, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ExtensionUpdateInfo>> CheckForUpdatesAsync(CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<ExtensionUpdateInfo>>(Array.Empty<ExtensionUpdateInfo>());
        }
    }
}
