using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using XamlVisualEditor.App.Controls;
using XamlVisualEditor.App.Views;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Tests.UI;

public sealed class GitPanelViewTests
{
    [AvaloniaFact]
    public async Task GitPanelView_Renders_Diff_Lines()
    {
        GitPanelViewModel vm = new();
        vm.DiffLines.Add(new GitDiffLineViewModel(GitDiffLineKind.FileHeader, "diff --git a/a.txt b/a.txt", string.Empty, null, null));
        vm.DiffLines.Add(new GitDiffLineViewModel(GitDiffLineKind.HunkHeader, "@@ -1 +1 @@", "@@", null, null));
        vm.DiffLines.Add(new GitDiffLineViewModel(GitDiffLineKind.Added, "added", "+", null, 1));
        vm.DiffLines.Add(new GitDiffLineViewModel(GitDiffLineKind.Removed, "removed", "-", 1, null));

        GitPanelView view = new() { DataContext = vm };
        Window window = await ShowInWindowAsync(view).ConfigureAwait(false);
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                view.ApplyTemplate();
            });

            GitDiffTextEditor? editor = view.GetVisualDescendants().OfType<GitDiffTextEditor>().FirstOrDefault();
            Assert.NotNull(editor);
            Assert.Contains("diff --git", editor!.Text);
            Assert.Contains("+ added", editor.Text);
            Assert.Contains("- removed", editor.Text);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => window.Close());
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
