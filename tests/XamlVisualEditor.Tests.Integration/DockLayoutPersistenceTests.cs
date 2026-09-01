using System;
using System.IO;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using XamlVisualEditor.Shell;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Integration;

public sealed class DockLayoutPersistenceTests
{
    [Fact]
    public void SaveLayout_RoundTrips_WithOpenDocumentsAndPinnedDock()
    {
        using MainWindowViewModel viewModel = new();
        IRootDock layout = viewModel.DockFactory.CreateDefaultLayout();
        viewModel.DockFactory.InitLayout(layout);
        viewModel.DockFactory.EnsureOwnerReferences(layout);

        TextDocumentViewModel documentVm = new(Path.Combine(Path.GetTempPath(), "roundtrip.txt"));
        TextDocument? document = viewModel.DockFactory.AddTextDocument(layout, documentVm);
        Assert.NotNull(document);
        Assert.NotNull(layout.PinnedDock);

        string path = Path.Combine(
            Path.GetTempPath(),
            "xve-dock-layout-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            viewModel.DockFactory.SaveLayout(layout, path);

            // Saving must leave the live layout untouched.
            DocumentDock? documentDock = XamlEditorDockFactory.FindDockable<DocumentDock>(layout, "DocumentDock");
            Assert.NotNull(documentDock);
            Assert.Contains(document, documentDock!.VisibleDockables!);
            Assert.Same(document, documentDock.ActiveDockable);
            Assert.NotNull(layout.PinnedDock);

            IRootDock? restored = viewModel.DockFactory.LoadLayout(path);

            Assert.NotNull(restored);

            // The serializer materializes Active-/DefaultDockable as duplicate subtrees;
            // after loading they must reference the visible tree, not a copy.
            Assert.NotNull(restored!.ActiveDockable);
            Assert.Same(restored.VisibleDockables![0], restored.ActiveDockable);
            foreach (IDock dock in XamlEditorDockFactory.FindDockables<IDock>(restored))
            {
                if (dock.ActiveDockable is not null)
                {
                    Assert.Contains(dock.ActiveDockable, dock.VisibleDockables!);
                }
            }

            Assert.NotNull(XamlEditorDockFactory.FindDockable<ToolDock>(restored!, "LeftToolDock"));
            Assert.NotNull(XamlEditorDockFactory.FindDockable<ToolDock>(restored!, "RightToolDock"));
            Assert.NotNull(XamlEditorDockFactory.FindDockable<ToolDock>(restored!, "BottomToolDock"));

            DocumentDock? restoredDocuments =
                XamlEditorDockFactory.FindDockable<DocumentDock>(restored!, "DocumentDock");
            Assert.NotNull(restoredDocuments);

            // Open documents are transient and must not survive the round trip.
            Assert.DoesNotContain(restoredDocuments!.VisibleDockables!, d => d is TextDocument);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveLayout_RoundTrip_KeepsPinnedToolsStable()
    {
        using MainWindowViewModel viewModel = new();
        IRootDock layout = viewModel.DockFactory.CreateDefaultLayout();
        viewModel.DockFactory.InitLayout(layout);
        viewModel.DockFactory.EnsureOwnerReferences(layout);
        viewModel.DockFactory.CollapseBottomToolDock(layout);

        int pinnedCount = layout.BottomPinnedDockables!.Count;
        Assert.True(pinnedCount > 0);

        string path = Path.Combine(
            Path.GetTempPath(),
            "xve-dock-layout-pinned-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            viewModel.DockFactory.SaveLayout(layout, path);
            IRootDock? restored = viewModel.DockFactory.LoadLayout(path);
            Assert.NotNull(restored);

            viewModel.DockFactory.SaveLayout(restored!, path);
            IRootDock? restoredAgain = viewModel.DockFactory.LoadLayout(path);
            Assert.NotNull(restoredAgain);

            // The pinned tools must survive the cycles without duplication.
            Assert.Equal(pinnedCount, restoredAgain!.BottomPinnedDockables!.Count);
            Assert.Single(XamlEditorDockFactory.FindDockables<BreakpointsTool>(restoredAgain));
            Assert.Single(XamlEditorDockFactory.FindDockables<OutputTool>(restoredAgain));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadLayout_ReturnsNull_ForMissingFile()
    {
        using MainWindowViewModel viewModel = new();
        string path = Path.Combine(
            Path.GetTempPath(),
            "xve-dock-layout-missing-" + Guid.NewGuid().ToString("N") + ".json");

        Assert.Null(viewModel.DockFactory.LoadLayout(path));
    }
}
