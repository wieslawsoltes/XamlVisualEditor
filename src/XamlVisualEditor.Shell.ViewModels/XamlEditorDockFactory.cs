using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using Dock.Serializer.SystemTextJson;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Collaboration.UI;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Shell;

/// <summary>
/// Dock tool for the toolbox panel.
/// </summary>
public sealed class ToolboxTool : Tool
{
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
    public MainWindowViewModel? MainViewModel { get; set; }

    public PropertyEditorTool()
    {
        Id = "Properties";
        Title = "Properties";
    }

    public PropertyEditorTool(MainWindowViewModel mainViewModel)
    {
        MainViewModel = mainViewModel;
        Id = "Properties";
        Title = "Properties";
    }
}

/// <summary>
/// Dock tool for the visual tree panel.
/// </summary>
public sealed class VisualTreeTool : Tool
{
    public MainWindowViewModel? MainViewModel { get; set; }

    public VisualTreeTool()
    {
        Id = "VisualTree";
        Title = "Visual Tree";
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
    public MainWindowViewModel? MainViewModel { get; set; }

    public LogicalTreeTool()
    {
        Id = "LogicalTree";
        Title = "Logical Tree";
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
/// Dock document for a XAML designer document.
/// </summary>
public sealed class DesignerDocument : Document
{
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
/// Factory that creates the default VS/Blend-style docking layout.
/// </summary>
public sealed class XamlEditorDockFactory : Factory
{
    private readonly MainWindowViewModel _mainVm;

    public XamlEditorDockFactory(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;
    }

    /// <summary>
    /// Creates the default layout with docking panels arranged in a VS/Blend style.
    /// </summary>
    public IRootDock CreateDefaultLayout()
    {
        // Left tools: Toolbox
        ToolboxTool toolboxTool = new(_mainVm.Toolbox);
        SolutionExplorerTool solutionExplorerTool = new(_mainVm.SolutionExplorer);

        // Right tools: Properties, Visual Tree, Logical Tree
        PropertyEditorTool propertyTool = new(_mainVm);
        VisualTreeTool visualTreeTool = new(_mainVm);
        LogicalTreeTool logicalTreeTool = new(_mainVm);

        // Bottom tools: Output, Collaboration
        OutputTool outputTool = new(_mainVm.Output);
        CollaborationTool collabTool = new(_mainVm.Collaboration);

        // Left tool dock
        ToolDock leftToolDock = new()
        {
            Id = "LeftToolDock",
            Title = "Left Tools",
            Proportion = 0.2,
            ActiveDockable = toolboxTool,
            VisibleDockables = CreateList<IDockable>(toolboxTool, solutionExplorerTool),
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
            VisibleDockables = CreateList<IDockable>(outputTool, collabTool),
            Alignment = Alignment.Bottom
        };

        // Document dock (center)
        DocumentDock documentDock = new()
        {
            Id = "DocumentDock",
            Title = "Documents",
            IsCollapsable = false,
            VisibleDockables = CreateList<IDockable>(),
            CanCreateDocument = false
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
            propertyTool.MainViewModel = _mainVm;
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

        CollaborationTool? collabTool = FindDockable<CollaborationTool>(rootDock, "Collaboration");
        if (collabTool is not null)
        {
            collabTool.CollaborationViewModel = _mainVm.Collaboration;
        }
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
        try
        {
            DockSerializer serializer = new(typeof(ObservableCollection<>));
            string json = serializer.Serialize(rootDock);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to save dock layout: {ex.Message}");
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
            DockSerializer serializer = new(typeof(ObservableCollection<>));
            return serializer.Deserialize<RootDock>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to load dock layout: {ex.Message}");
            return null;
        }
    }
}
