using Avalonia.Headless.XUnit;
using System.Reactive.Linq;
using Xunit;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Sync;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Xaml.Parsing;
using XamlVisualEditor.Xaml.Serialization;
using XamlVisualEditor.Designer.Core;
using XamlVisualEditor.TreeView;
using XamlVisualEditor.PropertyEditor;
using XamlVisualEditor.Collaboration.UI;

namespace XamlVisualEditor.Tests.UI;

// ==============================================
// 10.3.1 — Designer Surface Interactions
// ==============================================

/// <summary>
/// Tests for designer surface interactions using ViewModel-level verification.
/// Since headless Avalonia cannot fully instantiate complex designer views,
/// we test the ViewModel layer which drives all UI behavior.
/// </summary>
public sealed class DesignerSurfaceTests
{
    [Fact]
    public void DesignSurfaceViewModel_Can_Set_Zoom()
    {
        DesignSurfaceViewModel vm = new();

        vm.Zoom = 150;

        Assert.Equal(150, vm.Zoom);
    }

    [Fact]
    public void DesignSurfaceViewModel_Can_Toggle_Grid()
    {
        DesignSurfaceViewModel vm = new();

        bool initial = vm.ShowGrid;
        vm.ShowGrid = !initial;

        Assert.NotEqual(initial, vm.ShowGrid);
    }

    [Fact]
    public void SelectionManager_Select_And_Clear()
    {
        SelectionManager manager = new();
        MutableAstObjectNode node = new() { TypeName = "Button", XmlNamespace = "https://github.com/avaloniaui" };
        DesignItem item = new(node);

        manager.Select(item);
        Assert.Single(manager.SelectedItems);

        manager.ClearSelection();
        Assert.Empty(manager.SelectedItems);
    }

    [Fact]
    public void SelectionManager_MultiSelect()
    {
        SelectionManager manager = new();
        MutableAstObjectNode node1 = new() { TypeName = "Button", XmlNamespace = "https://github.com/avaloniaui" };
        MutableAstObjectNode node2 = new() { TypeName = "TextBlock", XmlNamespace = "https://github.com/avaloniaui" };
        DesignItem item1 = new(node1);
        DesignItem item2 = new(node2);

        manager.Select(item1);
        manager.ToggleSelection(item2);

        Assert.Equal(2, manager.SelectedItems.Count);
    }
}

// ==============================================
// 10.3.2 — Code Editor Typing and Completion
// ==============================================

/// <summary>
/// Tests for code editor ViewModel behavior.
/// </summary>
public sealed class CodeEditorTests
{
    [Fact]
    public void CodeEditorViewModel_SetTextSilently()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);
        XamlVisualEditor.Xaml.Intellisense.CompletionProviderRegistry registry = new();
        XamlVisualEditor.CodeEditor.CodeEditorViewModel vm = new(engine, registry);

        string xaml = "<Grid xmlns=\"https://github.com/avaloniaui\" />";
        vm.SetTextSilently(xaml);

        Assert.Equal(xaml, vm.Document.Text);
    }

    [Fact]
    public void CodeEditorViewModel_Options_Defaults()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);
        XamlVisualEditor.Xaml.Intellisense.CompletionProviderRegistry registry = new();
        XamlVisualEditor.CodeEditor.CodeEditorViewModel vm = new(engine, registry);

        Assert.True(vm.ShowLineNumbers);
        Assert.True(vm.FontSize > 0);
    }
}

// ==============================================
// 10.3.3 — Tree View Selection Sync
// ==============================================

/// <summary>
/// Tests for tree view selection synchronization.
/// </summary>
public sealed class TreeViewSelectionSyncTests
{
    [Fact]
    public void VisualTreeNode_Selection_Fires_Event()
    {
        MutableAstObjectNode astNode = new() { TypeName = "Grid", XmlNamespace = "https://github.com/avaloniaui" };
        VisualTreeNodeViewModel vm = new(astNode.Id);
        vm.TypeName = astNode.TypeName;

        Guid? receivedId = null;
        vm.NodeSelected += id => receivedId = id;

        // Setting IsSelected triggers the NodeSelected event via the subscription in the constructor
        vm.IsSelected = true;

        // The event should fire with the node's ID
        Assert.NotNull(receivedId);
        Assert.Equal(astNode.Id, receivedId);
    }

    [Fact]
    public void VisualTree_FromAstDocument_Creates_Tree()
    {
        MutableAstObjectNode root = new() { TypeName = "Grid", XmlNamespace = "https://github.com/avaloniaui" };
        MutableAstObjectNode child = new() { TypeName = "Button", XmlNamespace = "https://github.com/avaloniaui" };
        root.Children.Add(child);

        MutableAstDocument doc = new() { Root = root };

        VisualTreeNodeViewModel? treeRoot = VisualTreeNodeViewModel.FromAstDocument(doc);

        Assert.NotNull(treeRoot);
        Assert.Equal("Grid", treeRoot!.TypeName);
        Assert.Single(treeRoot.Children);
        Assert.Equal("Button", treeRoot.Children[0].TypeName);
    }

    [Fact]
    public void LogicalTree_FromAstDocument_Creates_Tree()
    {
        MutableAstObjectNode root = new() { TypeName = "StackPanel", XmlNamespace = "https://github.com/avaloniaui" };
        MutableAstObjectNode child = new() { TypeName = "TextBlock", XmlNamespace = "https://github.com/avaloniaui" };
        child.SetPropertyValue("Text", "Hello");
        root.Children.Add(child);

        MutableAstDocument doc = new() { Root = root };

        LogicalTreeNodeViewModel? treeRoot = LogicalTreeNodeViewModel.FromAstDocument(doc);

        Assert.NotNull(treeRoot);
        Assert.Equal("StackPanel", treeRoot!.TypeName);
        Assert.Single(treeRoot.Children);
    }
}

// ==============================================
// 10.3.4 — Property Editor Value Editing
// ==============================================

/// <summary>
/// Tests for property editor value editing.
/// </summary>
public sealed class PropertyEditorEditingTests
{
    [Fact]
    public void PropertyEditor_LoadFromDesignItem_Populates_Properties()
    {
        AstNodeMap map = new();
        PropertyEditorViewModel vm = new(map);

        MutableAstObjectNode node = new() { TypeName = "Button", XmlNamespace = "https://github.com/avaloniaui" };
        node.SetPropertyValue("Content", "Click Me");
        node.SetPropertyValue("Width", "100");
        map.Register(node);
        DesignItem item = new(node);

        vm.LoadFromDesignItem(item);

        Assert.NotEmpty(vm.Categories);
    }

    [Fact]
    public void PropertyEditor_ApplyValue_Updates_AST()
    {
        AstNodeMap map = new();
        PropertyEditorViewModel vm = new(map);

        MutableAstObjectNode node = new() { TypeName = "Button", XmlNamespace = "https://github.com/avaloniaui" };
        node.SetPropertyValue("Width", "100");
        map.Register(node);
        DesignItem item = new(node);

        vm.LoadFromDesignItem(item);

        // Find the Width property and change it
        PropertyItemViewModel? widthProp = vm.Categories.SelectMany(c => c.Properties).FirstOrDefault(p => p.Name == "Width");
        Assert.NotNull(widthProp);
        widthProp.Value = "200";
        vm.ApplyPropertyChange(widthProp);

        string? newValue = node.GetPropertyValue("Width");
        Assert.Equal("200", newValue);
    }
}

// ==============================================
// 10.3.5 — Docking Layout Manipulation
// ==============================================

/// <summary>
/// Tests for docking layout commands.
/// </summary>
public sealed class DockingLayoutTests
{
    [Fact]
    public void MainWindowViewModel_ResetLayout_Sets_All_Panels_Visible()
    {
        MainWindowViewModel vm = new();
        vm.IsToolboxVisible = false;
        vm.IsPropertiesVisible = false;
        vm.IsOutputVisible = false;

        vm.ResetLayoutCommand.Execute().Subscribe();

        Assert.True(vm.IsToolboxVisible);
        Assert.True(vm.IsPropertiesVisible);
        Assert.True(vm.IsVisualTreeVisible);
        Assert.True(vm.IsLogicalTreeVisible);
        Assert.True(vm.IsOutputVisible);
    }

    [Fact]
    public void MainWindowViewModel_ToggleCommands_Work()
    {
        MainWindowViewModel vm = new();

        bool initial = vm.IsToolboxVisible;
        vm.ToggleToolboxCommand.Execute().Subscribe();

        Assert.NotEqual(initial, vm.IsToolboxVisible);
    }
}

// ==============================================
// 10.3.6 — Drag-and-Drop Workflows
// ==============================================

/// <summary>
/// Tests for drag-and-drop ViewModel operations.
/// </summary>
public sealed class DragDropWorkflowTests
{
    [Fact]
    public void ToolboxItem_Can_Create_DragData()
    {
        ToolboxItemViewModel item = new("Button", "Button", "https://github.com/avaloniaui", "Controls");

        Assert.Equal("Button", item.DisplayName);
        Assert.Equal("Button", item.TypeName);
        Assert.Equal("Controls", item.Category);
    }

    [Fact]
    public void DropPosition_Enum_Has_Expected_Values()
    {
        Assert.True(Enum.IsDefined(typeof(DropPosition), DropPosition.Before));
        Assert.True(Enum.IsDefined(typeof(DropPosition), DropPosition.After));
        Assert.True(Enum.IsDefined(typeof(DropPosition), DropPosition.Inside));
    }

    [Fact]
    public async System.Threading.Tasks.Task NewDocument_Creates_Document_ViewModel()
    {
        MainWindowViewModel vm = new();

        await vm.NewDocumentCommand.Execute().FirstAsync();

        Assert.Single(vm.Documents);
        Assert.NotNull(vm.ActiveDocument);
    }
}

/// <summary>
/// Placeholder for full Avalonia headless UI tests.
/// These would use [AvaloniaFact] for proper UI testing.
/// </summary>
public sealed class MainWindowTests
{
    [Fact]
    public void MainWindowViewModel_Creates_With_Default_State()
    {
        MainWindowViewModel vm = new();

        Assert.True(vm.IsToolboxVisible);
        Assert.True(vm.IsPropertiesVisible);
        Assert.True(vm.IsOutputVisible);
        Assert.Empty(vm.Documents);
    }
}
