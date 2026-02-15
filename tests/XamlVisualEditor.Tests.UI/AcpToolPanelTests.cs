using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using XamlVisualEditor.Acp;
using XamlVisualEditor.AcpExtension;
using XamlVisualEditor.AcpExtension.Views;

namespace XamlVisualEditor.Tests.UI;

public sealed class AcpToolPanelTests
{
    [AvaloniaFact]
    public async Task AcpToolView_ShowsMockTranscriptAndLists()
    {
        AcpToolViewModel vm = new();
        AcpToolView view = new() { DataContext = vm };

        Window window = await ShowInWindowAsync(view).ConfigureAwait(false);
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                view.ApplyTemplate();

                ListBox[] listBoxes = view.GetVisualDescendants().OfType<ListBox>().ToArray();
                ListBox? transcript = listBoxes.FirstOrDefault();
                Assert.NotNull(transcript);
                Assert.Equal(vm.Transcript.Count, transcript!.ItemCount);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => window.Close());
            vm.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task AcpPermissionDialog_ShowsOptions()
    {
        AcpPermissionOption[] options =
        {
            new("allow_once", "Allow once", "allow_once"),
            new("reject_once", "Reject once", "reject_once")
        };

        AcpPermissionRequest request = new(
            "s1",
            options,
            "tool-1",
            "Write file",
            "edit",
            null);

        AcpPermissionDialogViewModel vm = new(request);
        AcpPermissionDialog dialog = new() { DataContext = vm };
        dialog.Show();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            dialog.ApplyTemplate();
        });
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ItemsControl? optionsControl = dialog.GetVisualDescendants()
                    .OfType<ItemsControl>()
                    .FirstOrDefault(control => ReferenceEquals(control.ItemsSource, vm.Options));

                Assert.NotNull(optionsControl);
                Assert.Equal(vm.Options.Count, optionsControl!.ItemCount);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => dialog.Close());
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
}
