using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>
/// Dock factory for the MDI canvas layout.
/// </summary>
public sealed class CanvasDockFactory : Factory
{
    public DocumentDock? DocumentDock { get; private set; }

    public override IRootDock CreateLayout()
    {
        DocumentDock = new DocumentDock
        {
            Id = "CanvasDocumentDock",
            Title = "Canvas Documents",
            CanCreateDocument = false,
            LayoutMode = DocumentLayoutMode.Mdi,
            VisibleDockables = new ObservableCollection<IDockable>()
        };

        RootDock rootDock = new()
        {
            Id = "CanvasRoot",
            Title = "Canvas",
            DefaultDockable = DocumentDock,
            ActiveDockable = DocumentDock,
            VisibleDockables = CreateList<IDockable>(DocumentDock)
        };

        EnsureLayoutDefaults(rootDock);
        return rootDock;
    }

    public void EnsureOwnerReferences(IRootDock rootDock)
    {
        SetOwnerRecursive(rootDock, null);
    }

    private void SetOwnerRecursive(IDockable dockable, IDockable? owner)
    {
        if (dockable.Owner is null)
        {
            dockable.Owner = owner;
        }

        if (dockable is IDock dock)
        {
            dock.Factory ??= this;
            if (dock.VisibleDockables is not null)
            {
                foreach (IDockable child in dock.VisibleDockables)
                {
                    SetOwnerRecursive(child, dockable);
                }
            }
        }

        if (dockable is IRootDock rootDock)
        {
            SetOwnerList(rootDock.HiddenDockables, rootDock);
            SetOwnerList(rootDock.LeftPinnedDockables, rootDock);
            SetOwnerList(rootDock.RightPinnedDockables, rootDock);
            SetOwnerList(rootDock.TopPinnedDockables, rootDock);
            SetOwnerList(rootDock.BottomPinnedDockables, rootDock);
            if (rootDock.PinnedDock is not null)
            {
                SetOwnerRecursive(rootDock.PinnedDock, rootDock);
            }
        }
    }

    private void SetOwnerList(IList<IDockable>? dockables, IDockable owner)
    {
        if (dockables is null)
        {
            return;
        }

        foreach (IDockable dockable in dockables)
        {
            SetOwnerRecursive(dockable, owner);
        }
    }

    private static void EnsureLayoutDefaults(IRootDock rootDock)
    {
        rootDock.VisibleDockables ??= new ObservableCollection<IDockable>();
        rootDock.HiddenDockables ??= new ObservableCollection<IDockable>();
        rootDock.LeftPinnedDockables ??= new ObservableCollection<IDockable>();
        rootDock.RightPinnedDockables ??= new ObservableCollection<IDockable>();
        rootDock.TopPinnedDockables ??= new ObservableCollection<IDockable>();
        rootDock.BottomPinnedDockables ??= new ObservableCollection<IDockable>();
    }
}
