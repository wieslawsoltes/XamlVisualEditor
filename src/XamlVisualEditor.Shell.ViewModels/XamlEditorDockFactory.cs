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
using ReactiveUI.SourceGenerators;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Shell;

/// <summary>
/// Dock tool for the solution explorer panel.
/// </summary>
public sealed partial class SolutionExplorerTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public partial SolutionExplorerViewModel? SolutionExplorerViewModel { get; set; }

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
public sealed partial class OutputTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public partial OutputViewModel? OutputViewModel { get; set; }

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
public sealed partial class TerminalTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public partial TerminalViewModel? TerminalViewModel { get; set; }

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
public sealed partial class BreakpointsTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public partial BreakpointsViewModel? BreakpointsViewModel { get; set; }

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
public sealed partial class CallStackTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public partial CallStackViewModel? CallStackViewModel { get; set; }

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
public sealed partial class LocalsTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public partial LocalsViewModel? LocalsViewModel { get; set; }

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
public sealed partial class WatchesTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public partial WatchesViewModel? WatchesViewModel { get; set; }

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
public sealed partial class ExtensionManagerTool : Tool
{
    [IgnoreDataMember]
    [Reactive]
    public partial ExtensionManagerViewModel? ExtensionManagerViewModel { get; set; }

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
public sealed partial class ExtensionTool : Tool
{
    public const string IdPrefix = "Extension:";

    [IgnoreDataMember]
    [Reactive]
    public partial ExtensionViewModel? ExtensionViewModel { get; set; }

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
public sealed partial class DesignerDocument : Document
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
public sealed partial class TextDocument : Document
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
public sealed partial class InfiniteCanvasDocument : Document
{
    [IgnoreDataMember]
    [Reactive]
    public partial InfiniteCanvasViewModel? CanvasViewModel { get; set; }

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
public sealed partial class XamlEditorDockFactory : Factory
{
    private static readonly bool LogLayoutWarnings = true;
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
            ActiveDockable = null,
            VisibleDockables = CreateList<IDockable>(
                outputTool,
                breakpointsTool,
                callStackTool,
                localsTool,
                watchesTool,
                extensionManagerTool),
            Alignment = Alignment.Bottom,
            AutoHide = true,
            IsExpanded = false
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

    // Debug traces for the dock state flow: which code path activates, pins, or
    // restores which dockable. Layout regressions (a collapsed dock expanding on
    // startup, a tool reappearing) are otherwise invisible in the log.
    public override void SetActiveDockable(IDockable dockable)
    {
        _logger.LogDebug(
            "Dock SetActiveDockable: {Id} (owner {OwnerId})",
            dockable.Id,
            (dockable.Owner as IDockable)?.Id);
        base.SetActiveDockable(dockable);
    }

    public override void SetFocusedDockable(IDock dock, IDockable? dockable)
    {
        _logger.LogDebug("Dock SetFocusedDockable: {Id} in {DockId}", dockable?.Id, dock.Id);
        base.SetFocusedDockable(dock, dockable);
    }

    public override void PinDockable(IDockable dockable)
    {
        _logger.LogDebug("Dock PinDockable: {Id}", dockable.Id);
        base.PinDockable(dockable);
    }

    public override void RestoreDockable(IDockable dockable)
    {
        _logger.LogDebug("Dock RestoreDockable: {Id}", dockable.Id);
        base.RestoreDockable(dockable);
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

    /// <summary>
    /// Collapses the bottom tool dock without changing its tools or saved proportion.
    /// </summary>
    public void CollapseBottomToolDock(IRootDock rootDock)
    {
        IReadOnlyList<ToolDock> bottomDocks = FindDockables<ToolDock>(rootDock)
            .Where(dock => string.Equals(dock.Id, "BottomToolDock", StringComparison.Ordinal))
            .ToList();
        foreach (ToolDock bottomDock in bottomDocks)
        {
            List<IDockable> tools = bottomDock.VisibleDockables?.ToList()
                ?? new List<IDockable>();
            foreach (IDockable tool in tools)
            {
                PinDockable(tool);
            }

            if (double.IsFinite(bottomDock.Proportion) && bottomDock.Proportion > 0)
            {
                bottomDock.CollapsedProportion = bottomDock.Proportion;
            }

            bottomDock.Proportion = 0;
            bottomDock.ActiveDockable = null;
            bottomDock.IsActive = false;
            bottomDock.IsExpanded = false;
        }

        if (rootDock.PinnedDock is ToolDock pinnedDock)
        {
            pinnedDock.ActiveDockable = null;
            pinnedDock.IsActive = false;
            pinnedDock.IsExpanded = false;
        }
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

            bool isBottomToolDock = string.Equals(toolDock.Id, "BottomToolDock", StringComparison.Ordinal);

            // A collapsed bottom dock stays collapsed: extension views registering
            // after startup would otherwise activate their tool and expand the dock
            // over the restored layout on every run. Their tools go to the pin strip
            // instead, matching the tools pinned at startup.
            bool bottomCollapsed = isBottomToolDock && !toolDock.IsExpanded;
            if (bottomCollapsed)
            {
                PinDockable(tool);
                return tool;
            }

            if (viewModel.ActivateByDefault || (!isBottomToolDock && toolDock.ActiveDockable is null))
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

        if (dockable is IRootDock rootDock)
        {
            foreach (IDockable child in EnumerateDetachedDockables(rootDock))
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

        if (dockable is IRootDock rootDock)
        {
            foreach (IDockable child in EnumerateDetachedDockables(rootDock))
            {
                CollectDockables(child, results);
            }
        }
    }

    /// <summary>
    /// Enumerates the dockables of a root dock that live outside the visible tree:
    /// hidden dockables and the four pinned dockable lists. Without them a pinned tool
    /// is invisible to the search helpers and gets recreated on every start.
    /// </summary>
    private static IEnumerable<IDockable> EnumerateDetachedDockables(IRootDock rootDock)
    {
        IEnumerable<IList<IDockable>?> lists = new[]
        {
            rootDock.HiddenDockables,
            rootDock.LeftPinnedDockables,
            rootDock.RightPinnedDockables,
            rootDock.TopPinnedDockables,
            rootDock.BottomPinnedDockables
        };

        foreach (IList<IDockable>? list in lists)
        {
            if (list is null)
            {
                continue;
            }

            foreach (IDockable dockable in list)
            {
                yield return dockable;
            }
        }
    }

    /// <summary>
    /// Overrides the directory holding the persisted layout state. Tests MUST set
    /// this to an isolated directory: they build real shell view models, and
    /// without the override they read and overwrite the user's layout files.
    /// </summary>
    public static string? SettingsRootOverride { get; set; }

    /// <summary>
    /// Gets the default layout file path.
    /// </summary>
    public static string GetDefaultLayoutPath()
    {
        string dir = SettingsRootOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XamlVisualEditor");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "dock-layout.json");
    }

    /// <summary>
    /// Gets the path of the persisted closed-tools list.
    /// </summary>
    public static string GetClosedToolsPath()
    {
        return Path.Combine(
            Path.GetDirectoryName(GetDefaultLayoutPath())!,
            "dock-closed-tools.json");
    }

    // Tools the user closed, persisted next to the layout: the shell recreates
    // missing tools on every start (default debug tools, extension panels), so
    // without this list a closed tool reappears on the next run.
    private HashSet<string>? _closedTools;

    private HashSet<string> ClosedTools => _closedTools ??= LoadClosedTools();

    /// <summary>Returns whether the user closed the tool with the given id.</summary>
    public bool IsToolClosed(string id)
    {
        return ClosedTools.Contains(id);
    }

    /// <summary>Records that the user closed the tool with the given id.</summary>
    public void MarkToolClosed(string id)
    {
        if (ClosedTools.Add(id))
        {
            SaveClosedTools();
        }
    }

    /// <summary>Removes the closed mark, so the tool may be recreated again.</summary>
    public void MarkToolReopened(string id)
    {
        if (ClosedTools.Remove(id))
        {
            SaveClosedTools();
        }
    }

    /// <summary>Clears all closed marks (layout reset).</summary>
    public void ClearClosedTools()
    {
        if (ClosedTools.Count > 0)
        {
            ClosedTools.Clear();
            SaveClosedTools();
        }
    }

    private HashSet<string> LoadClosedTools()
    {
        try
        {
            string path = GetClosedToolsPath();
            if (File.Exists(path))
            {
                string[]? ids = System.Text.Json.JsonSerializer.Deserialize<string[]>(File.ReadAllText(path));
                if (ids is not null)
                {
                    return new HashSet<string>(ids, StringComparer.Ordinal);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to load closed-tools list: {Message}", ex.Message);
        }

        return new HashSet<string>(StringComparer.Ordinal);
    }

    private void SaveClosedTools()
    {
        try
        {
            File.WriteAllText(
                GetClosedToolsPath(),
                System.Text.Json.JsonSerializer.Serialize(ClosedTools.OrderBy(id => id, StringComparer.Ordinal)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to save closed-tools list: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Saves the current dock layout to a JSON file.
    /// </summary>
    public void SaveLayout(IRootDock rootDock, string? filePath = null)
    {
        filePath ??= GetDefaultLayoutPath();
        List<Action> restoreActions = DetachToolViewModels(rootDock);
        restoreActions.AddRange(DetachNonPersistentExtensionTools(rootDock));
        restoreActions.AddRange(DetachTransientDocuments(rootDock));
        restoreActions.AddRange(DetachPinnedDock(rootDock));
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
            // The serializer registers polymorphism ($type) on the dock interfaces only,
            // so the layout must be deserialized via IRootDock, not the concrete RootDock.
            IRootDock? rootDock = s_serializer.Deserialize<IRootDock>(json);
            if (rootDock is not null)
            {
                EnsureLayoutDefaults(rootDock);
                ReconnectDockableReferences(rootDock);
                PruneDuplicatePinnedDockables(rootDock);
                RemoveClosedTools(rootDock);
                EnsureDebugTools(rootDock);
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
            // Repair only a missing proportion: zero is the legitimate collapsed
            // state, and resetting it reopened the bottom dock on every start.
            if (double.IsNaN(bottomDock.Proportion))
            {
                bottomDock.Proportion = bottomHeight;
            }
            if (bottomDock.VisibleDockables.Count > 0)
            {
                bottomDock.IsEmpty = false;
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

    /// <summary>
    /// Removes open designer and text documents from the layout for the duration of a save.
    /// Their view models represent open files that are not rehydrated on load, and the
    /// document types have no parameterless constructor, so persisted instances would make
    /// the saved layout impossible to deserialize.
    /// </summary>
    private static IReadOnlyList<Action> DetachTransientDocuments(IRootDock rootDock)
    {
        List<Action> restore = new();

        List<IDockable> transientDocuments = new();
        transientDocuments.AddRange(FindDockables<DesignerDocument>(rootDock));
        transientDocuments.AddRange(FindDockables<TextDocument>(rootDock));
        if (transientDocuments.Count == 0)
        {
            return restore;
        }

        HashSet<IDockable> transientSet = new(transientDocuments);

        foreach (IDock dock in FindDockables<IDock>(rootDock))
        {
            IDock target = dock;

            if (target.ActiveDockable is { } active && transientSet.Contains(active))
            {
                target.ActiveDockable = target.VisibleDockables?.FirstOrDefault(d => !transientSet.Contains(d));
                restore.Add(() => target.ActiveDockable = active);
            }

            if (target.DefaultDockable is { } defaultDockable && transientSet.Contains(defaultDockable))
            {
                target.DefaultDockable = null;
                restore.Add(() => target.DefaultDockable = defaultDockable);
            }

            if (target.FocusedDockable is { } focused && transientSet.Contains(focused))
            {
                target.FocusedDockable = null;
                restore.Add(() => target.FocusedDockable = focused);
            }
        }

        foreach (IDockable document in transientDocuments)
        {
            if (document.Owner is not IDock owner || owner.VisibleDockables is null)
            {
                continue;
            }

            int index = owner.VisibleDockables.IndexOf(document);
            if (index < 0)
            {
                continue;
            }

            owner.VisibleDockables.RemoveAt(index);
            int restoreIndex = index;
            restore.Add(() =>
            {
                if (owner.VisibleDockables is null || owner.VisibleDockables.Contains(document))
                {
                    return;
                }

                int insertIndex = Math.Clamp(restoreIndex, 0, owner.VisibleDockables.Count);
                owner.VisibleDockables.Insert(insertIndex, document);
            });
        }

        return restore;
    }

    /// <summary>
    /// Removes the pinned dock from the layout for the duration of a save. The serializer
    /// has no polymorphic registration for IToolDock, so a persisted pinned dock cannot be
    /// deserialized; EnsureLayoutDefaults recreates it on load.
    /// </summary>
    private static IReadOnlyList<Action> DetachPinnedDock(IRootDock rootDock)
    {
        List<Action> restore = new();

        if (rootDock.PinnedDock is { } pinnedDock)
        {
            rootDock.PinnedDock = null;
            restore.Add(() => rootDock.PinnedDock = pinnedDock);
        }

        return restore;
    }

    private static void PruneTerminalTools(IRootDock rootDock)
    {
        IReadOnlyList<TerminalTool> terminalTools = FindDockables<TerminalTool>(rootDock);
        foreach (TerminalTool tool in terminalTools)
        {
            if (tool.TerminalViewModel is not null)
            {
                continue;
            }

            if (tool.Owner is IDock dock && dock.VisibleDockables is not null)
            {
                dock.VisibleDockables.Remove(tool);
            }

            rootDock.HiddenDockables?.Remove(tool);
            rootDock.LeftPinnedDockables?.Remove(tool);
            rootDock.RightPinnedDockables?.Remove(tool);
            rootDock.TopPinnedDockables?.Remove(tool);
            rootDock.BottomPinnedDockables?.Remove(tool);
        }
    }

    /// <summary>
    /// Removes tools the user closed from a loaded layout. Layouts saved before
    /// closing reached the model (or by other means) may still carry them; the
    /// closed mark is authoritative until the tool is reopened.
    /// </summary>
    private void RemoveClosedTools(IRootDock rootDock)
    {
        if (ClosedTools.Count == 0)
        {
            return;
        }

        foreach (IDock dock in FindDockables<IDock>(rootDock))
        {
            if (dock.VisibleDockables is null)
            {
                continue;
            }

            for (int i = dock.VisibleDockables.Count - 1; i >= 0; i--)
            {
                if (dock.VisibleDockables[i] is not Tool tool
                    || tool is TerminalTool
                    || !IsToolClosed(tool.Id))
                {
                    continue;
                }

                if (ReferenceEquals(dock.ActiveDockable, tool))
                {
                    dock.ActiveDockable = null;
                }

                if (ReferenceEquals(dock.DefaultDockable, tool))
                {
                    dock.DefaultDockable = null;
                }

                if (ReferenceEquals(dock.FocusedDockable, tool))
                {
                    dock.FocusedDockable = null;
                }

                dock.VisibleDockables.RemoveAt(i);
            }
        }

        RemoveClosedTools(rootDock.HiddenDockables);
        RemoveClosedTools(rootDock.LeftPinnedDockables);
        RemoveClosedTools(rootDock.RightPinnedDockables);
        RemoveClosedTools(rootDock.TopPinnedDockables);
        RemoveClosedTools(rootDock.BottomPinnedDockables);
    }

    private void RemoveClosedTools(IList<IDockable>? dockables)
    {
        if (dockables is null)
        {
            return;
        }

        for (int i = dockables.Count - 1; i >= 0; i--)
        {
            if (dockables[i] is Tool tool && tool is not TerminalTool && IsToolClosed(tool.Id))
            {
                dockables.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Recreates the default debug tools missing from a loaded layout - except the
    /// ones the user closed, which stay closed until reopened via the view menu.
    /// </summary>
    private void EnsureDebugTools(IRootDock rootDock)
    {
        ToolDock? bottomDock = FindDockable<ToolDock>(rootDock, "BottomToolDock");
        if (bottomDock?.VisibleDockables is null)
        {
            return;
        }

        EnsureDebugTool<BreakpointsTool>(rootDock, bottomDock, "Breakpoints", () => new BreakpointsTool());
        EnsureDebugTool<CallStackTool>(rootDock, bottomDock, "CallStack", () => new CallStackTool());
        EnsureDebugTool<LocalsTool>(rootDock, bottomDock, "Locals", () => new LocalsTool());
        EnsureDebugTool<WatchesTool>(rootDock, bottomDock, "Watches", () => new WatchesTool());
    }

    private void EnsureDebugTool<T>(IRootDock rootDock, ToolDock bottomDock, string id, Func<T> create)
        where T : Tool
    {
        if (IsToolClosed(id) || FindDockables<T>(rootDock).Count > 0)
        {
            return;
        }

        bottomDock.VisibleDockables!.Add(create());
    }

    /// <summary>
    /// Recreates a closed built-in tool on explicit user request (view menu) and
    /// removes its closed mark.
    /// </summary>
    public Tool? ReopenBuiltInTool(IRootDock rootDock, string id)
    {
        MarkToolReopened(id);

        if (FindDockable<Tool>(rootDock, id) is { } existing)
        {
            return existing;
        }

        ToolDock? bottomDock = FindDockable<ToolDock>(rootDock, "BottomToolDock");
        if (bottomDock?.VisibleDockables is null)
        {
            return null;
        }

        Tool? tool = id switch
        {
            "Breakpoints" => new BreakpointsTool(),
            "CallStack" => new CallStackTool(),
            "Locals" => new LocalsTool(),
            "Watches" => new WatchesTool(),
            _ => null
        };

        if (tool is null)
        {
            return null;
        }

        bottomDock.VisibleDockables.Add(tool);
        SetOwnerRecursive(tool, bottomDock);
        bottomDock.IsEmpty = false;
        return tool;
    }

    /// <summary>
    /// Repoints the Active-/Default-/FocusedDockable of every dock in a loaded layout
    /// to the instance with the same id in its visible dockables. The serializer
    /// materializes such property references as duplicate subtrees instead of
    /// references into the visible tree; without repointing, the shell renders the
    /// unwired duplicate while all runtime wiring happens on the visible tree.
    /// </summary>
    private static void ReconnectDockableReferences(IRootDock rootDock)
    {
        foreach (IDock dock in FindDockables<IDock>(rootDock))
        {
            dock.ActiveDockable = ResolveDockable(dock.ActiveDockable, dock.VisibleDockables);
            dock.DefaultDockable = ResolveDockable(dock.DefaultDockable, dock.VisibleDockables);
            dock.FocusedDockable = ResolveDockable(dock.FocusedDockable, dock.VisibleDockables);
        }

        if (rootDock.ActiveDockable is null && rootDock.VisibleDockables is not null)
        {
            rootDock.ActiveDockable = rootDock.VisibleDockables.FirstOrDefault();
        }

        rootDock.DefaultDockable ??= rootDock.ActiveDockable;
    }

    private static IDockable? ResolveDockable(IDockable? candidate, IList<IDockable>? visibleDockables)
    {
        if (candidate is null || visibleDockables is null)
        {
            return null;
        }

        if (visibleDockables.Contains(candidate))
        {
            return candidate;
        }

        Type candidateType = candidate.GetType();
        return visibleDockables.FirstOrDefault(dockable =>
            dockable.GetType() == candidateType
            && string.Equals(dockable.Id, candidate.Id, StringComparison.Ordinal));
    }

    /// <summary>
    /// Removes pinned or hidden dockables that duplicate a dockable of the same type
    /// and id elsewhere in the loaded layout. Layouts saved before the search helpers
    /// covered the pinned lists accumulated such duplicates on every start.
    /// </summary>
    private static void PruneDuplicatePinnedDockables(IRootDock rootDock)
    {
        HashSet<(Type Type, string Id)> seen = new();
        CollectVisibleKeys(rootDock, seen);

        PruneDuplicateKeys(rootDock.HiddenDockables, seen);
        PruneDuplicateKeys(rootDock.LeftPinnedDockables, seen);
        PruneDuplicateKeys(rootDock.RightPinnedDockables, seen);
        PruneDuplicateKeys(rootDock.TopPinnedDockables, seen);
        PruneDuplicateKeys(rootDock.BottomPinnedDockables, seen);
    }

    private static void CollectVisibleKeys(IDockable dockable, HashSet<(Type Type, string Id)> keys)
    {
        keys.Add((dockable.GetType(), dockable.Id ?? string.Empty));

        if (dockable is IDock dock && dock.VisibleDockables is not null)
        {
            foreach (IDockable child in dock.VisibleDockables)
            {
                CollectVisibleKeys(child, keys);
            }
        }
    }

    private static void PruneDuplicateKeys(IList<IDockable>? dockables, HashSet<(Type Type, string Id)> seen)
    {
        if (dockables is null)
        {
            return;
        }

        int index = 0;
        while (index < dockables.Count)
        {
            IDockable dockable = dockables[index];
            if (!seen.Add((dockable.GetType(), dockable.Id ?? string.Empty)))
            {
                dockables.RemoveAt(index);
                continue;
            }

            index++;
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
