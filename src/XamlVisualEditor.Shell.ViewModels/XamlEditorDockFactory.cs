using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Reactive.Linq;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using Dock.Serializer.SystemTextJson;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Collaboration.UI;
using XamlVisualEditor.PropertyEditor;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Shell;

/// <summary>
/// Dock tool for the toolbox panel.
/// </summary>
public sealed class ToolboxTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public ToolboxViewModel? ToolboxViewModel { get; set; }

    public ToolboxTool()
    {
        Id = "Toolbox";
        Title = "Toolbox";
    }

    public ToolboxTool(ToolboxViewModel toolboxViewModel)
    {
        ToolboxViewModel = toolboxViewModel;
        Id = "Toolbox";
        Title = "Toolbox";
    }
}

/// <summary>
/// Dock tool for the solution explorer panel.
/// </summary>
public sealed class SolutionExplorerTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public SolutionExplorerViewModel? SolutionExplorerViewModel { get; set; }

    public SolutionExplorerTool()
    {
        Id = "SolutionExplorer";
        Title = "Solution Explorer";
    }

    public SolutionExplorerTool(SolutionExplorerViewModel solutionExplorerViewModel)
    {
        SolutionExplorerViewModel = solutionExplorerViewModel;
        Id = "SolutionExplorer";
        Title = "Solution Explorer";
    }
}

/// <summary>
/// Dock tool for the property editor panel.
/// </summary>
public sealed class PropertyEditorTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public MainWindowViewModel? MainViewModel { get; set; }

    internal static MainWindowViewModel? DefaultMainViewModel { get; set; }

    [IgnoreDataMember]
    [Reactive]
    public PropertyEditorViewModel? PropertyEditor { get; private set; }

    private IDisposable? _activeDocSubscription;

    public PropertyEditorTool()
    {
        Id = "Properties";
        Title = "Properties";
        MainViewModel = DefaultMainViewModel;
    }

    public PropertyEditorTool(MainWindowViewModel mainViewModel)
    {
        MainViewModel = mainViewModel;
        Id = "Properties";
        Title = "Properties";
    }

    public void Bind(MainWindowViewModel mainViewModel)
    {
        MainViewModel = mainViewModel;
        _activeDocSubscription?.Dispose();
        _activeDocSubscription = mainViewModel.WhenAnyValue(x => x.ActiveDesignerDocument)
            .Select(doc => doc?.PropertyEditor)
            .Subscribe(vm => PropertyEditor = vm);
    }
}

/// <summary>
/// Dock tool for the visual tree panel.
/// </summary>
public sealed class VisualTreeTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public MainWindowViewModel? MainViewModel { get; set; }

    internal static MainWindowViewModel? DefaultMainViewModel { get; set; }

    public VisualTreeTool()
    {
        Id = "VisualTree";
        Title = "Visual Tree";
        MainViewModel = DefaultMainViewModel;
    }

    public VisualTreeTool(MainWindowViewModel mainViewModel)
    {
        MainViewModel = mainViewModel;
        Id = "VisualTree";
        Title = "Visual Tree";
    }
}

/// <summary>
/// Dock tool for the logical tree panel.
/// </summary>
public sealed class LogicalTreeTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public MainWindowViewModel? MainViewModel { get; set; }

    internal static MainWindowViewModel? DefaultMainViewModel { get; set; }

    public LogicalTreeTool()
    {
        Id = "LogicalTree";
        Title = "Logical Tree";
        MainViewModel = DefaultMainViewModel;
    }

    public LogicalTreeTool(MainWindowViewModel mainViewModel)
    {
        MainViewModel = mainViewModel;
        Id = "LogicalTree";
        Title = "Logical Tree";
    }
}

/// <summary>
/// Dock tool for the output panel.
/// </summary>
public sealed class OutputTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public OutputViewModel? OutputViewModel { get; set; }

    public OutputTool()
    {
        Id = "Output";
        Title = "Output";
    }

    public OutputTool(OutputViewModel outputViewModel)
    {
        OutputViewModel = outputViewModel;
        Id = "Output";
        Title = "Output";
    }
}

/// <summary>
/// Dock tool for the collaboration panel.
/// </summary>
public sealed class CollaborationTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public CollaborationPanelViewModel? CollaborationViewModel { get; set; }

    public CollaborationTool()
    {
        Id = "Collaboration";
        Title = "Collaboration";
    }

    public CollaborationTool(CollaborationPanelViewModel collaborationViewModel)
    {
        CollaborationViewModel = collaborationViewModel;
        Id = "Collaboration";
        Title = "Collaboration";
    }
}

/// <summary>
/// Dock tool for the references panel.
/// </summary>
public sealed class ReferencesTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public ReferencesViewModel? ReferencesViewModel { get; set; }

    public ReferencesTool()
    {
        Id = "References";
        Title = "References";
    }

    public ReferencesTool(ReferencesViewModel referencesViewModel)
    {
        ReferencesViewModel = referencesViewModel;
        Id = "References";
        Title = "References";
    }
}

/// <summary>
/// Dock document for a XAML designer document.
/// </summary>
public sealed class DesignerDocument : Document
{
    [IgnoreDataMember]
    public DesignerDocumentViewModel DocumentViewModel { get; }

    public DesignerDocument(DesignerDocumentViewModel documentViewModel)
    {
        DocumentViewModel = documentViewModel;
        Id = documentViewModel.FilePath;
        Title = documentViewModel.FileName;
        CanClose = true;
    }
}

/// <summary>
/// Dock document for a text file.
/// </summary>
public sealed class TextDocument : Document
{
    [IgnoreDataMember]
    public TextDocumentViewModel DocumentViewModel { get; }

    public TextDocument(TextDocumentViewModel documentViewModel)
    {
        DocumentViewModel = documentViewModel;
        Id = documentViewModel.FilePath;
        Title = documentViewModel.FileName;
        CanClose = true;
    }
}

/// <summary>
/// Dock document for the infinite editor canvas.
/// </summary>
public sealed class InfiniteCanvasDocument : Document
{
    [IgnoreDataMember]
    [Reactive]
    public InfiniteCanvasViewModel? CanvasViewModel { get; set; }

    public InfiniteCanvasDocument()
    {
        Id = "InfiniteCanvas";
        Title = "Canvas";
        CanClose = true;
    }

    public InfiniteCanvasDocument(InfiniteCanvasViewModel canvasViewModel)
    {
        CanvasViewModel = canvasViewModel;
        Id = "InfiniteCanvas";
        Title = "Canvas";
        CanClose = true;
    }
}

/// <summary>
/// Factory that creates the default VS/Blend-style docking layout.
/// </summary>
public sealed class XamlEditorDockFactory : Factory
{
    private readonly MainWindowViewModel _mainVm;
    private static readonly DockSerializer s_serializer = new(typeof(ObservableCollection<>));

    public XamlEditorDockFactory(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;
        PropertyEditorTool.DefaultMainViewModel = mainVm;
        VisualTreeTool.DefaultMainViewModel = mainVm;
        LogicalTreeTool.DefaultMainViewModel = mainVm;
    }

    /// <summary>
    /// Creates the default layout with docking panels arranged in a VS/Blend style.
    /// </summary>
    public IRootDock CreateDefaultLayout()
    {
        // Left tools: Solution Explorer, Toolbox
        SolutionExplorerTool solutionExplorerTool = new(_mainVm.SolutionExplorer);
        ToolboxTool toolboxTool = new(_mainVm.Toolbox);

        // Right tools: Properties, Visual Tree, Logical Tree
        PropertyEditorTool propertyTool = new(_mainVm);
        VisualTreeTool visualTreeTool = new(_mainVm);
        LogicalTreeTool logicalTreeTool = new(_mainVm);

        // Bottom tools: Output, Collaboration
        OutputTool outputTool = new(_mainVm.Output);
        ReferencesTool referencesTool = new(_mainVm.References);
        CollaborationTool collabTool = new(_mainVm.Collaboration);

        // Left tool dock
        ToolDock leftToolDock = new()
        {
            Id = "LeftToolDock",
            Title = "Left Tools",
            Proportion = 0.2,
            ActiveDockable = solutionExplorerTool,
            VisibleDockables = CreateList<IDockable>(solutionExplorerTool, toolboxTool),
            Alignment = Alignment.Left
        };

        // Right tool dock
        ToolDock rightToolDock = new()
        {
            Id = "RightToolDock",
            Title = "Right Tools",
            Proportion = 0.25,
            ActiveDockable = propertyTool,
            VisibleDockables = CreateList<IDockable>(propertyTool, visualTreeTool, logicalTreeTool),
            Alignment = Alignment.Right
        };

        // Bottom tool dock
        ToolDock bottomToolDock = new()
        {
            Id = "BottomToolDock",
            Title = "Bottom Tools",
            Proportion = 0.25,
            ActiveDockable = outputTool,
            VisibleDockables = CreateList<IDockable>(outputTool, referencesTool, collabTool),
            Alignment = Alignment.Bottom
        };

        // Document dock (center)
        DocumentDock documentDock = new()
        {
            Id = "DocumentDock",
            Title = "Documents",
            IsCollapsable = false,
            VisibleDockables = CreateList<IDockable>(),
            ActiveDockable = null,
            CanCreateDocument = false,
            EnableWindowDrag = true
        };

        // Main layout: Left | (Center / Bottom) | Right
        ProportionalDock centerAndBottom = new()
        {
            Id = "CenterAndBottom",
            Orientation = Orientation.Vertical,
            Proportion = double.NaN,
            VisibleDockables = CreateList<IDockable>(
                documentDock,
                new ProportionalDockSplitter(),
                bottomToolDock)
        };

        ProportionalDock mainLayout = new()
        {
            Id = "MainLayout",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                leftToolDock,
                new ProportionalDockSplitter(),
                centerAndBottom,
                new ProportionalDockSplitter(),
                rightToolDock)
        };

        RootDock rootDock = new()
        {
            Id = "Root",
            Title = "Root",
            IsCollapsable = false,
            ActiveDockable = mainLayout,
            DefaultDockable = mainLayout,
            VisibleDockables = CreateList<IDockable>(mainLayout)
        };

        EnsureLayoutDefaults(rootDock);

        return rootDock;
    }

    /// <summary>
    /// Ensures deserialized tools have their view model references wired.
    /// </summary>
    public void ConfigureToolViewModels(IRootDock rootDock)
    {
        ToolboxTool? toolboxTool = FindDockable<ToolboxTool>(rootDock, "Toolbox");
        if (toolboxTool is not null)
        {
            toolboxTool.ToolboxViewModel = _mainVm.Toolbox;
        }

        SolutionExplorerTool? solutionTool = FindDockable<SolutionExplorerTool>(rootDock, "SolutionExplorer");
        if (solutionTool is not null)
        {
            solutionTool.SolutionExplorerViewModel = _mainVm.SolutionExplorer;
        }

        PropertyEditorTool? propertyTool = FindDockable<PropertyEditorTool>(rootDock, "Properties");
        if (propertyTool is not null)
        {
            propertyTool.Bind(_mainVm);
        }

        VisualTreeTool? visualTreeTool = FindDockable<VisualTreeTool>(rootDock, "VisualTree");
        if (visualTreeTool is not null)
        {
            visualTreeTool.MainViewModel = _mainVm;
        }

        LogicalTreeTool? logicalTreeTool = FindDockable<LogicalTreeTool>(rootDock, "LogicalTree");
        if (logicalTreeTool is not null)
        {
            logicalTreeTool.MainViewModel = _mainVm;
        }

        OutputTool? outputTool = FindDockable<OutputTool>(rootDock, "Output");
        if (outputTool is not null)
        {
            outputTool.OutputViewModel = _mainVm.Output;
        }

        ReferencesTool? referencesTool = FindDockable<ReferencesTool>(rootDock, "References");
        if (referencesTool is not null)
        {
            referencesTool.ReferencesViewModel = _mainVm.References;
        }

        CollaborationTool? collabTool = FindDockable<CollaborationTool>(rootDock, "Collaboration");
        if (collabTool is not null)
        {
            collabTool.CollaborationViewModel = _mainVm.Collaboration;
        }
    }

    /// <summary>
    /// Ensures deserialized documents have their view model references wired.
    /// </summary>
    public void ConfigureDocumentViewModels(IRootDock rootDock)
    {
        InfiniteCanvasDocument? canvasDoc = FindDockable<InfiniteCanvasDocument>(rootDock, "InfiniteCanvas");
        if (canvasDoc is not null)
        {
            canvasDoc.CanvasViewModel = _mainVm.InfiniteCanvas;
        }
    }

    public override void InitLayout(IDockable layout)
    {
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        base.InitLayout(layout);
    }

    public void EnsureOwnerReferences(IRootDock rootDock)
    {
        SetOwnerRecursive(rootDock, null);
    }

    /// <summary>
    /// Adds a document to the document dock.
    /// </summary>
    public DesignerDocument? AddDocument(IRootDock rootDock, DesignerDocumentViewModel documentVm)
    {
        DesignerDocument doc = new(documentVm);

        // Find the document dock
        DocumentDock? docDock = FindDockable<DocumentDock>(rootDock, "DocumentDock");
        if (docDock is not null)
        {
            AddDockable(docDock, doc);
            SetActiveDockable(doc);
            SetFocusedDockable(docDock, doc);
            return doc;
        }

        return null;
    }

    public TextDocument? AddTextDocument(IRootDock rootDock, TextDocumentViewModel documentVm)
    {
        TextDocument doc = new(documentVm);

        DocumentDock? docDock = FindDockable<DocumentDock>(rootDock, "DocumentDock");
        if (docDock is not null)
        {
            AddDockable(docDock, doc);
            SetActiveDockable(doc);
            SetFocusedDockable(docDock, doc);
            return doc;
        }

        return null;
    }

    public InfiniteCanvasDocument? AddCanvasDocument(IRootDock rootDock, InfiniteCanvasViewModel canvasViewModel)
    {
        InfiniteCanvasDocument doc = new(canvasViewModel);

        DocumentDock? docDock = FindDockable<DocumentDock>(rootDock, "DocumentDock");
        if (docDock is not null)
        {
            AddDockable(docDock, doc);
            SetActiveDockable(doc);
            SetFocusedDockable(docDock, doc);
            return doc;
        }

        return null;
    }

    public static T? FindDockable<T>(IDockable dockable, string id) where T : class, IDockable
    {
        if (dockable is T typed && dockable.Id == id)
        {
            return typed;
        }

        if (dockable is IDock dock && dock.VisibleDockables is not null)
        {
            foreach (IDockable child in dock.VisibleDockables)
            {
                T? result = FindDockable<T>(child, id);
                if (result is not null)
                {
                    return result;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the default layout file path.
    /// </summary>
    public static string GetDefaultLayoutPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, "XamlVisualEditor");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "dock-layout.json");
    }

    /// <summary>
    /// Saves the current dock layout to a JSON file.
    /// </summary>
    public static void SaveLayout(IRootDock rootDock, string? filePath = null)
    {
        filePath ??= GetDefaultLayoutPath();
        List<Action> restoreActions = DetachToolViewModels(rootDock);
        restoreActions.AddRange(DetachDockRuntimeReferences(rootDock));
        try
        {
            string json = s_serializer.Serialize(rootDock);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to save dock layout: {ex.Message}");
        }
        finally
        {
            foreach (Action restore in restoreActions)
            {
                restore();
            }
        }
    }

    /// <summary>
    /// Loads a dock layout from a JSON file.
    /// </summary>
    public static IRootDock? LoadLayout(string? filePath = null)
    {
        filePath ??= GetDefaultLayoutPath();
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            string json = File.ReadAllText(filePath);
            RootDock? rootDock = s_serializer.Deserialize<RootDock>(json);
            if (rootDock is not null)
            {
                EnsureLayoutDefaults(rootDock);
            }
            return rootDock;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to load dock layout: {ex.Message}");
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception deleteEx)
            {
                System.Diagnostics.Trace.TraceWarning($"Failed to delete invalid dock layout: {deleteEx.Message}");
            }
            return null;
        }
    }

    public static void EnsureLayoutDefaults(IRootDock rootDock)
    {
        if (string.IsNullOrWhiteSpace(rootDock.Id))
        {
            rootDock.Id = "Root";
        }

        if (string.IsNullOrWhiteSpace(rootDock.Title))
        {
            rootDock.Title = "Root";
        }

        rootDock.VisibleDockables ??= new ObservableCollection<IDockable>();
        rootDock.HiddenDockables ??= new ObservableCollection<IDockable>();
        rootDock.LeftPinnedDockables ??= new ObservableCollection<IDockable>();
        rootDock.RightPinnedDockables ??= new ObservableCollection<IDockable>();
        rootDock.TopPinnedDockables ??= new ObservableCollection<IDockable>();
        rootDock.BottomPinnedDockables ??= new ObservableCollection<IDockable>();
        rootDock.Windows ??= new ObservableCollection<IDockWindow>();

        if (rootDock.PinnedDock is null)
        {
            rootDock.PinnedDock = new ToolDock
            {
                Id = "PinnedDock",
                Title = "Pinned",
                Alignment = Alignment.Left,
                VisibleDockables = new ObservableCollection<IDockable>()
            };
        }

        rootDock.DockGroup ??= "Root";
        rootDock.CanDrag = true;
        rootDock.CanDrop = true;
    }

    private static List<Action> DetachToolViewModels(IRootDock rootDock)
    {
        List<Action> restore = new();

        DetachToolViewModel(
            FindDockable<ToolboxTool>(rootDock, "Toolbox"),
            tool => tool.ToolboxViewModel,
            (tool, vm) => tool.ToolboxViewModel = vm,
            restore);

        DetachToolViewModel(
            FindDockable<SolutionExplorerTool>(rootDock, "SolutionExplorer"),
            tool => tool.SolutionExplorerViewModel,
            (tool, vm) => tool.SolutionExplorerViewModel = vm,
            restore);

        DetachToolViewModel(
            FindDockable<PropertyEditorTool>(rootDock, "Properties"),
            tool => tool.MainViewModel,
            (tool, vm) => tool.MainViewModel = vm,
            restore);

        DetachToolViewModel(
            FindDockable<VisualTreeTool>(rootDock, "VisualTree"),
            tool => tool.MainViewModel,
            (tool, vm) => tool.MainViewModel = vm,
            restore);

        DetachToolViewModel(
            FindDockable<LogicalTreeTool>(rootDock, "LogicalTree"),
            tool => tool.MainViewModel,
            (tool, vm) => tool.MainViewModel = vm,
            restore);

        DetachToolViewModel(
            FindDockable<OutputTool>(rootDock, "Output"),
            tool => tool.OutputViewModel,
            (tool, vm) => tool.OutputViewModel = vm,
            restore);

        DetachToolViewModel(
            FindDockable<ReferencesTool>(rootDock, "References"),
            tool => tool.ReferencesViewModel,
            (tool, vm) => tool.ReferencesViewModel = vm,
            restore);

        DetachToolViewModel(
            FindDockable<CollaborationTool>(rootDock, "Collaboration"),
            tool => tool.CollaborationViewModel,
            (tool, vm) => tool.CollaborationViewModel = vm,
            restore);

        return restore;
    }

    private static void DetachToolViewModel<TTool, TViewModel>(
        TTool? tool,
        Func<TTool, TViewModel?> getter,
        Action<TTool, TViewModel?> setter,
        List<Action> restore)
        where TTool : class
        where TViewModel : class
    {
        if (tool is null)
        {
            return;
        }

        TViewModel? current = getter(tool);
        if (current is null)
        {
            return;
        }

        setter(tool, null);
        restore.Add(() => setter(tool, current));
    }

    private static List<Action> DetachDockRuntimeReferences(IRootDock rootDock)
    {
        List<Action> restore = new();

        if (rootDock.Window is not null)
        {
            IDockWindow? window = rootDock.Window;
            rootDock.Window = null;
            restore.Add(() => rootDock.Window = window);
        }

        if (rootDock.Windows is not null)
        {
            IList<IDockWindow>? windows = rootDock.Windows;
            rootDock.Windows = null;
            restore.Add(() => rootDock.Windows = windows);
        }

        DetachOwnersRecursive(rootDock, restore);
        DetachFactoryRecursive(rootDock, restore);
        return restore;
    }

    private static void DetachFactoryRecursive(IDockable dockable, List<Action> restore)
    {
        if (dockable is IDock dock && dock.Factory is not null)
        {
            IFactory? factory = dock.Factory;
            dock.Factory = null;
            restore.Add(() => dock.Factory = factory);
        }

        if (dockable is not IDock dockWithChildren || dockWithChildren.VisibleDockables is null)
        {
            return;
        }

        foreach (IDockable child in dockWithChildren.VisibleDockables)
        {
            DetachFactoryRecursive(child, restore);
        }

        if (dockable is IRootDock rootDock)
        {
            DetachFactoryList(rootDock.HiddenDockables, restore);
            DetachFactoryList(rootDock.LeftPinnedDockables, restore);
            DetachFactoryList(rootDock.RightPinnedDockables, restore);
            DetachFactoryList(rootDock.TopPinnedDockables, restore);
            DetachFactoryList(rootDock.BottomPinnedDockables, restore);
            if (rootDock.PinnedDock is not null)
            {
                DetachFactoryRecursive(rootDock.PinnedDock, restore);
            }
        }
    }

    private static void DetachFactoryList(IList<IDockable>? dockables, List<Action> restore)
    {
        if (dockables is null)
        {
            return;
        }

        foreach (IDockable dockable in dockables)
        {
            DetachFactoryRecursive(dockable, restore);
        }
    }

    private static void DetachOwnersRecursive(IDockable dockable, List<Action> restore)
    {
        if (dockable.Owner is not null)
        {
            IDockable? owner = dockable.Owner;
            dockable.Owner = null;
            restore.Add(() => dockable.Owner = owner);
        }

        if (dockable is not IDock dock || dock.VisibleDockables is null)
        {
            return;
        }

        foreach (IDockable child in dock.VisibleDockables)
        {
            DetachOwnersRecursive(child, restore);
        }

        if (dock is IRootDock rootDock)
        {
            DetachOwnersList(rootDock.HiddenDockables, restore);
            DetachOwnersList(rootDock.LeftPinnedDockables, restore);
            DetachOwnersList(rootDock.RightPinnedDockables, restore);
            DetachOwnersList(rootDock.TopPinnedDockables, restore);
            DetachOwnersList(rootDock.BottomPinnedDockables, restore);
            if (rootDock.PinnedDock is not null)
            {
                DetachOwnersRecursive(rootDock.PinnedDock, restore);
            }
        }
    }

    private static void DetachOwnersList(IList<IDockable>? dockables, List<Action> restore)
    {
        if (dockables is null)
        {
            return;
        }

        foreach (IDockable dockable in dockables)
        {
            DetachOwnersRecursive(dockable, restore);
        }
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
}
