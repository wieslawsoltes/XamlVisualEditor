using Xunit;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Xaml.Intellisense;
using XamlVisualEditor.Xaml.Parsing;
using XamlVisualEditor.Xaml.Serialization;
using XamlVisualEditor.Sync;
using XamlVisualEditor.Collaboration;
using XamlVisualEditor.Collaboration.UI;
using XamlVisualEditor.Workspace;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Tests.Integration;

/// <summary>
/// Integration tests for the parse → AST → serialize round-trip.
/// </summary>
public sealed class RoundTripTests
{
    private const string SampleXaml = """
        <UserControl xmlns="https://github.com/avaloniaui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <Grid>
                <StackPanel>
                    <TextBlock Text="Hello World" />
                    <Button Content="Click Me" Width="100" Height="32" />
                </StackPanel>
            </Grid>
        </UserControl>
        """;

    [Fact]
    public void Parse_Then_Serialize_Preserves_Structure()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();

        ParseResult result = parser.Parse(SampleXaml);
        Assert.NotNull(result.Document);

        string output = serializer.Serialize(result.Document!);

        Assert.Contains("UserControl", output);
        Assert.Contains("Grid", output);
        Assert.Contains("StackPanel", output);
        Assert.Contains("TextBlock", output);
        Assert.Contains("Button", output);
        Assert.Contains("Hello World", output);
        Assert.Contains("Click Me", output);
    }

    [Fact]
    public void Parse_Then_Modify_Then_Serialize_Reflects_Change()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();

        ParseResult result = parser.Parse(SampleXaml);
        MutableAstDocument? doc = result.Document as MutableAstDocument;
        Assert.NotNull(doc?.Root);

        // Find the Button and change its Content
        MutableAstObjectNode? button = FindNode(doc!.Root!, "Button");
        Assert.NotNull(button);

        button!.SetPropertyValue("Content", "Updated");

        string output = serializer.Serialize(doc);
        Assert.Contains("Content=\"Updated\"", output);
        Assert.DoesNotContain("Click Me", output);
    }

    [Fact]
    public void Parse_Then_Add_Node_Then_Serialize()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();

        ParseResult result = parser.Parse(SampleXaml);
        MutableAstDocument? doc = result.Document as MutableAstDocument;
        Assert.NotNull(doc?.Root);

        MutableAstObjectNode? stackPanel = FindNode(doc!.Root!, "StackPanel");
        Assert.NotNull(stackPanel);

        MutableAstObjectNode newCheckBox = new()
        {
            TypeName = "CheckBox",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        newCheckBox.SetPropertyValue("Content", "Accept");
        stackPanel!.Children.Add(newCheckBox);

        string output = serializer.Serialize(doc);
        Assert.Contains("CheckBox", output);
        Assert.Contains("Content=\"Accept\"", output);
    }

    [Fact]
    public async System.Threading.Tasks.Task SyncEngine_RoundTrip()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);

        await engine.LoadAsync(SampleXaml);

        Assert.NotNull(engine.CurrentDocument);
        Assert.NotNull(engine.CurrentText);

        // Modify AST
        MutableAstObjectNode? button = FindNode(engine.CurrentDocument!.Root!, "Button");
        Assert.NotNull(button);
        button!.SetPropertyValue("Content", "SyncTest");

        // Notify sync engine
        engine.NotifyAstChanged(engine.CurrentDocument!, SyncSource.DesignSurface);

        // Verify text updated
        Assert.Contains("SyncTest", engine.CurrentText);
    }

    [Fact]
    public async System.Threading.Tasks.Task SyncEngine_UndoRedo_RoundTrip()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);

        await engine.LoadAsync(SampleXaml);

        // Modify a property
        MutableAstObjectNode? button = FindNode(engine.CurrentDocument!.Root!, "Button");
        Assert.NotNull(button);
        button!.SetPropertyValue("Content", "Modified");
        engine.CommitUndoBatch("Modify Content");

        Assert.True(engine.UndoRedo.CanUndo);

        // Undo
        engine.Undo();
        Assert.False(engine.UndoRedo.CanUndo);
        Assert.True(engine.UndoRedo.CanRedo);

        // Redo
        engine.Redo();
        Assert.True(engine.UndoRedo.CanUndo);
        Assert.False(engine.UndoRedo.CanRedo);
    }

    [Fact]
    public async System.Threading.Tasks.Task SyncEngine_Multiple_Property_Changes_Undo()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);

        await engine.LoadAsync(SampleXaml);

        MutableAstObjectNode? button = FindNode(engine.CurrentDocument!.Root!, "Button");
        Assert.NotNull(button);

        // Batch 1
        button!.SetPropertyValue("Content", "Step1");
        engine.CommitUndoBatch("Step 1");

        // Batch 2
        button.SetPropertyValue("Content", "Step2");
        engine.CommitUndoBatch("Step 2");

        Assert.Equal(2, engine.UndoRedo.UndoCount);

        engine.Undo(); // Back to Step1
        Assert.Equal(1, engine.UndoRedo.UndoCount);

        engine.Undo(); // Back to original
        Assert.Equal(0, engine.UndoRedo.UndoCount);
    }

    [Fact]
    public async System.Threading.Tasks.Task SyncEngine_NotifyAstChanged_Emits_SyncEvent()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);

        await engine.LoadAsync(SampleXaml);

        SyncEvent? received = null;
        engine.SyncEvents.Subscribe(e => received = e);

        // Modify AST and notify
        MutableAstObjectNode? button = FindNode(engine.CurrentDocument!.Root!, "Button");
        button!.SetPropertyValue("Content", "EventTest");
        engine.NotifyAstChanged(engine.CurrentDocument!, SyncSource.DesignSurface);

        Assert.NotNull(received);
        Assert.Equal(SyncSource.DesignSurface, received!.Source);
    }

    [Fact]
    public async System.Threading.Tasks.Task SyncEngine_Dispose_Cleans_Up()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);

        await engine.LoadAsync(SampleXaml);
        engine.Dispose();

        // After dispose, UndoRedo should be cleared
        Assert.False(engine.UndoRedo.CanUndo);
    }

    [Fact]
    public void Parse_Complex_Nested_RoundTrip()
    {
        const string xaml = """
            <Window xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Title="Test Window" Width="800" Height="600">
                <DockPanel>
                    <Menu DockPanel.Dock="Top">
                        <MenuItem Header="File" />
                        <MenuItem Header="Edit" />
                    </Menu>
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto" />
                            <RowDefinition Height="*" />
                        </Grid.RowDefinitions>
                        <TextBlock Text="Header" Grid.Row="0" />
                        <ListBox Grid.Row="1" />
                    </Grid>
                </DockPanel>
            </Window>
            """;

        XamlParsingService parser = new();
        XamlSerializationService serializer = new();

        ParseResult result = parser.Parse(xaml);
        Assert.NotNull(result.Document);
        Assert.Empty(result.Diagnostics);

        string output = serializer.Serialize(result.Document!);

        Assert.Contains("Window", output);
        Assert.Contains("DockPanel", output);
        Assert.Contains("Menu", output);
        Assert.Contains("MenuItem", output);
        Assert.Contains("Grid", output);
        Assert.Contains("TextBlock", output);
        Assert.Contains("ListBox", output);
        Assert.Contains("Title=\"Test Window\"", output);
    }

    [Fact]
    public void Parse_Remove_Node_Serialize()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();

        ParseResult result = parser.Parse(SampleXaml);
        MutableAstDocument? doc = result.Document as MutableAstDocument;
        Assert.NotNull(doc?.Root);

        MutableAstObjectNode? stackPanel = FindNode(doc!.Root!, "StackPanel");
        Assert.NotNull(stackPanel);

        // Remove the Button from StackPanel
        MutableAstObjectNode? button = FindNode(stackPanel!, "Button");
        Assert.NotNull(button);
        stackPanel!.Children.Remove(button!);

        string output = serializer.Serialize(doc);
        Assert.DoesNotContain("Button", output);
        Assert.Contains("TextBlock", output);
    }

    [Fact]
    public void NodeMap_Registration_During_Parse()
    {
        XamlParsingService parser = new();
        AstNodeMap map = new();

        ParseResult result = parser.Parse(SampleXaml);
        MutableAstDocument? doc = result.Document as MutableAstDocument;
        Assert.NotNull(doc?.Root);

        map.RegisterTree(doc!.Root!);

        // Should be able to find all nodes by ID
        MutableAstObjectNode? button = FindNode(doc.Root!, "Button");
        Assert.NotNull(button);

        MutableAstNode? found = map.FindById(button!.Id);
        Assert.Same(button, found);
    }

    private static MutableAstObjectNode? FindNode(MutableAstObjectNode root, string typeName)
    {
        if (root.TypeName == typeName)
        {
            return root;
        }

        foreach (MutableAstNode child in root.Children)
        {
            if (child is MutableAstObjectNode obj)
            {
                MutableAstObjectNode? found = FindNode(obj, typeName);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Integration tests for intellisense completion.
/// </summary>
public sealed class IntellisenseTests
{
    [Fact]
    public void CompletionRegistry_Returns_ClosingTag_Completions()
    {
        CompletionProviderRegistry registry = CompletionProviderRegistry.CreateDefault();

        CompletionContext ctx = new()
        {
            TextBefore = "<Grid xmlns=\"https://github.com/avaloniaui\">\n  <Button />\n  </",
            Offset = "<Grid xmlns=\"https://github.com/avaloniaui\">\n  <Button />\n  </".Length,
            Trigger = CompletionTrigger.CharacterTyped
        };

        IReadOnlyList<CompletionItem> items = registry.GetCompletions(ctx);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText == "</Grid>");
    }

    [Fact]
    public void CompletionRegistry_Returns_Value_Completions_For_Alignment()
    {
        CompletionProviderRegistry registry = CompletionProviderRegistry.CreateDefault();

        CompletionContext ctx = new()
        {
            TextBefore = "<Button HorizontalAlignment=\"",
            Offset = "<Button HorizontalAlignment=\"".Length,
            Trigger = CompletionTrigger.CharacterTyped
        };

        IReadOnlyList<CompletionItem> items = registry.GetCompletions(ctx);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText == "Center");
        Assert.Contains(items, i => i.DisplayText == "Stretch");
    }
}

// ==============================================
// 7.8 — Workspace Loading Integration Tests
// ==============================================

/// <summary>
/// Integration tests for the workspace service and type metadata.
/// These test the standalone mode and metadata service without requiring real MSBuild.
/// </summary>
public sealed class WorkspaceIntegrationTests
{
    [Fact]
    public void CreateStandaloneWorkspace_Returns_SingleProject()
    {
        WorkspaceService service = new();

        string tempFile = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "test-standalone.axaml");

        WorkspaceModel workspace = service.CreateStandaloneWorkspace(tempFile);

        Assert.Single(workspace.Projects);
        Assert.Single(workspace.Projects[0].XamlFiles);
        Assert.Equal(tempFile, workspace.Projects[0].XamlFiles[0].FilePath);
    }

    [Fact]
    public void CreateStandaloneWorkspace_ProjectName_Is_FileName()
    {
        WorkspaceService service = new();

        string tempFile = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "MyView.axaml");

        WorkspaceModel workspace = service.CreateStandaloneWorkspace(tempFile);

        Assert.Equal("MyView.axaml", workspace.Projects[0].Name);
    }

    [Fact]
    public void CreateStandaloneWorkspace_Has_Empty_References()
    {
        WorkspaceService service = new();

        string tempFile = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "test.axaml");

        WorkspaceModel workspace = service.CreateStandaloneWorkspace(tempFile);

        Assert.Empty(workspace.Projects[0].References);
    }
}

// ==============================================
// 10.2.2 — MSBuild Workspace Loading Tests
// ==============================================

/// <summary>
/// Tests for workspace model creation and manipulation.
/// Uses standalone mode to avoid requiring MSBuild SDK.
/// </summary>
public sealed class MsBuildWorkspaceTests
{
    [Fact]
    public void WorkspaceModel_Can_Hold_Multiple_Projects()
    {
        WorkspaceModel workspace = new()
        {
            Projects = new List<ProjectModel>
            {
                new()
                {
                    Name = "Project1",
                    ProjectPath = "/path/to/Project1.csproj",
                    XamlFiles = new List<XamlFileModel>
                    {
                        new() { FilePath = "/path/MainWindow.axaml", RelativePath = "MainWindow.axaml" }
                    },
                    Files = Array.Empty<ProjectFileModel>(),
                    References = Array.Empty<AssemblyReference>()
                },
                new()
                {
                    Name = "Project2",
                    ProjectPath = "/path/to/Project2.csproj",
                    XamlFiles = Array.Empty<XamlFileModel>(),
                    Files = Array.Empty<ProjectFileModel>(),
                    References = new List<AssemblyReference>
                    {
                        new() { Name = "Avalonia", Path = "/lib/Avalonia.dll" }
                    }
                }
            }
        };

        Assert.Equal(2, workspace.Projects.Count);
        Assert.Single(workspace.Projects[0].XamlFiles);
        Assert.Single(workspace.Projects[1].References);
    }
}

// ==============================================
// 10.2.3 — Type Metadata Resolution Tests
// ==============================================

/// <summary>
/// Tests for the type metadata service's resolution capabilities.
/// </summary>
public sealed class TypeMetadataResolutionTests
{
    [Fact]
    public void TypeMetadataService_GetType_Returns_Null_For_Unknown()
    {
        TypeMetadataService service = new();

        TypeMetadata? result = service.GetType("https://unknown", "NonExistentControl");

        Assert.Null(result);
    }

    [Fact]
    public void TypeMetadataService_GetProperties_Returns_Empty_For_Unknown()
    {
        TypeMetadataService service = new();

        TypeMetadata unknownType = new()
        {
            FullName = "Unknown.Type",
            Name = "Type",
            XmlNamespace = "clr-namespace:Unknown",
            ClrNamespace = "Unknown",
            AssemblyName = "Unknown"
        };

        IReadOnlyList<PropertyMetadata> props = service.GetProperties(unknownType);

        Assert.Empty(props);
    }

    [Fact]
    public void TypeMetadataService_GetEvents_Returns_Empty_For_Unknown()
    {
        TypeMetadataService service = new();

        TypeMetadata unknownType = new()
        {
            FullName = "Unknown.Type",
            Name = "Type",
            XmlNamespace = "clr-namespace:Unknown",
            ClrNamespace = "Unknown",
            AssemblyName = "Unknown"
        };

        IReadOnlyList<EventMetadata> events = service.GetEvents(unknownType);

        Assert.Empty(events);
    }

    [Fact]
    public void TypeMetadataService_GetAvailableNamespaces_Returns_Empty_By_Default()
    {
        TypeMetadataService service = new();

        IReadOnlyList<string> namespaces = service.GetAvailableNamespaces();

        Assert.Contains("https://github.com/avaloniaui", namespaces);
    }
}

// ==============================================
// 10.2.4 — Collaboration Session Lifecycle Tests
// ==============================================

/// <summary>
/// Tests for collaboration session lifecycle operations.
/// </summary>
public sealed class CollaborationLifecycleTests
{
    [Fact]
    public void CollaborationPanel_StartSession_Creates_SessionId()
    {
        CollaborationPanelViewModel panel = new();

        panel.StartSessionCommand.Execute().Subscribe();

        Assert.True(panel.IsSessionActive);
        Assert.NotNull(panel.SessionId);
        Assert.NotEmpty(panel.SessionId!);
    }

    [Fact]
    public void CollaborationPanel_JoinSession_Sets_SessionId()
    {
        CollaborationPanelViewModel panel = new();

        panel.SessionId = "test123";
        panel.JoinSessionCommand.Execute().Subscribe();

        Assert.True(panel.IsSessionActive);
        Assert.Equal("test123", panel.SessionId);
    }

    [Fact]
    public void CollaborationPanel_LeaveSession_Clears_State()
    {
        CollaborationPanelViewModel panel = new();

        panel.StartSessionCommand.Execute().Subscribe();
        Assert.True(panel.IsSessionActive);

        panel.LeaveSessionCommand.Execute().Subscribe();

        Assert.False(panel.IsSessionActive);
        Assert.Null(panel.SessionId);
        Assert.Empty(panel.Participants);
    }

    [Fact]
    public void CollaborationPanel_StartSession_Adds_Local_Participant()
    {
        CollaborationPanelViewModel panel = new();

        panel.StartSessionCommand.Execute().Subscribe();

        Assert.Single(panel.Participants);
        Assert.True(panel.Participants[0].IsLocal);
    }

    [Fact]
    public void CollabUndoRedoService_Records_And_Undoes()
    {
        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);
        XamlCollabBridge bridge = new(map, engine);

        CollabUndoRedoService undoRedo = new(bridge);

        Guid nodeId = Guid.NewGuid();
        PropertyValueChanged change = new(nodeId, "Width", "50", "100");

        undoRedo.BeginBatch("Set Width");
        undoRedo.RecordChange(change);
        undoRedo.CommitBatch();

        Assert.True(undoRedo.CanUndo);
        Assert.False(undoRedo.CanRedo);

        IReadOnlyList<AstChange>? undoneChanges = undoRedo.Undo();

        Assert.NotNull(undoneChanges);
        Assert.Single(undoneChanges!);
        Assert.False(undoRedo.CanUndo);
        Assert.True(undoRedo.CanRedo);
    }

    [Fact]
    public void SharedFileCollabSession_Is_Not_Connected_Initially()
    {
        string tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"collab-test-{Guid.NewGuid():N}");

        using SharedFileCollabSession session = new(tempDir, "participant1");

        Assert.False(session.IsConnected);

        // Clean up
        try { System.IO.Directory.Delete(tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void SharedFileCollabSession_Start_Sets_Connected()
    {
        string tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"collab-test-{Guid.NewGuid():N}");

        using SharedFileCollabSession session = new(tempDir, "participant1");
        session.Start();

        Assert.True(session.IsConnected);

        session.Stop();
        Assert.False(session.IsConnected);

        // Clean up
        try { System.IO.Directory.Delete(tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void CollabRealtimeSession_Is_Not_Connected_Initially()
    {
        using CollabRealtimeSession session = new("ws://localhost:8080", "session1", "participant1");

        Assert.False(session.IsConnected);
    }

    [Fact]
    public void SolutionExplorer_FromWorkspace_Creates_Tree()
    {
        WorkspaceModel workspace = new()
        {
            Projects = new List<ProjectModel>
            {
                new()
                {
                    Name = "MyProject",
                    ProjectPath = "/path/to/MyProject.csproj",
                    XamlFiles = new List<XamlFileModel>
                    {
                        new() { FilePath = "/path/MainWindow.axaml", RelativePath = "MainWindow.axaml" },
                        new() { FilePath = "/path/MyView.axaml", RelativePath = "Views/MyView.axaml" }
                    },
                    Files = Array.Empty<ProjectFileModel>(),
                    References = new List<AssemblyReference>
                    {
                        new() { Name = "Avalonia", Path = "/lib/Avalonia.dll" }
                    }
                }
            }
        };

        SolutionExplorerNodeViewModel root = SolutionExplorerNodeViewModel.FromWorkspace(workspace, "TestSolution");

        Assert.Equal("TestSolution", root.Name);
        Assert.Equal(SolutionExplorerNodeKind.Solution, root.Kind);
        Assert.Single(root.Children); // 1 project

        SolutionExplorerNodeViewModel project = root.Children[0];
        Assert.Equal("MyProject", project.Name);
        Assert.Equal(SolutionExplorerNodeKind.Project, project.Kind);
        Assert.Single(project.Children); // References folder only

        SolutionExplorerNodeViewModel refsFolder = project.Children[0];
        Assert.Equal("References", refsFolder.Name);
        Assert.Single(refsFolder.Children);
    }
}
