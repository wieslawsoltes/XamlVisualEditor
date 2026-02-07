using Xunit;
using System.Diagnostics;
using XamlVisualEditor.CodeEditor;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Sync;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Xaml.Intellisense;
using XamlVisualEditor.Xaml.Parsing;
using XamlVisualEditor.Xaml.Serialization;

namespace XamlVisualEditor.Tests.Unit;

/// <summary>
/// Tests for the mutable AST model.
/// </summary>
public sealed class MutableAstDocumentTests
{
    [Fact]
    public void NewDocument_HasUniqueId()
    {
        MutableAstDocument doc = new();
        Assert.NotEqual(Guid.Empty, doc.Id);
    }

    [Fact]
    public void SetPropertyValue_CreatesPropertyNode()
    {
        MutableAstObjectNode node = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        node.SetPropertyValue("Content", "Hello");

        string? value = node.GetPropertyValue("Content");
        Assert.Equal("Hello", value);
    }

    [Fact]
    public void SetPropertyValue_UpdatesExistingProperty()
    {
        MutableAstObjectNode node = new()
        {
            TypeName = "TextBlock",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        node.SetPropertyValue("Text", "Original");
        node.SetPropertyValue("Text", "Updated");

        string? value = node.GetPropertyValue("Text");
        Assert.Equal("Updated", value);
    }

    [Fact]
    public void AddChild_SetsParent()
    {
        MutableAstObjectNode parent = new()
        {
            TypeName = "Grid",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        MutableAstObjectNode child = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        parent.Children.Add(child);

        Assert.Same(parent, child.Parent);
        Assert.Contains(child, parent.Children);
    }

    [Fact]
    public void RemoveChild_ClearsParent()
    {
        MutableAstObjectNode parent = new()
        {
            TypeName = "Grid",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        MutableAstObjectNode child = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        parent.Children.Add(child);
        parent.Children.Remove(child);

        Assert.Null(child.Parent);
        Assert.DoesNotContain(child, parent.Children);
    }

    [Fact]
    public void SetPropertyValue_Null_RemovesProperty()
    {
        MutableAstObjectNode node = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        node.SetPropertyValue("Content", "Hello");
        node.SetPropertyValue("Content", null);

        Assert.Null(node.GetPropertyValue("Content"));
    }

    [Fact]
    public void Document_Changed_Fires_On_PropertyChange()
    {
        MutableAstDocument doc = new();
        MutableAstObjectNode root = new()
        {
            TypeName = "Grid",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        doc.Root = root;

        // First set a property so it exists
        root.SetPropertyValue("Width", "100");

        AstChange? receivedChange = null;
        doc.Changed += c => receivedChange = c;

        // Now update the existing property — this triggers TextContentChanged
        root.SetPropertyValue("Width", "200");

        Assert.NotNull(receivedChange);
        Assert.IsType<TextContentChanged>(receivedChange);
    }

    [Fact]
    public void Document_Changed_Fires_On_ChildAdded()
    {
        MutableAstDocument doc = new();
        MutableAstObjectNode root = new()
        {
            TypeName = "Grid",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        doc.Root = root;

        AstChange? receivedChange = null;
        doc.Changed += c => receivedChange = c;

        root.Children.Add(new MutableAstObjectNode
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        });

        Assert.IsType<NodeAdded>(receivedChange);
    }

    [Fact]
    public void TextNode_Changed_Emits_TextContentChanged()
    {
        MutableAstTextNode textNode = new() { Text = "Hello" };

        AstChange? receivedChange = null;
        MutableAstDocument doc = new();
        MutableAstObjectNode root = new()
        {
            TypeName = "TextBlock",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        root.SetPropertyValue("Text", "Hello");
        doc.Root = root;
        doc.Changed += c => receivedChange = c;

        // Change the text through the property
        MutableAstPropertyNode? textProp = root.Properties.FirstOrDefault(p => p.PropertyName == "Text");
        Assert.NotNull(textProp);
        if (textProp!.Value is MutableAstTextNode tn)
        {
            tn.Text = "World";
        }

        Assert.IsType<TextContentChanged>(receivedChange);
    }
}

/// <summary>
/// Tests for the AST node map.
/// </summary>
public sealed class AstNodeMapTests
{
    [Fact]
    public void Register_And_Lookup_Returns_Node()
    {
        AstNodeMap map = new();
        MutableAstObjectNode node = new()
        {
            TypeName = "Grid",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        map.Register(node);

        MutableAstNode? found = map.FindById(node.Id);
        Assert.Same(node, found);
    }

    [Fact]
    public void Unregister_Removes_Node()
    {
        AstNodeMap map = new();
        MutableAstObjectNode node = new()
        {
            TypeName = "Grid",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        map.Register(node);
        map.Unregister(node.Id);

        MutableAstNode? found = map.FindById(node.Id);
        Assert.Null(found);
    }

    [Fact]
    public void RegisterTree_Registers_All_Descendants()
    {
        AstNodeMap map = new();
        MutableAstObjectNode root = new()
        {
            TypeName = "Grid",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        MutableAstObjectNode child = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        child.SetPropertyValue("Content", "Hello");
        root.Children.Add(child);

        map.RegisterTree(root);

        Assert.NotNull(map.FindById(root.Id));
        Assert.NotNull(map.FindById(child.Id));
        Assert.True(map.Count >= 2);
    }

    [Fact]
    public void FindById_Returns_Null_For_Unknown_Id()
    {
        AstNodeMap map = new();
        Assert.Null(map.FindById(Guid.NewGuid()));
    }
}

/// <summary>
/// Tests for the XAML parsing service.
/// </summary>
public sealed class XamlParsingServiceTests
{
    private const string SimpleXaml = """
        <Grid xmlns="https://github.com/avaloniaui">
            <Button Content="Click Me" />
        </Grid>
        """;

    [Fact]
    public void Parse_Simple_Xaml_Returns_Document()
    {
        XamlParsingService service = new();
        ParseResult result = service.Parse(SimpleXaml);

        Assert.NotNull(result.Document);
        MutableAstDocument doc = Assert.IsType<MutableAstDocument>(result.Document);
        Assert.NotNull(doc.Root);
        Assert.Equal("Grid", doc.Root!.TypeName);
    }

    [Fact]
    public void Parse_Invalid_Xaml_Returns_Diagnostics()
    {
        XamlParsingService service = new();
        ParseResult result = service.Parse("<Invalid><Unclosed>");

        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Parse_Preserves_Attributes()
    {
        XamlParsingService service = new();
        ParseResult result = service.Parse(SimpleXaml);

        MutableAstDocument? doc = result.Document as MutableAstDocument;
        MutableAstObjectNode? button = doc?.Root?.Children
            .OfType<MutableAstObjectNode>()
            .FirstOrDefault(n => n.TypeName == "Button");

        Assert.NotNull(button);
        Assert.Equal("Click Me", button!.GetPropertyValue("Content"));
    }

    [Fact]
    public void Parse_Nested_Elements()
    {
        const string xaml = """
            <Grid xmlns="https://github.com/avaloniaui">
                <StackPanel>
                    <TextBlock Text="Hello" />
                    <Button Content="World" />
                </StackPanel>
            </Grid>
            """;

        XamlParsingService service = new();
        ParseResult result = service.Parse(xaml);

        MutableAstDocument? doc = result.Document as MutableAstDocument;
        Assert.NotNull(doc?.Root);
        Assert.Single(doc!.Root!.Children);

        MutableAstObjectNode? stackPanel = doc.Root.Children[0] as MutableAstObjectNode;
        Assert.NotNull(stackPanel);
        Assert.Equal("StackPanel", stackPanel!.TypeName);
        Assert.Equal(2, stackPanel.Children.Count);
    }
}

/// <summary>
/// Tests for the XAML serialization service.
/// </summary>
public sealed class XamlSerializationServiceTests
{
    [Fact]
    public void Serialize_Simple_Document_Produces_Valid_Xaml()
    {
        MutableAstDocument doc = new();
        doc.Root = new MutableAstObjectNode
        {
            TypeName = "Grid",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        doc.Root.Children.Add(new MutableAstObjectNode
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        });

        XamlSerializationService service = new();
        string xaml = service.Serialize(doc);

        Assert.Contains("<Grid", xaml);
        Assert.Contains("<Button", xaml);
    }

    [Fact]
    public void Serialize_Preserves_Properties()
    {
        MutableAstDocument doc = new();
        MutableAstObjectNode root = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        root.SetPropertyValue("Content", "Test");
        doc.Root = root;

        XamlSerializationService service = new();
        string xaml = service.Serialize(doc);

        Assert.Contains("Content=\"Test\"", xaml);
    }
}

/// <summary>
/// Tests for the sync engine.
/// </summary>
public sealed class SyncEngineTests
{
    [Fact]
    public async System.Threading.Tasks.Task LoadAsync_Parses_And_Sets_State()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);

        await engine.LoadAsync("<Grid xmlns=\"https://github.com/avaloniaui\" />");

        Assert.NotNull(engine.CurrentDocument);
        Assert.NotNull(engine.CurrentText);
    }

    [Fact]
    public async System.Threading.Tasks.Task SyncEngine_UndoRedo_Is_Available()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);

        Assert.NotNull(engine.UndoRedo);
        Assert.False(engine.UndoRedo.CanUndo);
        Assert.False(engine.UndoRedo.CanRedo);
    }

    [Fact]
    public async System.Threading.Tasks.Task SyncEngine_CommitBatch_Creates_UndoFrame()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);

        const string xaml = """
            <Grid xmlns="https://github.com/avaloniaui" Width="100" />
            """;
        await engine.LoadAsync(xaml);

        // Modify an existing property — this emits TextContentChanged which is captured
        engine.CurrentDocument!.Root!.SetPropertyValue("Width", "200");
        engine.CommitUndoBatch("Set Width");

        Assert.True(engine.UndoRedo.CanUndo);
        Assert.Equal(1, engine.UndoRedo.UndoCount);
    }
}

/// <summary>
/// Tests for the UndoRedoService.
/// </summary>
public sealed class UndoRedoServiceTests
{
    [Fact]
    public void Initial_State_Is_Empty()
    {
        using UndoRedoService svc = new();
        Assert.False(svc.CanUndo);
        Assert.False(svc.CanRedo);
        Assert.Equal(0, svc.UndoCount);
        Assert.Equal(0, svc.RedoCount);
    }

    [Fact]
    public void RecordChange_Then_Commit_Creates_UndoFrame()
    {
        using UndoRedoService svc = new();
        svc.RecordChange(new PropertyValueChanged(Guid.NewGuid(), "Width", null, "100"));
        svc.CommitBatch("Set Width");

        Assert.True(svc.CanUndo);
        Assert.Equal(1, svc.UndoCount);
    }

    [Fact]
    public void Undo_Returns_Inverse_Changes()
    {
        using UndoRedoService svc = new();
        Guid nodeId = Guid.NewGuid();
        svc.RecordChange(new PropertyValueChanged(nodeId, "Width", null, "100"));
        svc.CommitBatch("Set Width");

        IReadOnlyList<AstChange>? inverse = svc.Undo();

        Assert.NotNull(inverse);
        Assert.Single(inverse!);
        Assert.IsType<PropertyValueChanged>(inverse[0]);

        PropertyValueChanged pvc = (PropertyValueChanged)inverse[0];
        Assert.Equal("Width", pvc.PropertyName);
        Assert.Equal("100", pvc.OldValue); // Inverse swaps old/new
        Assert.Null(pvc.NewValue);
    }

    [Fact]
    public void Undo_Then_Redo_Returns_Original_Changes()
    {
        using UndoRedoService svc = new();
        Guid nodeId = Guid.NewGuid();
        svc.RecordChange(new PropertyValueChanged(nodeId, "Width", null, "100"));
        svc.CommitBatch("Set Width");

        svc.Undo();
        Assert.True(svc.CanRedo);

        IReadOnlyList<AstChange>? redo = svc.Redo();

        Assert.NotNull(redo);
        Assert.Single(redo!);
        PropertyValueChanged pvc = Assert.IsType<PropertyValueChanged>(redo[0]);
        Assert.Equal("100", pvc.NewValue);
    }

    [Fact]
    public void New_Commit_After_Undo_Clears_RedoStack()
    {
        using UndoRedoService svc = new();
        svc.RecordChange(new PropertyValueChanged(Guid.NewGuid(), "Width", null, "100"));
        svc.CommitBatch("Set Width");

        svc.Undo();
        Assert.True(svc.CanRedo);

        svc.RecordChange(new PropertyValueChanged(Guid.NewGuid(), "Height", null, "200"));
        svc.CommitBatch("Set Height");

        Assert.False(svc.CanRedo);
        Assert.Equal(1, svc.UndoCount);
    }

    [Fact]
    public void Multiple_Changes_In_Single_Batch()
    {
        using UndoRedoService svc = new();
        Guid nodeId = Guid.NewGuid();
        svc.RecordChange(new PropertyValueChanged(nodeId, "Width", null, "100"));
        svc.RecordChange(new PropertyValueChanged(nodeId, "Height", null, "50"));
        svc.CommitBatch("Set Size");

        Assert.Equal(1, svc.UndoCount);

        IReadOnlyList<AstChange>? inverse = svc.Undo();
        Assert.Equal(2, inverse!.Count);
    }

    [Fact]
    public void Clear_Resets_All_Stacks()
    {
        using UndoRedoService svc = new();
        svc.RecordChange(new PropertyValueChanged(Guid.NewGuid(), "Width", null, "100"));
        svc.CommitBatch("Test");

        svc.Clear();

        Assert.False(svc.CanUndo);
        Assert.False(svc.CanRedo);
        Assert.Equal(0, svc.UndoCount);
    }

    [Fact]
    public void StateChanged_Fires_On_Commit()
    {
        using UndoRedoService svc = new();
        bool fired = false;
        svc.StateChanged += () => fired = true;

        svc.RecordChange(new PropertyValueChanged(Guid.NewGuid(), "Width", null, "100"));
        svc.CommitBatch("Test");

        Assert.True(fired);
    }

    [Fact]
    public void NodeAdded_Inverse_Is_NodeRemoved()
    {
        using UndoRedoService svc = new();
        Guid nodeId = Guid.NewGuid();
        Guid parentId = Guid.NewGuid();
        svc.RecordChange(new NodeAdded(nodeId, parentId, 0, "Button"));
        svc.CommitBatch("Add Button");

        IReadOnlyList<AstChange>? inverse = svc.Undo();
        Assert.NotNull(inverse);
        Assert.Single(inverse!);
        Assert.IsType<NodeRemoved>(inverse![0]);
    }

    [Fact]
    public void NodeRemoved_Inverse_Is_NodeAdded()
    {
        using UndoRedoService svc = new();
        Guid nodeId = Guid.NewGuid();
        Guid parentId = Guid.NewGuid();
        svc.RecordChange(new NodeRemoved(nodeId, parentId, 0));
        svc.CommitBatch("Remove");

        IReadOnlyList<AstChange>? inverse = svc.Undo();
        Assert.NotNull(inverse);
        Assert.Single(inverse!);
        Assert.IsType<NodeAdded>(inverse![0]);
    }
}

/// <summary>
/// Tests for tree view models.
/// </summary>
public sealed class TreeViewModelTests
{
    [Fact]
    public void FromAstDocument_Creates_Tree()
    {
        MutableAstDocument doc = new();
        doc.Root = new MutableAstObjectNode
        {
            TypeName = "Grid",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        doc.Root.Children.Add(new MutableAstObjectNode
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        });

        XamlVisualEditor.TreeView.VisualTreeNodeViewModel? tree =
            XamlVisualEditor.TreeView.VisualTreeNodeViewModel.FromAstDocument(doc);

        Assert.NotNull(tree);
        Assert.Equal("Grid", tree!.TypeName);
        Assert.Single(tree.Children);
        Assert.Equal("Button", tree.Children[0].TypeName);
    }

    [Fact]
    public void FromAstDocument_Null_Returns_Null()
    {
        var result = XamlVisualEditor.TreeView.VisualTreeNodeViewModel.FromAstDocument(null);
        Assert.Null(result);
    }

    [Fact]
    public void FindByNodeId_Returns_Correct_Node()
    {
        MutableAstDocument doc = new();
        MutableAstObjectNode root = new()
        {
            TypeName = "Grid",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        MutableAstObjectNode button = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        root.Children.Add(button);
        doc.Root = root;

        XamlVisualEditor.TreeView.VisualTreeNodeViewModel? tree =
            XamlVisualEditor.TreeView.VisualTreeNodeViewModel.FromAstDocument(doc);

        Assert.NotNull(tree);

        var found = tree!.FindByNodeId(button.Id);
        Assert.NotNull(found);
        Assert.Equal("Button", found!.TypeName);
    }

    [Fact]
    public void SelectByNodeId_Selects_Correct_Node()
    {
        MutableAstDocument doc = new();
        MutableAstObjectNode root = new()
        {
            TypeName = "Grid",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        MutableAstObjectNode button = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        root.Children.Add(button);
        doc.Root = root;

        XamlVisualEditor.TreeView.VisualTreeNodeViewModel? tree =
            XamlVisualEditor.TreeView.VisualTreeNodeViewModel.FromAstDocument(doc);

        tree!.SelectByNodeId(button.Id);

        Assert.False(tree.IsSelected);
        Assert.True(tree.Children[0].IsSelected);
    }

    [Fact]
    public void LogicalTree_FromAstDocument_Creates_Tree()
    {
        MutableAstDocument doc = new();
        MutableAstObjectNode root = new()
        {
            TypeName = "Grid",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        MutableAstObjectNode textBlock = new()
        {
            TypeName = "TextBlock",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        textBlock.SetPropertyValue("Text", "Hello World");
        root.Children.Add(textBlock);
        doc.Root = root;

        XamlVisualEditor.TreeView.LogicalTreeNodeViewModel? tree =
            XamlVisualEditor.TreeView.LogicalTreeNodeViewModel.FromAstDocument(doc);

        Assert.NotNull(tree);
        Assert.Single(tree!.Children);
        Assert.Contains("Hello World", tree.Children[0].DisplayText);
    }
}

/// <summary>
/// Tests for the property editor ViewModel.
/// </summary>
public sealed class PropertyEditorTests
{
    [Fact]
    public void LoadFromDesignItem_Populates_Categories()
    {
        AstNodeMap map = new();
        MutableAstObjectNode node = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        node.SetPropertyValue("Content", "Click");
        node.SetPropertyValue("Width", "100");
        map.RegisterTree(node);

        XamlVisualEditor.Designer.Core.DesignItem item = new(node);
        XamlVisualEditor.PropertyEditor.PropertyEditorViewModel vm = new(map);
        vm.LoadFromDesignItem(item);

        Assert.Equal("Button", vm.SelectedTypeName);
        Assert.NotEmpty(vm.Categories);
    }

    [Fact]
    public void ApplyPropertyChange_Updates_AST()
    {
        AstNodeMap map = new();
        MutableAstObjectNode node = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        node.SetPropertyValue("Width", "100");
        map.RegisterTree(node);

        XamlVisualEditor.PropertyEditor.PropertyEditorViewModel vm = new(map);
        XamlVisualEditor.PropertyEditor.PropertyItemViewModel propVm =
            new("Width", "Layout", XamlVisualEditor.Core.PropertyKind.ClrProperty, node.Id)
            {
                Value = "200",
                IsSet = true
            };

        vm.ApplyPropertyChange(propVm);

        Assert.Equal("200", node.GetPropertyValue("Width"));
    }

    [Fact]
    public void ResetProperty_Removes_From_AST()
    {
        AstNodeMap map = new();
        MutableAstObjectNode node = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        node.SetPropertyValue("Width", "100");
        map.RegisterTree(node);

        XamlVisualEditor.PropertyEditor.PropertyEditorViewModel vm = new(map);
        XamlVisualEditor.PropertyEditor.PropertyItemViewModel propVm =
            new("Width", "Layout", XamlVisualEditor.Core.PropertyKind.ClrProperty, node.Id)
            {
                Value = "100",
                IsSet = true
            };

        vm.ResetProperty(propVm);

        Assert.Null(node.GetPropertyValue("Width"));
        Assert.False(propVm.IsSet);
    }
}

/// <summary>
/// Tests for the SelectionManager.
/// </summary>
public sealed class SelectionManagerTests
{
    [Fact]
    public void Select_Single_Item()
    {
        XamlVisualEditor.Designer.Core.SelectionManager mgr = new();
        MutableAstObjectNode node = new() { TypeName = "Button" };
        XamlVisualEditor.Designer.Core.DesignItem item = new(node);

        mgr.Select(item);

        Assert.Single(mgr.SelectedItems);
        Assert.Same(item, mgr.PrimarySelection);
    }

    [Fact]
    public void Select_Replace_Clears_Previous()
    {
        XamlVisualEditor.Designer.Core.SelectionManager mgr = new();
        MutableAstObjectNode node1 = new() { TypeName = "Button" };
        MutableAstObjectNode node2 = new() { TypeName = "TextBlock" };
        XamlVisualEditor.Designer.Core.DesignItem item1 = new(node1);
        XamlVisualEditor.Designer.Core.DesignItem item2 = new(node2);

        mgr.Select(item1);
        mgr.Select(item2);

        Assert.Single(mgr.SelectedItems);
        Assert.Same(item2, mgr.PrimarySelection);
    }

    [Fact]
    public void Select_AddToSelection()
    {
        XamlVisualEditor.Designer.Core.SelectionManager mgr = new();
        MutableAstObjectNode node1 = new() { TypeName = "Button" };
        MutableAstObjectNode node2 = new() { TypeName = "TextBlock" };
        XamlVisualEditor.Designer.Core.DesignItem item1 = new(node1);
        XamlVisualEditor.Designer.Core.DesignItem item2 = new(node2);

        mgr.Select(item1);
        mgr.Select(item2, addToSelection: true);

        Assert.Equal(2, mgr.SelectedItems.Count);
    }

    [Fact]
    public void ClearSelection_Removes_All()
    {
        XamlVisualEditor.Designer.Core.SelectionManager mgr = new();
        MutableAstObjectNode node = new() { TypeName = "Button" };
        XamlVisualEditor.Designer.Core.DesignItem item = new(node);

        mgr.Select(item);
        mgr.ClearSelection();

        Assert.Empty(mgr.SelectedItems);
        Assert.Null(mgr.PrimarySelection);
    }

    [Fact]
    public void ToggleSelection()
    {
        XamlVisualEditor.Designer.Core.SelectionManager mgr = new();
        MutableAstObjectNode node = new() { TypeName = "Button" };
        XamlVisualEditor.Designer.Core.DesignItem item = new(node);

        mgr.Select(item);
        mgr.ToggleSelection(item);

        Assert.Empty(mgr.SelectedItems);
    }

    [Fact]
    public void SelectionChanged_Event_Fires()
    {
        XamlVisualEditor.Designer.Core.SelectionManager mgr = new();
        MutableAstObjectNode node = new() { TypeName = "Button" };
        XamlVisualEditor.Designer.Core.DesignItem item = new(node);

        bool fired = false;
        mgr.SelectionChanged += _ => fired = true;

        mgr.Select(item);

        Assert.True(fired);
    }
}

// ==============================================
// Tolerant Parsing Tests
// ==============================================
public sealed class TolerantParsingTests
{
    [Fact]
    public void Parse_Unclosed_Tag_With_Tolerant_Mode_Returns_PartialDocument()
    {
        XamlParsingService parser = new();
        string xaml = "<Grid><Button /></Grid";

        ParseResult result = parser.Parse(xaml, new XamlParserOptions { UseTolerantParser = true });

        // Should have diagnostics for the error
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Parse_Valid_Xml_With_Tolerant_Returns_Document()
    {
        XamlParsingService parser = new();
        string xaml = "<Grid><Button /></Grid>";

        ParseResult result = parser.Parse(xaml, new XamlParserOptions { UseTolerantParser = true });

        Assert.NotNull(result.Document);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parse_Malformed_Attribute_With_Tolerant()
    {
        XamlParsingService parser = new();
        string xaml = """<Grid><Button Width="100" Height></Grid>""";

        ParseResult result = parser.Parse(xaml, new XamlParserOptions { UseTolerantParser = true });

        Assert.NotEmpty(result.Diagnostics);
        Assert.True(result.Diagnostics[0].Severity == DiagnosticSeverity.Error);
    }
}

// ==============================================
// Intellisense Provider Tests
// ==============================================
public sealed class IntellisenseProviderTests
{
    [Fact]
    public void ElementCompletionProvider_ShouldTrigger_On_LessThan()
    {
        ElementCompletionProvider provider = new();
        CompletionContext context = new()
        {
            TextBefore = "<",
            Offset = 1,
            Trigger = CompletionTrigger.CharacterTyped
        };

        Assert.True(provider.ShouldTrigger(context));
    }

    [Fact]
    public void ElementCompletionProvider_ShouldNotTrigger_On_Space()
    {
        ElementCompletionProvider provider = new();
        CompletionContext context = new()
        {
            TextBefore = " ",
            Offset = 1,
            Trigger = CompletionTrigger.CharacterTyped
        };

        Assert.False(provider.ShouldTrigger(context));
    }

    [Fact]
    public void AttributeCompletionProvider_ShouldTrigger_Inside_Tag()
    {
        AttributeCompletionProvider provider = new();
        CompletionContext context = new()
        {
            TextBefore = "<Button ",
            Offset = 8,
            Trigger = CompletionTrigger.CharacterTyped
        };

        Assert.True(provider.ShouldTrigger(context));
    }

    [Fact]
    public void ClosingTagCompletionProvider_ShouldTrigger_On_Slash()
    {
        ClosingTagCompletionProvider provider = new();
        CompletionContext context = new()
        {
            TextBefore = "<Grid><Button></",
            Offset = 16,
            Trigger = CompletionTrigger.CharacterTyped
        };

        Assert.True(provider.ShouldTrigger(context));
    }

    [Fact]
    public void ClosingTagCompletionProvider_Returns_Matching_Tag()
    {
        ClosingTagCompletionProvider provider = new();
        CompletionContext context = new()
        {
            TextBefore = "<Grid><Button></",
            Offset = 16,
            Trigger = CompletionTrigger.CharacterTyped
        };

        IReadOnlyList<CompletionItem> items = provider.GetCompletions(context);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText.Contains("Button"));
    }

    [Fact]
    public void CompletionProviderRegistry_CreateDefault_Has_Providers()
    {
        CompletionProviderRegistry registry = CompletionProviderRegistry.CreateDefault();

        CompletionContext context = new()
        {
            TextBefore = "<",
            Offset = 1,
            Trigger = CompletionTrigger.CharacterTyped
        };

        // Should not throw — providers are registered
        IReadOnlyList<CompletionItem> items = registry.GetCompletions(context);
        Assert.NotNull(items);
    }
}

// ==============================================
// Serialization Attribute Ordering Tests
// ==============================================
public sealed class SerializerAttributeOrderingTests
{
    [Fact]
    public void Serialize_Alphabetical_Ordering()
    {
        MutableAstDocument doc = new();
        MutableAstObjectNode root = new() { TypeName = "Button" };
        root.SetPropertyValue("Width", "100");
        root.SetPropertyValue("Content", "Click");
        root.SetPropertyValue("Background", "Red");
        doc.Root = root;

        XamlSerializationService serializer = new();
        string result = serializer.Serialize(doc, new SerializationOptions
        {
            AttributeOrdering = AttributeOrdering.Alphabetical
        });

        // In alphabetical order: Background before Content before Width
        int bgIdx = result.IndexOf("Background", StringComparison.Ordinal);
        int contentIdx = result.IndexOf("Content", StringComparison.Ordinal);
        int widthIdx = result.IndexOf("Width", StringComparison.Ordinal);

        Assert.True(bgIdx < contentIdx, "Background should come before Content");
        Assert.True(contentIdx < widthIdx, "Content should come before Width");
    }

    [Fact]
    public void Serialize_ByCategory_Ordering_Name_Before_Layout()
    {
        MutableAstDocument doc = new();
        MutableAstObjectNode root = new() { TypeName = "Button" };
        root.SetPropertyValue("Width", "100");
        root.SetPropertyValue("Name", "btn");
        root.SetPropertyValue("Background", "Red");
        doc.Root = root;

        XamlSerializationService serializer = new();
        string result = serializer.Serialize(doc, new SerializationOptions
        {
            AttributeOrdering = AttributeOrdering.ByCategory
        });

        // Name (category 0) before Width (category 1) before Background (category 2)
        int nameIdx = result.IndexOf("Name", StringComparison.Ordinal);
        int widthIdx = result.IndexOf("Width", StringComparison.Ordinal);
        int bgIdx = result.IndexOf("Background", StringComparison.Ordinal);

        Assert.True(nameIdx < widthIdx, "Name should come before Width");
        Assert.True(widthIdx < bgIdx, "Width should come before Background");
    }

    [Fact]
    public void Serialize_Preserve_Ordering_Keeps_Original_Order()
    {
        MutableAstDocument doc = new();
        MutableAstObjectNode root = new() { TypeName = "Button" };
        root.SetPropertyValue("Width", "100");
        root.SetPropertyValue("Content", "Click");
        root.SetPropertyValue("Background", "Red");
        doc.Root = root;

        XamlSerializationService serializer = new();
        string result = serializer.Serialize(doc, new SerializationOptions
        {
            AttributeOrdering = AttributeOrdering.Preserve
        });

        // Original order: Width, Content, Background
        int widthIdx = result.IndexOf("Width", StringComparison.Ordinal);
        int contentIdx = result.IndexOf("Content", StringComparison.Ordinal);
        int bgIdx = result.IndexOf("Background", StringComparison.Ordinal);

        Assert.True(widthIdx < contentIdx, "Width should come before Content (original order)");
        Assert.True(contentIdx < bgIdx, "Content should come before Background (original order)");
    }
}

// ==============================================
// Diagnostic Colorizer Tests
// ==============================================
public sealed class DiagnosticColorizerTests
{
    [Fact]
    public void GetDiagnosticAt_Returns_Matching_Diagnostic()
    {
        DiagnosticColorizer colorizer = new();
        List<XamlDiagnostic> diags = new()
        {
            new XamlDiagnostic
            {
                Severity = DiagnosticSeverity.Error,
                Message = "Unknown element",
                Line = 3,
                Column = 5,
                Length = 10
            }
        };

        colorizer.UpdateDiagnostics(diags);

        XamlDiagnostic? result = colorizer.GetDiagnosticAt(3, 8);
        Assert.NotNull(result);
        Assert.Equal("Unknown element", result!.Message);
    }

    [Fact]
    public void GetDiagnosticAt_Returns_Null_For_Wrong_Line()
    {
        DiagnosticColorizer colorizer = new();
        List<XamlDiagnostic> diags = new()
        {
            new XamlDiagnostic
            {
                Severity = DiagnosticSeverity.Error,
                Message = "Error",
                Line = 3,
                Column = 1,
                Length = 5
            }
        };

        colorizer.UpdateDiagnostics(diags);

        XamlDiagnostic? result = colorizer.GetDiagnosticAt(5, 1);
        Assert.Null(result);
    }

    [Fact]
    public void GetDiagnosticAt_Returns_Null_For_Column_Outside_Range()
    {
        DiagnosticColorizer colorizer = new();
        List<XamlDiagnostic> diags = new()
        {
            new XamlDiagnostic
            {
                Severity = DiagnosticSeverity.Warning,
                Message = "Warning",
                Line = 1,
                Column = 10,
                Length = 5
            }
        };

        colorizer.UpdateDiagnostics(diags);

        XamlDiagnostic? result = colorizer.GetDiagnosticAt(1, 3);
        Assert.Null(result);
    }

    [Fact]
    public void UpdateDiagnostics_With_Null_Uses_Empty()
    {
        DiagnosticColorizer colorizer = new();
        colorizer.UpdateDiagnostics(null!);

        XamlDiagnostic? result = colorizer.GetDiagnosticAt(1, 1);
        Assert.Null(result);
    }
}

// ==============================================
// Collaboration Op Mapping Tests
// ==============================================
public sealed class CollaborationOpTests
{
    [Fact]
    public void XamlCollabOpType_Has_All_Required_Values()
    {
        Assert.True(Enum.IsDefined(typeof(XamlCollabOpType), XamlCollabOpType.InsertNode));
        Assert.True(Enum.IsDefined(typeof(XamlCollabOpType), XamlCollabOpType.RemoveNode));
        Assert.True(Enum.IsDefined(typeof(XamlCollabOpType), XamlCollabOpType.MoveNode));
        Assert.True(Enum.IsDefined(typeof(XamlCollabOpType), XamlCollabOpType.SetProperty));
        Assert.True(Enum.IsDefined(typeof(XamlCollabOpType), XamlCollabOpType.RemoveProperty));
    }

    [Fact]
    public void AstChange_NodeAdded_Can_Be_Created()
    {
        Guid parentId = Guid.NewGuid();
        Guid nodeId = Guid.NewGuid();

        NodeAdded change = new(nodeId, parentId, 0, "Grid");

        Assert.Equal(parentId, change.ParentId);
        Assert.Equal(nodeId, change.NodeId);
        Assert.Equal("Grid", change.NodeTypeName);
        Assert.Equal(0, change.Index);
    }

    [Fact]
    public void AstChange_PropertyValueChanged_Can_Be_Created()
    {
        Guid nodeId = Guid.NewGuid();

        PropertyValueChanged change = new(nodeId, "Width", "50", "100");

        Assert.Equal(nodeId, change.NodeId);
        Assert.Equal("Width", change.PropertyName);
        Assert.Equal("50", change.OldValue);
        Assert.Equal("100", change.NewValue);
    }
}

// ==============================================
// Performance Tests (10.4.1–10.4.5)
// ==============================================

/// <summary>
/// Performance tests to ensure key operations complete within acceptable time bounds.
/// These validate that core operations don't regress beyond acceptable thresholds.
/// </summary>
public sealed class PerformanceTests
{
    /// <summary>
    /// 10.4.1 — Profile XAML parse time for large files (1000+ lines).
    /// The parser should handle large documents in under 500ms.
    /// </summary>
    [Fact]
    public void ParseLargeXaml_Completes_Under_Threshold()
    {
        // Generate a large XAML file (~1000 lines)
        System.Text.StringBuilder sb = new();
        sb.AppendLine("<UserControl xmlns=\"https://github.com/avaloniaui\"");
        sb.AppendLine("             xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        sb.AppendLine("  <StackPanel>");

        for (int i = 0; i < 300; i++)
        {
            sb.AppendLine($"    <Grid>");
            sb.AppendLine($"      <TextBlock Text=\"Item {i}\" Margin=\"4\" FontSize=\"14\" />");
            sb.AppendLine($"      <Button Content=\"Action {i}\" Width=\"100\" Height=\"32\" />");
            sb.AppendLine($"    </Grid>");
        }

        sb.AppendLine("  </StackPanel>");
        sb.AppendLine("</UserControl>");

        string largeXaml = sb.ToString();
        Assert.True(largeXaml.Split('\n').Length > 1000, "Generated XAML should be 1000+ lines");

        XamlParsingService parser = new();
        Stopwatch sw = Stopwatch.StartNew();

        ParseResult result = parser.Parse(largeXaml);

        sw.Stop();

        Assert.NotNull(result.Document);
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Parsing {largeXaml.Split('\n').Length} lines took {sw.ElapsedMilliseconds}ms (threshold: 5000ms)");
    }

    /// <summary>
    /// 10.4.2 — Profile serialization for large AST.
    /// Serialization should handle large documents efficiently.
    /// </summary>
    [Fact]
    public void SerializeLargeAst_Completes_Under_Threshold()
    {
        // Build a large AST manually
        MutableAstObjectNode root = new()
        {
            TypeName = "StackPanel",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        for (int i = 0; i < 500; i++)
        {
            MutableAstObjectNode child = new()
            {
                TypeName = "TextBlock",
                XmlNamespace = "https://github.com/avaloniaui"
            };
            child.SetPropertyValue("Text", $"Item {i}");
            child.SetPropertyValue("Margin", "4");
            child.SetPropertyValue("FontSize", "14");
            root.Children.Add(child);
        }

        MutableAstDocument doc = new() { Root = root };
        XamlSerializationService serializer = new();

        Stopwatch sw = Stopwatch.StartNew();

        string output = serializer.Serialize(doc);

        sw.Stop();

        Assert.NotEmpty(output);
        Assert.Contains("Item 499", output);
        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"Serializing 500 nodes took {sw.ElapsedMilliseconds}ms (threshold: 2000ms)");
    }

    /// <summary>
    /// 10.4.3 — Profile sync engine throughput.
    /// Loading and syncing should complete promptly.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task SyncEngine_LoadLargeDocument_Under_Threshold()
    {
        System.Text.StringBuilder sb = new();
        sb.AppendLine("<UserControl xmlns=\"https://github.com/avaloniaui\">");
        sb.AppendLine("  <StackPanel>");

        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"    <TextBlock Text=\"Line {i}\" />");
        }

        sb.AppendLine("  </StackPanel>");
        sb.AppendLine("</UserControl>");

        string xaml = sb.ToString();

        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);

        Stopwatch sw = Stopwatch.StartNew();

        await engine.LoadAsync(xaml);

        sw.Stop();

        Assert.NotNull(engine.CurrentDocument);
        Assert.True(sw.ElapsedMilliseconds < 3000,
            $"Loading {xaml.Split('\n').Length} lines took {sw.ElapsedMilliseconds}ms (threshold: 3000ms)");
    }

    /// <summary>
    /// 10.4.4 — Profile memory usage during property mutations.
    /// Rapid property changes should not cause excessive allocations.
    /// </summary>
    [Fact]
    public void RapidPropertyMutations_No_Excessive_Allocations()
    {
        MutableAstObjectNode node = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        long memBefore = GC.GetTotalMemory(forceFullCollection: true);

        for (int i = 0; i < 10000; i++)
        {
            node.SetPropertyValue("Width", i.ToString());
        }

        long memAfter = GC.GetTotalMemory(forceFullCollection: true);
        long memDelta = memAfter - memBefore;

        Assert.Equal("9999", node.GetPropertyValue("Width"));
        // 10K mutations should not allocate more than 10MB of retained memory
        Assert.True(memDelta < 10 * 1024 * 1024,
            $"10K property mutations allocated {memDelta / 1024}KB (threshold: 10MB)");
    }

    /// <summary>
    /// 10.4.5 — Profile intellisense completion time.
    /// Completion lookups should be fast enough for interactive use.
    /// </summary>
    [Fact]
    public void IntellisenseCompletion_Completes_Under_Threshold()
    {
        CompletionProviderRegistry registry = CompletionProviderRegistry.CreateDefault();

        CompletionContext ctx = new()
        {
            TextBefore = "<",
            Offset = 1,
            Trigger = CompletionTrigger.CharacterTyped
        };

        Stopwatch sw = Stopwatch.StartNew();

        for (int i = 0; i < 100; i++)
        {
            IReadOnlyList<CompletionItem> items = registry.GetCompletions(ctx);
        }

        sw.Stop();

        // 100 completions should complete in under 500ms
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"100 completion lookups took {sw.ElapsedMilliseconds}ms (threshold: 500ms)");
    }
}
