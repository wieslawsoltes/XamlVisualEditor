using XamlVisualEditor.Extensions;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class StatusBarIntegrationTests
{
    [Fact]
    public void UpsertStatusBarItem_AddsAndMovesBetweenAlignments()
    {
        using MainWindowViewModel viewModel = new();

        viewModel.UpsertStatusBarItem(
            "status.collab",
            "Collab: Offline",
            "No active collaboration session",
            "collaboration.toggleView",
            StatusBarAlignment.Left,
            95,
            isVisible: true);

        Assert.Single(viewModel.LeftStatusBarItems);
        Assert.Empty(viewModel.RightStatusBarItems);
        Assert.Equal("Collab: Offline", viewModel.LeftStatusBarItems[0].Text);
        Assert.NotNull(viewModel.LeftStatusBarItems[0].Command);

        viewModel.UpsertStatusBarItem(
            "status.collab",
            "Collab: 2",
            "Session with two participants",
            null,
            StatusBarAlignment.Right,
            10,
            isVisible: true);

        Assert.Empty(viewModel.LeftStatusBarItems);
        Assert.Single(viewModel.RightStatusBarItems);
        Assert.Equal("Collab: 2", viewModel.RightStatusBarItems[0].Text);
        Assert.Null(viewModel.RightStatusBarItems[0].Command);
    }

    [Fact]
    public void UpsertStatusBarItem_HideAndRemove_Work()
    {
        using MainWindowViewModel viewModel = new();

        viewModel.UpsertStatusBarItem(
            "status.sample",
            "Sample",
            null,
            null,
            StatusBarAlignment.Left,
            1,
            isVisible: true);

        Assert.Single(viewModel.LeftStatusBarItems);

        viewModel.UpsertStatusBarItem(
            "status.sample",
            "Sample",
            null,
            null,
            StatusBarAlignment.Left,
            1,
            isVisible: false);

        Assert.Empty(viewModel.LeftStatusBarItems);

        viewModel.RemoveStatusBarItem("status.sample");
        Assert.Empty(viewModel.LeftStatusBarItems);
        Assert.Empty(viewModel.RightStatusBarItems);
    }
}
