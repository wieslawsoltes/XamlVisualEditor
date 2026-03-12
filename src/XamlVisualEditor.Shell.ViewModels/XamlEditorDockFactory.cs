using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Reactive.Linq;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using Dock.Serializer.SystemTextJson;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Shell;

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
/// Dock tool for terminal sessions.
/// </summary>
public sealed class TerminalTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public TerminalViewModel? TerminalViewModel { get; set; }

    public TerminalTool()
    {
        Id = "Terminal";
        Title = "Terminal";
    }

    public TerminalTool(TerminalViewModel terminalViewModel)
    {
        TerminalViewModel = terminalViewModel;
        Id = "Terminal-" + terminalViewModel.Id.ToString("N");
        Title = terminalViewModel.Title;
    }
}

/// <summary>
/// Dock tool for the breakpoints panel.
/// </summary>
public sealed class BreakpointsTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public BreakpointsViewModel? BreakpointsViewModel { get; set; }

    public BreakpointsTool()
    {
        Id = "Breakpoints";
        Title = "Breakpoints";
    }

    public BreakpointsTool(BreakpointsViewModel breakpoints)
    {
        BreakpointsViewModel = breakpoints;
        Id = "Breakpoints";
        Title = "Breakpoints";
    }
}

/// <summary>
/// Dock tool for the call stack panel.
/// </summary>
public sealed class CallStackTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public CallStackViewModel? CallStackViewModel { get; set; }

    public CallStackTool()
    {
        Id = "CallStack";
        Title = "Call Stack";
    }

    public CallStackTool(CallStackViewModel callStack)
    {
        CallStackViewModel = callStack;
        Id = "CallStack";
        Title = "Call Stack";
    }
}

/// <summary>
/// Dock tool for the locals panel.
/// </summary>
public sealed class LocalsTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public LocalsViewModel? LocalsViewModel { get; set; }

    public LocalsTool()
    {
        Id = "Locals";
        Title = "Locals";
    }

    public LocalsTool(LocalsViewModel locals)
    {
        LocalsViewModel = locals;
        Id = "Locals";
        Title = "Locals";
    }
}

/// <summary>
/// Dock tool for the watches panel.
/// </summary>
public sealed class WatchesTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public WatchesViewModel? WatchesViewModel { get; set; }

    public WatchesTool()
    {
        Id = "Watches";
        Title = "Watches";
    }

    public WatchesTool(WatchesViewModel watches)
    {
        WatchesViewModel = watches;
        Id = "Watches";
        Title = "Watches";
    }
}

/// <summary>
/// Dock tool for extension management.
/// </summary>
public sealed class ExtensionManagerTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public ExtensionManagerViewModel? ExtensionManagerViewModel { get; set; }

    public ExtensionManagerTool()
    {
        Id = "ExtensionsManager";
        Title = "Extensions";
    }

    public ExtensionManagerTool(ExtensionManagerViewModel viewModel)
        : this()
    {
        ExtensionManagerViewModel = viewModel;
    }
}

/// <summary>
/// Dock tool for extension-contributed views.
/// </summary>
public sealed class ExtensionTool : Tool
{
    public const string IdPrefix = "Extension:";

    [IgnoreDataMember]
    [Reactive]
    public ExtensionViewModel? ExtensionViewModel { get; set; }

    public string? ViewId { get; set; }

    public bool PersistDockState { get; set; } = true;

    public ExtensionTool()
    {
        Id = "Extension";
        Title = "Extension";
    }

    public ExtensionTool(ExtensionViewModel viewModel)
    {
        ExtensionViewModel = viewModel;
        ViewId = viewModel.ViewId;
        PersistDockState = viewModel.PersistDockState;
        Id = BuildId(viewModel.ViewId);
        Title = viewModel.Title;
    }

    public static string BuildId(string viewId)
    {
        return IdPrefix + viewId;
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
    private static readonly bool LogLayoutWarnings = false;
    private const string SolutionExplorerViewId = "solutionExplorer.panel";
    private readonly MainWindowViewModel _mainVm;
    private readonly ILogger<XamlEditorDockFactory> _logger;
    private static readonly DockSerializer s_serializer = new(typeof(ObservableCollection<>));

    public XamlEditorDockFactory(
        MainWindowViewModel mainVm,
        ILogger<XamlEditorDockFactory>? logger = null)
    {
        _mainVm = mainVm;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<XamlEditorDockFactory>.Instance;
    }

    /// <summary>
    /// Creates the default layout with docking panels arranged in a VS/Blend style.
    /// </summary>
    public IRootDock CreateDefaultLayout()
    {
        // Left tools: extensions will populate

        // Bottom tools: output and debugging panels (extensions contribute the rest).
        OutputTool outputTool = new(_mainVm.Output);
        BreakpointsTool breakpointsTool = new(_mainVm.Breakpoints);
        CallStackTool callStackTool = new(_mainVm.CallStack);
        LocalsTool localsTool = new(_mainVm.Locals);
        WatchesTool watchesTool = new(_mainVm.Watches);
        ExtensionManagerTool extensionManagerTool = new(_mainVm.ExtensionManager);

        // Left tool dock
        ToolDock leftToolDock = new()
        {
            Id = "LeftToolDock",
            Title = "Left Tools",
            Proportion = 0.2,
            ActiveDockable = null,
            VisibleDockables = CreateList<IDockable>(),
            Alignment = Alignment.Left
        };

        // Right tool dock
        ToolDock rightToolDock = new()
        {
            Id = "RightToolDock",
            Title = "Right Tools",
            Proportion = 0.25,
            ActiveDockable = null,
            VisibleDockables = CreateList<IDockable>(),
            Alignment = Alignment.Right
        };

        // Bottom tool dock
        ToolDock bottomToolDock = new()
        {
            Id = "BottomToolDock",
            Title = "Bottom Tools",
            Proportion = 0.25,
            ActiveDockable = outputTool,
            VisibleDockables = CreateList<IDockable>(
                outputTool,
                breakpointsTool,
                callStackTool,
                localsTool,
                watchesTool,
                extensionManagerTool),
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
        SolutionExplorerTool? solutionTool = FindDockable<SolutionExplorerTool>(rootDock, "SolutionExplorer");
        if (solutionTool is not null)
        {
            solutionTool.SolutionExplorerViewModel = _mainVm.SolutionExplorer;
        }

        OutputTool? outputTool = FindDockable<OutputTool>(rootDock, "Output");
        if (outputTool is not null)
        {
            outputTool.OutputViewModel = _mainVm.Output;
        }

        BreakpointsTool? breakpointsTool = FindDockable<BreakpointsTool>(rootDock, "Breakpoints");
        if (breakpointsTool is not null)
        {
            breakpointsTool.BreakpointsViewModel = _mainVm.Breakpoints;
        }

        CallStackTool? callStackTool = FindDockable<CallStackTool>(rootDock, "CallStack");
        if (callStackTool is not null)
        {
            callStackTool.CallStackViewModel = _mainVm.CallStack;
        }

        LocalsTool? localsTool = FindDockable<LocalsTool>(rootDock, "Locals");
        if (localsTool is not null)
        {
            localsTool.LocalsViewModel = _mainVm.Locals;
        }

        WatchesTool? watchesTool = FindDockable<WatchesTool>(rootDock, "Watches");
        if (watchesTool is not null)
        {
            watchesTool.WatchesViewModel = _mainVm.Watches;
        }

        ExtensionManagerTool? extensionManagerTool =
            FindDockable<ExtensionManagerTool>(rootDock, "ExtensionsManager");
        if (extensionManagerTool is not null)
        {
            extensionManagerTool.ExtensionManagerViewModel = _mainVm.ExtensionManager;
        }

        foreach (ExtensionTool extensionTool in FindDockables<ExtensionTool>(rootDock))
        {
            if (!string.IsNullOrWhiteSpace(extensionTool.ViewId)
                && _mainVm.TryGetExtensionView(extensionTool.ViewId, out ExtensionViewModel? viewModel)
                && viewModel is not null)
            {
                extensionTool.ExtensionViewModel = viewModel;
                extensionTool.Title = viewModel.Title;
            }
        }

        PruneTerminalTools(rootDock);
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
            SetOwnerRecursive(doc, docDock);
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
            SetOwnerRecursive(doc, docDock);
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
            SetOwnerRecursive(doc, docDock);
            SetActiveDockable(doc);
            SetFocusedDockable(docDock, doc);
            return doc;
        }

        return null;
    }

    public TerminalTool? AddTerminalTool(IRootDock rootDock, TerminalViewModel terminalViewModel)
    {
        ToolDock? toolDock = FindDockable<ToolDock>(rootDock, "BottomToolDock");
        if (toolDock is not null)
        {
            TerminalTool tool = new(terminalViewModel)
            {
                Title = terminalViewModel.Title
            };
            AddDockable(toolDock, tool);
            SetOwnerRecursive(tool, toolDock);
            SetActiveDockable(tool);
            SetFocusedDockable(toolDock, tool);
            return tool;
        }

        return null;
    }

    public ExtensionTool? AddExtensionTool(IRootDock rootDock, ExtensionViewModel viewModel)
    {
        string fallbackDockId = viewModel.Location switch
        {
            ExtensionViewLocation.Left => "LeftToolDock",
            ExtensionViewLocation.Right => "RightToolDock",
            ExtensionViewLocation.Bottom => "BottomToolDock",
            _ => "RightToolDock"
        };

        ToolDock? toolDock = null;
        if (!string.IsNullOrWhiteSpace(viewModel.ContainerId))
        {
            toolDock = FindDockable<ToolDock>(rootDock, viewModel.ContainerId);
            if (toolDock is null)
            {
                _logger.LogDebug(
                    "Extension container '{ContainerId}' was not found for view '{ViewId}'. Falling back to location dock.",
                    viewModel.ContainerId,
                    viewModel.ViewId);
            }
        }

        toolDock ??= FindDockable<ToolDock>(rootDock, fallbackDockId);
        if (toolDock is not null)
        {
            ExtensionTool tool = new(viewModel);
            int? desiredInsertIndex = null;
            if (toolDock.VisibleDockables is ObservableCollection<IDockable> dockables)
            {
                int insertIndex = dockables.Count;
                if (viewModel.Location == ExtensionViewLocation.Left)
                {
                    int solutionIndex = -1;
                    for (int i = 0; i < dockables.Count; i++)
                    {
                        if (dockables[i] is SolutionExplorerTool)
                        {
                            solutionIndex = i;
                            break;
                        }

                        if (dockables[i] is ExtensionTool extensionTool
                            && string.Equals(extensionTool.ViewId, SolutionExplorerViewId, StringComparison.OrdinalIgnoreCase))
                        {
                            solutionIndex = i;
                            break;
                        }
                    }

                    if (solutionIndex >= 0 && solutionIndex <= dockables.Count)
                    {
                        insertIndex = Math.Min(solutionIndex + 1, dockables.Count);
                    }
                    else
                    {
                        insertIndex = 0;
                    }
                }

                desiredInsertIndex = insertIndex;
            }

            AddDockable(toolDock, tool);
            SetOwnerRecursive(tool, toolDock);
            toolDock.IsEmpty = false;

            if (desiredInsertIndex is int targetIndex
                && toolDock.VisibleDockables is ObservableCollection<IDockable> orderedDockables)
            {
                int currentIndex = orderedDockables.IndexOf(tool);
                int boundedTargetIndex = Math.Clamp(targetIndex, 0, orderedDockables.Count - 1);
                if (currentIndex >= 0 && currentIndex != boundedTargetIndex)
                {
                    orderedDockables.Move(currentIndex, boundedTargetIndex);
                }
            }

            if (toolDock.ActiveDockable is null || viewModel.ActivateByDefault)
            {
                SetActiveDockable(tool);
                SetFocusedDockable(toolDock, tool);
            }
            return tool;
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

    public static IReadOnlyList<T> FindDockables<T>(IDockable dockable) where T : class, IDockable
    {
        List<T> results = new();
        CollectDockables(dockable, results);
        return results;
    }

    private static void CollectDockables<T>(IDockable dockable, List<T> results) where T : class, IDockable
    {
        if (dockable is T typed)
        {
            results.Add(typed);
        }

        if (dockable is IDock dock && dock.VisibleDockables is not null)
        {
            foreach (IDockable child in dock.VisibleDockables)
            {
                CollectDockables(child, results);
            }
        }
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
    public void SaveLayout(IRootDock rootDock, string? filePath = null)
    {
        filePath ??= GetDefaultLayoutPath();
        List<Action> restoreActions = DetachToolViewModels(rootDock);
        restoreActions.AddRange(DetachNonPersistentExtensionTools(rootDock));
        restoreActions.AddRange(DetachDockRuntimeReferences(rootDock));
        try
        {
            string json = s_serializer.Serialize(rootDock);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            if (LogLayoutWarnings)
            {
                _logger.LogWarning("Failed to save dock layout: {Message}", ex.Message);
            }
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
    public IRootDock? LoadLayout(string? filePath = null)
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
                bool hasLeft = FindDockable<ToolDock>(rootDock, "LeftToolDock") is not null;
                bool hasRight = FindDockable<ToolDock>(rootDock, "RightToolDock") is not null;
                bool hasBottom = FindDockable<ToolDock>(rootDock, "BottomToolDock") is not null;
                bool hasDocuments = FindDockable<DocumentDock>(rootDock, "DocumentDock") is not null;
                if (!hasLeft || !hasRight || !hasBottom || !hasDocuments)
                {
                    if (LogLayoutWarnings)
                    {
                        _logger.LogWarning("Dock layout missing required docks. Resetting to defaults.");
                    }
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                    return null;
                }
            }
            return rootDock;
        }
        catch (Exception ex)
        {
            if (LogLayoutWarnings)
            {
                _logger.LogWarning("Failed to load dock layout: {Message}", ex.Message);
            }
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception deleteEx)
            {
                if (LogLayoutWarnings)
                {
                    _logger.LogWarning("Failed to delete invalid dock layout: {Message}", deleteEx.Message);
                }
            }
            return null;
        }
    }

    public static void EnsureLayoutDefaults(IRootDock rootDock)
    {
        const double leftWidth = 0.2;
        const double rightWidth = 0.25;
        const double bottomHeight = 0.25;
        const double documentWidth = 0.75;

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

        ToolDock? bottomDock = FindDockable<ToolDock>(rootDock, "BottomToolDock");
        if (bottomDock is not null)
        {
            bottomDock.VisibleDockables ??= new ObservableCollection<IDockable>();
            if (double.IsNaN(bottomDock.Proportion) || bottomDock.Proportion <= 0)
            {
                bottomDock.Proportion = bottomHeight;
            }
            if (bottomDock.VisibleDockables.Count > 0)
            {
                bottomDock.IsEmpty = false;
            }
            bool hasBreakpoints = bottomDock.VisibleDockables
                .OfType<BreakpointsTool>()
                .Any();
            if (!hasBreakpoints)
            {
                bottomDock.VisibleDockables.Add(new BreakpointsTool());
            }

            bool hasCallStack = bottomDock.VisibleDockables
                .OfType<CallStackTool>()
                .Any();
            if (!hasCallStack)
            {
                bottomDock.VisibleDockables.Add(new CallStackTool());
            }

            bool hasLocals = bottomDock.VisibleDockables
                .OfType<LocalsTool>()
                .Any();
            if (!hasLocals)
            {
                bottomDock.VisibleDockables.Add(new LocalsTool());
            }

            bool hasWatches = bottomDock.VisibleDockables
                .OfType<WatchesTool>()
                .Any();
            if (!hasWatches)
            {
                bottomDock.VisibleDockables.Add(new WatchesTool());
            }

        }

        ToolDock? leftDock = FindDockable<ToolDock>(rootDock, "LeftToolDock");
        if (leftDock is not null)
        {
            leftDock.VisibleDockables ??= new ObservableCollection<IDockable>();
            if (double.IsNaN(leftDock.Proportion) || leftDock.Proportion <= 0)
            {
                leftDock.Proportion = leftWidth;
            }
            if (leftDock.VisibleDockables.Count > 0)
            {
                leftDock.IsEmpty = false;
            }
        }

        ToolDock? rightDock = FindDockable<ToolDock>(rootDock, "RightToolDock");
        if (rightDock is not null)
        {
            rightDock.VisibleDockables ??= new ObservableCollection<IDockable>();
            if (double.IsNaN(rightDock.Proportion) || rightDock.Proportion <= 0)
            {
                rightDock.Proportion = rightWidth;
            }
            if (rightDock.VisibleDockables.Count > 0)
            {
                rightDock.IsEmpty = false;
            }
        }

        DocumentDock? documentDock = FindDockable<DocumentDock>(rootDock, "DocumentDock");
        if (documentDock is not null && (double.IsNaN(documentDock.Proportion) || documentDock.Proportion <= 0))
        {
            documentDock.Proportion = documentWidth;
        }
    }

    private static List<Action> DetachToolViewModels(IRootDock rootDock)
    {
        List<Action> restore = new();

        DetachToolViewModel(
            FindDockable<SolutionExplorerTool>(rootDock, "SolutionExplorer"),
            tool => tool.SolutionExplorerViewModel,
            (tool, vm) => tool.SolutionExplorerViewModel = vm,
            restore);

        DetachToolViewModel(
            FindDockable<OutputTool>(rootDock, "Output"),
            tool => tool.OutputViewModel,
            (tool, vm) => tool.OutputViewModel = vm,
            restore);

        DetachToolViewModel(
            FindDockable<BreakpointsTool>(rootDock, "Breakpoints"),
            tool => tool.BreakpointsViewModel,
            (tool, vm) => tool.BreakpointsViewModel = vm,
            restore);

        DetachToolViewModel(
            FindDockable<CallStackTool>(rootDock, "CallStack"),
            tool => tool.CallStackViewModel,
            (tool, vm) => tool.CallStackViewModel = vm,
            restore);

        DetachToolViewModel(
            FindDockable<LocalsTool>(rootDock, "Locals"),
            tool => tool.LocalsViewModel,
            (tool, vm) => tool.LocalsViewModel = vm,
            restore);

        DetachToolViewModel(
            FindDockable<WatchesTool>(rootDock, "Watches"),
            tool => tool.WatchesViewModel,
            (tool, vm) => tool.WatchesViewModel = vm,
            restore);

        foreach (TerminalTool terminalTool in FindDockables<TerminalTool>(rootDock))
        {
            DetachToolViewModel(
                terminalTool,
                tool => tool.TerminalViewModel,
                (tool, vm) => tool.TerminalViewModel = vm,
                restore);
        }

        foreach (ExtensionTool extensionTool in FindDockables<ExtensionTool>(rootDock))
        {
            DetachToolViewModel(
                extensionTool,
                tool => tool.ExtensionViewModel,
                (tool, vm) => tool.ExtensionViewModel = vm,
                restore);
        }

        DetachToolViewModel(
            FindDockable<ExtensionManagerTool>(rootDock, "ExtensionsManager"),
            tool => tool.ExtensionManagerViewModel,
            (tool, vm) => tool.ExtensionManagerViewModel = vm,
            restore);

        return restore;
    }

    private static IReadOnlyList<Action> DetachNonPersistentExtensionTools(IRootDock rootDock)
    {
        List<Action> restore = new();
        foreach (ExtensionTool extensionTool in FindDockables<ExtensionTool>(rootDock))
        {
            if (extensionTool.PersistDockState || extensionTool.Owner is not IDock owner || owner.VisibleDockables is null)
            {
                continue;
            }

            int index = owner.VisibleDockables.IndexOf(extensionTool);
            if (index < 0)
            {
                continue;
            }

            bool wasActive = ReferenceEquals(owner.ActiveDockable, extensionTool);
            owner.VisibleDockables.RemoveAt(index);
            if (wasActive)
            {
                owner.ActiveDockable = owner.VisibleDockables.FirstOrDefault();
            }

            restore.Add(() =>
            {
                if (owner.VisibleDockables is null || owner.VisibleDockables.Contains(extensionTool))
                {
                    return;
                }

                int insertIndex = Math.Clamp(index, 0, owner.VisibleDockables.Count);
                owner.VisibleDockables.Insert(insertIndex, extensionTool);
                if (wasActive)
                {
                    owner.ActiveDockable = extensionTool;
                }
            });
        }

        return restore;
    }

    private static void PruneTerminalTools(IRootDock rootDock)
    {
        IReadOnlyList<TerminalTool> terminalTools = FindDockables<TerminalTool>(rootDock);
        foreach (TerminalTool tool in terminalTools)
        {
            if (tool.TerminalViewModel is null && tool.Owner is IDock dock && dock.VisibleDockables is not null)
            {
                dock.VisibleDockables.Remove(tool);
            }
        }
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

        dockable.Factory ??= this;
        dockable.DockCapabilityOverrides ??= new DockCapabilityOverrides();

        if (dockable is IDock dock)
        {
            dock.DockCapabilityPolicy ??= new DockCapabilityPolicy();
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
            rootDock.RootDockCapabilityPolicy ??= new DockCapabilityPolicy();
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
