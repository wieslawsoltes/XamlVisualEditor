using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using XamlVisualEditor.Acp;
using XamlVisualEditor.App.Views;
using XamlVisualEditor.Shell.ViewModels;

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
            await Dispatcher.UIThread.InvokeAsync(() => { });

            ListBox[] listBoxes = view.GetVisualDescendants().OfType<ListBox>().ToArray();
            Assert.True(listBoxes.Length >= 3);

            bool transcriptMatched = listBoxes.Any(list => list.ItemCount == vm.Transcript.Count);
            bool sessionsMatched = listBoxes.Any(list => list.ItemCount == vm.Sessions.Count);
            bool activityMatched = listBoxes.Any(list => list.ItemCount == vm.Activity.Count);

            Assert.True(transcriptMatched, "Transcript list was not bound.");
            Assert.True(sessionsMatched, "Sessions list was not bound.");
            Assert.True(activityMatched, "Activity list was not bound.");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => window.Close());
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
            Button[] buttons = dialog.GetVisualDescendants().OfType<Button>().ToArray();
            int optionButtons = buttons.Count(button => string.Equals(button.Content?.ToString(), "Allow once", StringComparison.Ordinal)
                || string.Equals(button.Content?.ToString(), "Reject once", StringComparison.Ordinal));

            Assert.Equal(2, optionButtons);
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
