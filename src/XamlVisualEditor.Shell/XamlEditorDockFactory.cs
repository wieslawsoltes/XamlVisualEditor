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
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Shell;

/// <summary>
/// Dock tool for the toolbox panel.
/// </summary>
public sealed class ToolboxTool : Tool
{
    public ToolboxViewModel ToolboxViewModel { get; }

    public ToolboxTool(ToolboxViewModel toolboxViewModel)
    {
        ToolboxViewModel = toolboxViewModel;
        Id = "Toolbox";
        Title = "Toolbox";
    }
}

/// <summary>
/// Dock tool for the property editor panel.
/// </summary>
public sealed class PropertyEditorTool : Tool
{
    public string ToolTitle => "Properties";

    public PropertyEditorTool()
    {
        Id = "Properties";
        Title = "Properties";
    }
}

/// <summary>
/// Dock tool for the visual tree panel.
/// </summary>
public sealed class VisualTreeTool : Tool
{
    public VisualTreeTool()
    {
        Id = "VisualTree";
        Title = "Visual Tree";
    }
}

/// <summary>
/// Dock tool for the logical tree panel.
/// </summary>
public sealed class LogicalTreeTool : Tool
{
    public LogicalTreeTool()
    {
        Id = "LogicalTree";
        Title = "Logical Tree";
    }
}

/// <summary>
/// Dock tool for the output panel.
/// </summary>
public sealed class OutputTool : Tool
{
    public OutputViewModel OutputViewModel { get; }

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
    public CollaborationTool()
    {
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

        // Right tools: Properties, Visual Tree, Logical Tree
        PropertyEditorTool propertyTool = new();
        VisualTreeTool visualTreeTool = new();
        LogicalTreeTool logicalTreeTool = new();

        // Bottom tools: Output, Collaboration
        OutputTool outputTool = new(_mainVm.Output);
        CollaborationTool collabTool = new();

        // Left tool dock
        ToolDock leftToolDock = new()
        {
            Id = "LeftToolDock",
            Title = "Left Tools",
            Proportion = 0.2,
            ActiveDockable = toolboxTool,
            VisibleDockables = CreateList<IDockable>(toolboxTool),
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
    /// Adds a document to the document dock.
    /// </summary>
    public void AddDocument(IRootDock rootDock, DesignerDocumentViewModel documentVm)
    {
        DesignerDocument doc = new(documentVm);

        // Find the document dock
        DocumentDock? docDock = FindDockable<DocumentDock>(rootDock, "DocumentDock");
        if (docDock is not null)
        {
            AddDockable(docDock, doc);
            SetActiveDockable(doc);
            SetFocusedDockable(docDock, doc);
        }
    }

    private static T? FindDockable<T>(IDockable dockable, string id) where T : class, IDockable
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
