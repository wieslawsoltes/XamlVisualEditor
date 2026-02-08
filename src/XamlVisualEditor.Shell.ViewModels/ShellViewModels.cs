using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Diagnostics;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.CodeEditor;
using XamlVisualEditor.Collaboration;
using XamlVisualEditor.Collaboration.UI;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Designer.Adorners;
using XamlVisualEditor.Designer.Core;
using XamlVisualEditor.Designer.DragDrop;
using XamlVisualEditor.Designer.Rendering;
using XamlVisualEditor.PropertyEditor;
using XamlVisualEditor.Sync;
using XamlVisualEditor.TreeView;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Xaml.Intellisense;
using XamlVisualEditor.Workspace;
using XamlVisualEditor.Xaml.Parsing;
using XamlVisualEditor.Xaml.Serialization;
using XamlVisualEditor.Shell;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>
/// Common interface for editor documents.
/// </summary>
public interface IEditorDocumentViewModel : IDisposable
{
    string FilePath { get; }
    string FileName { get; }
    bool IsModified { get; }
    int CurrentLine { get; }
    int CurrentColumn { get; }
    ReactiveCommand<Unit, Unit> SaveCommand { get; }
}

/// <summary>
/// ViewModel for a XAML document tab (designer + code split view).
/// </summary>
public sealed class DesignerDocumentViewModel : ReactiveObject, IEditorDocumentViewModel
{
    private readonly CompositeDisposable _disposables = new();
    private SyncSource _selectionSource = SyncSource.DesignSurface;

    /// <summary>
    /// Gets the file path of the XAML document.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the file name for display.
    /// </summary>
    public string FileName => System.IO.Path.GetFileName(FilePath);

    /// <summary>
    /// Gets the current caret line (1-based).
    /// </summary>
    public int CurrentLine => CodeEditor.CurrentLine;

    /// <summary>
    /// Gets the current caret column (1-based).
    /// </summary>
    public int CurrentColumn => CodeEditor.CurrentColumn;

    /// <summary>
    /// Gets or sets whether the document is modified.
    /// </summary>
    [Reactive]
    public bool IsModified { get; set; }

    /// <summary>
    /// Gets the title for display (includes dirty indicator).
    /// </summary>
    public string Title => IsModified ? $"{FileName}*" : FileName;

    // Sub-ViewModels
    public DesignSurfaceViewModel DesignSurface { get; }
    public CodeEditorViewModel CodeEditor { get; }
    public PropertyEditorViewModel PropertyEditor { get; }

    // Services
    public SyncEngine SyncEngine { get; }
    public AstNodeMap NodeMap { get; }
    public ControlFactory ControlFactory { get; }
    public SelectionManager SelectionManager { get; }

    /// <summary>
    /// Gets or sets the active view mode.
    /// </summary>
    [Reactive]
    public DocumentViewMode ViewMode { get; set; } = DocumentViewMode.Split;

    /// <summary>
    /// Gets or sets the selected AST node ID (synced between editor, tree, and designer).
    /// </summary>
    [Reactive]
    public Guid? SelectedNodeId { get; set; }

    /// <summary>
    /// Gets the source of the most recent selection change.
    /// </summary>
    public SyncSource SelectionSource => _selectionSource;

    /// <summary>
    /// Updates selection with an explicit source to avoid feedback loops.
    /// </summary>
    public void SetSelectedNode(Guid? nodeId, SyncSource source)
    {
        _selectionSource = source;
        SelectedNodeId = nodeId;
    }

    /// <summary>
    /// Command to save the document.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    /// <summary>
    /// Command to close the document.
    /// </summary>
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    /// <summary>
    /// Command to switch to design view.
    /// </summary>
    public ReactiveCommand<Unit, Unit> DesignViewCommand { get; }

    /// <summary>
    /// Command to switch to code view.
    /// </summary>
    public ReactiveCommand<Unit, Unit> CodeViewCommand { get; }

    /// <summary>
    /// Command to switch to split view.
    /// </summary>
    public ReactiveCommand<Unit, Unit> SplitViewCommand { get; }

    public DesignerDocumentViewModel(string filePath, ITypeMetadataService? metadataService = null)
    {
        FilePath = filePath;

        // Create services
        XamlParsingService parsingService = new();
        XamlSerializationService serializationService = new();
        NodeMap = new AstNodeMap();
        SyncEngine = new SyncEngine(parsingService, serializationService, NodeMap);
        SelectionManager = new SelectionManager();

        // Create sub-ViewModels
        DesignSurface = new DesignSurfaceViewModel();
        CompletionProviderRegistry completionRegistry = new();
        CodeEditor = new CodeEditorViewModel(SyncEngine, completionRegistry);
        PropertyEditor = new PropertyEditorViewModel(NodeMap);
        ControlFactory = new ControlFactory(metadataService);

        // Wire property editor changes back to the sync engine
        PropertyEditor.PropertyValueApplied += _ =>
        {
            if (SyncEngine.CurrentDocument is not null)
            {
                SyncEngine.NotifyAstChanged(SyncEngine.CurrentDocument, SyncSource.DesignSurface);
            }
        };

        // Track modification state
        IDisposable isModifiedSubscription = this.WhenAnyValue(x => x.CodeEditor.IsModified)
            .Subscribe(m => IsModified = m);
        _disposables.Add(isModifiedSubscription);

        // Watch for property changes to raise Title notification
        IDisposable titleSubscription = this.WhenAnyValue(x => x.IsModified)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(Title)));
        _disposables.Add(titleSubscription);

        // Sync caret→node from code editor to selected node
        CodeEditor.CaretNodeChanged += nodeId =>
        {
            if (nodeId is null)
            {
                return;
            }

            SetSelectedNode(nodeId, SyncSource.CodeEditor);
        };

        // When selected node changes, update property editor
        IDisposable selectedNodeClearSubscription = this.WhenAnyValue(x => x.SelectedNodeId)
            .Subscribe(id =>
            {
                if (id is null)
                {
                    PropertyEditor.Categories.Clear();
                    PropertyEditor.FlatProperties.Clear();
                    PropertyEditor.Events.Clear();
                    PropertyEditor.SelectedTypeName = null;
                }
            });
        _disposables.Add(selectedNodeClearSubscription);

        IDisposable selectedNodeLoadSubscription = this.WhenAnyValue(x => x.SelectedNodeId)
            .Where(id => id is not null)
            .Subscribe(id =>
            {
                MutableAstNode? node = NodeMap.FindById(id!.Value);
                if (node is MutableAstObjectNode objNode)
                {
                    DesignItem item = new(objNode);
                    PropertyEditor.LoadFromDesignItem(item);
                }
            });
        _disposables.Add(selectedNodeLoadSubscription);

        // Sync selection to design surface and code editor caret
        IDisposable selectedNodeSyncSubscription = this.WhenAnyValue(x => x.SelectedNodeId)
            .Subscribe(id =>
            {
                SyncSource source = SelectionSource;
                if (id is null)
                {
                    if (source != SyncSource.DesignSurface)
                    {
                        DesignSurface.ClearSelectionFromSync();
                    }
                    return;
                }

                MutableAstNode? node = NodeMap.FindById(id.Value);
                if (node is MutableAstObjectNode objNode)
                {
                    int? offset = CodeEditor.GetOffsetForNode(objNode);
                    if (offset is not null)
                    {
                        if (source != SyncSource.CodeEditor)
                        {
                            CodeEditor.SetCaretOffsetFromSync(offset.Value);
                        }
                    }

                    if (source != SyncSource.DesignSurface)
                    {
                        DesignSurface.SelectByAstNodeIdFromSync(id.Value);
                    }
                }
            });
        _disposables.Add(selectedNodeSyncSubscription);

        // Listen for sync events to update trees
        IDisposable syncEventsSubscription = SyncEngine.SyncEvents
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                // After any sync, commit pending changes as an undo batch
                SyncEngine.CommitUndoBatch("Sync");

                // Rebuild the design surface from the updated AST
                DesignSurface.RequestRebuild();
            });
        _disposables.Add(syncEventsSubscription);

        SaveCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            string? text = SyncEngine.CurrentText;
            if (text is not null)
            {
                await System.IO.File.WriteAllTextAsync(FilePath, text);
                IsModified = false;
                CodeEditor.IsModified = false;
            }
        });

        CloseCommand = ReactiveCommand.Create(() => { /* Handled by MainWindowViewModel.CloseDocument */ });

        DesignViewCommand = ReactiveCommand.Create(() => { ViewMode = DocumentViewMode.Design; });
        CodeViewCommand = ReactiveCommand.Create(() => { ViewMode = DocumentViewMode.Code; });
        SplitViewCommand = ReactiveCommand.Create(() => { ViewMode = DocumentViewMode.Split; });
    }

    /// <summary>
    /// Loads the document from disk.
    /// </summary>
    public async System.Threading.Tasks.Task LoadAsync()
    {
        if (System.IO.File.Exists(FilePath))
        {
            string text = await System.IO.File.ReadAllTextAsync(FilePath);
            await SyncEngine.LoadAsync(text);
            CodeEditor.SetTextSilently(text);

            // Ensure the design surface rebuilds after loading
            DesignSurface.RequestRebuild();
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        PropertyEditor.Dispose();
        CodeEditor.Dispose();
        SyncEngine.Dispose();
    }
}

/// <summary>
/// ViewModel for a non-XAML text document.
/// </summary>
public sealed class TextDocumentViewModel : ReactiveObject, IEditorDocumentViewModel
{
    private readonly CompositeDisposable _disposables = new();
    private bool _suppressTextChanged;

    public string FilePath { get; }

    public string FileName => System.IO.Path.GetFileName(FilePath);

    public AvaloniaEdit.Document.TextDocument Document { get; } = new();

    [Reactive]
    public int CaretOffset { get; set; }

    [Reactive]
    public int CurrentLine { get; set; } = 1;

    [Reactive]
    public int CurrentColumn { get; set; } = 1;

    [Reactive]
    public bool IsModified { get; set; }

    [Reactive]
    public bool WordWrap { get; set; }

    [Reactive]
    public bool ShowLineNumbers { get; set; } = true;

    [Reactive]
    public double FontSize { get; set; } = 14.0;

    public string? LanguageId { get; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public TextDocumentViewModel(string filePath)
    {
        FilePath = filePath;
        LanguageId = GetLanguageIdForFile(filePath);

        IDisposable textChangedSubscription = Observable.FromEventPattern<EventHandler, EventArgs>(
                h => Document.TextChanged += h,
                h => Document.TextChanged -= h)
            .Where(_ => !_suppressTextChanged)
            .Subscribe(_ => IsModified = true);
        _disposables.Add(textChangedSubscription);

        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
    }

    public async System.Threading.Tasks.Task LoadAsync()
    {
        if (!System.IO.File.Exists(FilePath))
        {
            return;
        }

        string text = await System.IO.File.ReadAllTextAsync(FilePath);
        _suppressTextChanged = true;
        try
        {
            Document.Text = text;
            IsModified = false;
        }
        finally
        {
            _suppressTextChanged = false;
        }
    }

    private async System.Threading.Tasks.Task SaveAsync()
    {
        await System.IO.File.WriteAllTextAsync(FilePath, Document.Text);
        IsModified = false;
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    private static string? GetLanguageIdForFile(string filePath)
    {
        string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".axaml" => "xml",
            ".xaml" => "xml",
            ".xml" => "xml",
            ".csproj" => "xml",
            ".props" => "xml",
            ".targets" => "xml",
            ".cs" => "csharp",
            ".fs" => "fsharp",
            ".vb" => "vb",
            ".json" => "json",
            ".yml" => "yaml",
            ".yaml" => "yaml",
            ".md" => "markdown",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".css" => "css",
            ".scss" => "scss",
            ".less" => "less",
            ".html" => "html",
            ".htm" => "html",
            ".sln" => "ini",
            _ => null
        };
    }
}

/// <summary>
/// Specifies the document view mode.
/// </summary>
public enum DocumentViewMode
{
    /// <summary>Design surface only.</summary>
    Design,

    /// <summary>Code editor only.</summary>
    Code,

    /// <summary>Split view (design + code).</summary>
    Split
}

/// <summary>
/// ViewModel for a toolbox item.
/// </summary>
public sealed class ToolboxItemViewModel : ReactiveObject
{
    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the full type name (including namespace).
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the XML namespace.
    /// </summary>
    public string XmlNamespace { get; }

    /// <summary>
    /// Gets the category.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets or sets whether this item is visible after filtering.
    /// </summary>
    [Reactive]
    public bool IsVisible { get; set; } = true;

    public ToolboxItemViewModel(string displayName, string typeName, string xmlNamespace, string category)
    {
        DisplayName = displayName;
        TypeName = typeName;
        XmlNamespace = xmlNamespace;
        Category = category;
    }
}

/// <summary>
/// ViewModel for the toolbox panel.
/// </summary>
public sealed class ToolboxViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    /// <summary>
    /// Gets or sets the search/filter text.
    /// </summary>
    [Reactive]
    public string? SearchText { get; set; }

    /// <summary>
    /// Gets all toolbox items.
    /// </summary>
    public ObservableCollection<ToolboxItemViewModel> Items { get; } = new();

    public ToolboxViewModel()
    {
        // Register default Avalonia controls
        RegisterDefaultControls();

        // Filter items on search
        IDisposable filterSubscription = this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(filter =>
            {
                foreach (ToolboxItemViewModel item in Items)
                {
                    item.IsVisible = string.IsNullOrEmpty(filter) ||
                                     item.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase);
                }
            });
        _disposables.Add(filterSubscription);
    }

    /// <summary>
    /// Releases all subscriptions.
    /// </summary>
    public void Dispose() => _disposables.Dispose();

    private void RegisterDefaultControls()
    {
        const string ns = "https://github.com/avaloniaui";

        // Layout
        Items.Add(new("Grid", "Grid", ns, "Layout"));
        Items.Add(new("StackPanel", "StackPanel", ns, "Layout"));
        Items.Add(new("WrapPanel", "WrapPanel", ns, "Layout"));
        Items.Add(new("DockPanel", "DockPanel", ns, "Layout"));
        Items.Add(new("Canvas", "Canvas", ns, "Layout"));
        Items.Add(new("UniformGrid", "UniformGrid", ns, "Layout"));
        Items.Add(new("ScrollViewer", "ScrollViewer", ns, "Layout"));
        Items.Add(new("Border", "Border", ns, "Layout"));
        Items.Add(new("Viewbox", "Viewbox", ns, "Layout"));
        Items.Add(new("Panel", "Panel", ns, "Layout"));

        // Controls
        Items.Add(new("Button", "Button", ns, "Controls"));
        Items.Add(new("TextBlock", "TextBlock", ns, "Controls"));
        Items.Add(new("TextBox", "TextBox", ns, "Controls"));
        Items.Add(new("CheckBox", "CheckBox", ns, "Controls"));
        Items.Add(new("RadioButton", "RadioButton", ns, "Controls"));
        Items.Add(new("ComboBox", "ComboBox", ns, "Controls"));
        Items.Add(new("ListBox", "ListBox", ns, "Controls"));
        Items.Add(new("Slider", "Slider", ns, "Controls"));
        Items.Add(new("ProgressBar", "ProgressBar", ns, "Controls"));
        Items.Add(new("Image", "Image", ns, "Controls"));
        Items.Add(new("Menu", "Menu", ns, "Controls"));
        Items.Add(new("MenuItem", "MenuItem", ns, "Controls"));
        Items.Add(new("TabControl", "TabControl", ns, "Controls"));
        Items.Add(new("TabItem", "TabItem", ns, "Controls"));
        Items.Add(new("Expander", "Expander", ns, "Controls"));
        Items.Add(new("TreeView", "TreeView", ns, "Controls"));
        Items.Add(new("DataGrid", "DataGrid", ns, "Controls"));
        Items.Add(new("Calendar", "Calendar", ns, "Controls"));
        Items.Add(new("DatePicker", "DatePicker", ns, "Controls"));
        Items.Add(new("TimePicker", "TimePicker", ns, "Controls"));
        Items.Add(new("NumericUpDown", "NumericUpDown", ns, "Controls"));
        Items.Add(new("ToggleSwitch", "ToggleSwitch", ns, "Controls"));
        Items.Add(new("SplitView", "SplitView", ns, "Controls"));
    }
}

/// <summary>
/// ViewModel for the output/diagnostics panel.
/// </summary>
public sealed class OutputViewModel : ReactiveObject
{
    /// <summary>
    /// Gets the output messages.
    /// </summary>
    public ObservableCollection<OutputMessage> Messages { get; } = new();

    /// <summary>
    /// Gets or sets the active filter (All, Errors, Warnings).
    /// </summary>
    [Reactive]
    public string ActiveFilter { get; set; } = "All";

    /// <summary>
    /// Gets the error count.
    /// </summary>
    [Reactive]
    public int ErrorCount { get; set; }

    /// <summary>
    /// Gets the warning count.
    /// </summary>
    [Reactive]
    public int WarningCount { get; set; }

    /// <summary>
    /// Command to clear the output.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }

    public OutputViewModel()
    {
        ClearCommand = ReactiveCommand.Create(() =>
        {
            Messages.Clear();
            ErrorCount = 0;
            WarningCount = 0;
        });
    }

    /// <summary>
    /// Adds a plain output message.
    /// </summary>
    public void AddMessage(string level, string text)
    {
        Messages.Add(new OutputMessage(level, text, 0, 0, false));
    }

    /// <summary>
    /// Adds a diagnostic as an output message.
    /// </summary>
    public void AddDiagnostic(XamlDiagnostic diagnostic)
    {
        Messages.Add(new OutputMessage(
            diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => "Error",
                DiagnosticSeverity.Warning => "Warning",
                _ => "Info"
            },
            diagnostic.Message,
            diagnostic.Line,
            diagnostic.Column,
            true));

        if (diagnostic.Severity == DiagnosticSeverity.Error)
        {
            ErrorCount++;
        }
        else if (diagnostic.Severity == DiagnosticSeverity.Warning)
        {
            WarningCount++;
        }
    }

    /// <summary>
    /// Replaces existing diagnostics with the provided list.
    /// </summary>
    public void ReplaceDiagnostics(IReadOnlyList<XamlDiagnostic> diagnostics)
    {
        for (int i = Messages.Count - 1; i >= 0; i--)
        {
            if (Messages[i].IsDiagnostic)
            {
                Messages.RemoveAt(i);
            }
        }

        ErrorCount = 0;
        WarningCount = 0;

        foreach (XamlDiagnostic diagnostic in diagnostics)
        {
            AddDiagnostic(diagnostic);
        }
    }
}

/// <summary>
/// A message in the output panel.
/// </summary>
public sealed record OutputMessage(
    string Level,
    string Text,
    int Line,
    int Column,
    bool IsDiagnostic = false);

/// <summary>
/// Specifies how XAML changes are saved to disk.
/// </summary>
public enum SaveBehavior
{
    /// <summary>Save automatically after changes.</summary>
    AutoSave,

    /// <summary>Save only when invoked manually.</summary>
    SaveManually,

    /// <summary>Do not save changes to disk.</summary>
    NoSaving
}

/// <summary>
/// Main window ViewModel orchestrating the docking layout and document management.
/// </summary>
public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private bool _suppressTreeSelectionSync;
    private string? _clipboard;
    private readonly HashSet<Guid> _visualExpandedIds = new();
    private readonly HashSet<Guid> _logicalExpandedIds = new();
    private readonly IWorkspaceService? _workspaceService;
    private readonly ITypeMetadataService? _metadataService;
    private readonly Dictionary<IEditorDocumentViewModel, IDisposable> _autoSaveSubscriptions = new();
    private WorkspaceAssemblyResolver? _assemblyResolver;
    private WorkspaceModel? _workspace;
    private string? _workspacePath;
    private readonly Dictionary<string, IDockable> _dockDocuments = new(StringComparer.OrdinalIgnoreCase);
    private bool _isClosingFromDock;

    /// <summary>
    /// Gets the open documents.
    /// </summary>
    public ObservableCollection<IEditorDocumentViewModel> Documents { get; } = new();

    /// <summary>
    /// Gets or sets the active document.
    /// </summary>
    [Reactive]
    public IEditorDocumentViewModel? ActiveDocument { get; set; }

    [Reactive]
    public DesignerDocumentViewModel? ActiveDesignerDocument { get; private set; }

    [Reactive]
    public TextDocumentViewModel? ActiveTextDocument { get; private set; }

    /// <summary>
    /// Gets the toolbox ViewModel.
    /// </summary>
    public ToolboxViewModel Toolbox { get; } = new();

    /// <summary>
    /// Gets the solution explorer ViewModel.
    /// </summary>
    public SolutionExplorerViewModel SolutionExplorer { get; } = new();

    /// <summary>
    /// Gets the output ViewModel.
    /// </summary>
    public OutputViewModel Output { get; } = new();

    /// <summary>
    /// Gets or sets the save behavior for XAML changes.
    /// </summary>
    [Reactive]
    public SaveBehavior SaveBehavior { get; set; } = SaveBehavior.SaveManually;

    public bool IsAutoSaveSelected => SaveBehavior == SaveBehavior.AutoSave;
    public bool IsManualSaveSelected => SaveBehavior == SaveBehavior.SaveManually;
    public bool IsNoSaveSelected => SaveBehavior == SaveBehavior.NoSaving;

    /// <summary>
    /// Gets the visual tree grid ViewModel for the active document.
    /// </summary>
    public VisualTreeGridViewModel VisualTree { get; } = new();

    /// <summary>
    /// Gets the logical tree grid ViewModel for the active document.
    /// </summary>
    public LogicalTreeGridViewModel LogicalTree { get; } = new();

    /// <summary>
    /// Gets the collaboration panel ViewModel.
    /// </summary>
    public CollaborationPanelViewModel Collaboration { get; } = new();

    /// <summary>
    /// Gets the dock factory.
    /// </summary>
    public XamlEditorDockFactory DockFactory { get; }

    /// <summary>
    /// Gets the active dock layout.
    /// </summary>
    [Reactive]
    public IRootDock? DockLayout { get; private set; }

    /// <summary>
    /// Gets or sets the application title.
    /// </summary>
    [Reactive]
    public string Title { get; set; } = "XAML Visual Editor";

    /// <summary>
    /// Gets or sets the status bar text.
    /// </summary>
    [Reactive]
    public string StatusText { get; set; } = "Ready";

    /// <summary>
    /// Gets the collaboration status text for the status bar.
    /// </summary>
    [Reactive]
    public string CollaborationStatusText { get; set; } = string.Empty;

    /// <summary>
    /// Gets the recent files list.
    /// </summary>
    public ObservableCollection<string> RecentFiles { get; } = new();

    /// <summary>
    /// Gets whether a workspace is currently loaded.
    /// </summary>
    [Reactive]
    public bool HasWorkspace { get; private set; }

    /// <summary>
    /// Interaction for opening a file dialog.
    /// </summary>
    public Interaction<Unit, string?> OpenFileInteraction { get; } = new();

    /// <summary>
    /// Interaction for saving a file dialog.
    /// </summary>
    public Interaction<string, string?> SaveFileInteraction { get; } = new();

    // Panel visibility
    [Reactive] public bool IsToolboxVisible { get; set; } = true;
    [Reactive] public bool IsPropertiesVisible { get; set; } = true;
    [Reactive] public bool IsVisualTreeVisible { get; set; } = true;
    [Reactive] public bool IsLogicalTreeVisible { get; set; } = true;
    [Reactive] public bool IsOutputVisible { get; set; } = true;
    [Reactive] public bool IsCollaborationVisible { get; set; }

    // File Commands
    public ReactiveCommand<Unit, Unit> NewDocumentCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenDocumentCommand { get; }
    public ReactiveCommand<string, Unit> OpenPathCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveDocumentCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAllCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    // Save behavior commands
    public ReactiveCommand<Unit, Unit> SetAutoSaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SetManualSaveCommand { get; }
    public ReactiveCommand<Unit, Unit> SetNoSaveCommand { get; }

    // Edit Commands
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }
    public ReactiveCommand<Unit, Unit> CutCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyCommand { get; }
    public ReactiveCommand<Unit, Unit> PasteCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }

    // View Commands
    public ReactiveCommand<Unit, Unit> ToggleToolboxCommand { get; }
    public ReactiveCommand<Unit, Unit> TogglePropertiesCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleVisualTreeCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleLogicalTreeCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleOutputCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCollaborationCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetLayoutCommand { get; }

    // Help Commands
    public ReactiveCommand<Unit, Unit> AboutCommand { get; }

    // Workspace Commands
    public ReactiveCommand<Unit, Unit> RestoreWorkspaceCommand { get; }
    public ReactiveCommand<Unit, Unit> BuildWorkspaceCommand { get; }
    public ReactiveCommand<Unit, Unit> RebuildWorkspaceCommand { get; }
    public ReactiveCommand<Unit, Unit> CleanWorkspaceCommand { get; }

    /// <summary>
    /// Command to close a specific document (used by tab close button).
    /// </summary>
    public ReactiveCommand<IEditorDocumentViewModel, Unit> CloseDocumentCommand { get; }

    public MainWindowViewModel(IWorkspaceService? workspaceService = null, ITypeMetadataService? metadataService = null)
    {
        _workspaceService = workspaceService;
        _metadataService = metadataService;

        DockFactory = new XamlEditorDockFactory(this);
        IRootDock layout = XamlEditorDockFactory.LoadLayout() ?? DockFactory.CreateDefaultLayout();
        DockFactory.InitLayout(layout);
        DockFactory.ConfigureToolViewModels(layout);
        DockLayout = layout;
        WireDockEvents();

        _dockDocuments.Clear();
        foreach (IEditorDocumentViewModel doc in Documents)
        {
            AddDocumentToDock(doc);
        }

        // File commands
        NewDocumentCommand = ReactiveCommand.CreateFromTask(NewDocumentAsync);
        OpenDocumentCommand = ReactiveCommand.CreateFromTask(OpenDocumentAsync);
        OpenPathCommand = ReactiveCommand.CreateFromTask<string>(OpenFileAsync);

        IObservable<bool> hasActiveDoc = this.WhenAnyValue(x => x.ActiveDocument).Select(d => d is not null);
        IObservable<bool> hasDesignerDoc = this.WhenAnyValue(x => x.ActiveDesignerDocument).Select(d => d is not null);
        SaveDocumentCommand = ReactiveCommand.CreateFromTask(SaveActiveDocumentAsync, hasActiveDoc);
        SaveAllCommand = ReactiveCommand.CreateFromTask(SaveAllAsync);
        ExitCommand = ReactiveCommand.Create(() =>
        {
            if (Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        });

        SetAutoSaveCommand = ReactiveCommand.Create(() =>
        {
            SaveBehavior = SaveBehavior.AutoSave;
            StatusText = "Save mode: Auto Save";
        });
        SetManualSaveCommand = ReactiveCommand.Create(() =>
        {
            SaveBehavior = SaveBehavior.SaveManually;
            StatusText = "Save mode: Save Manually";
        });
        SetNoSaveCommand = ReactiveCommand.Create(() =>
        {
            SaveBehavior = SaveBehavior.NoSaving;
            StatusText = "Save mode: No Saving";
        });

        // Edit commands
        UndoCommand = ReactiveCommand.Create(() =>
        {
            ActiveDesignerDocument?.SyncEngine.Undo();
        }, hasDesignerDoc);

        RedoCommand = ReactiveCommand.Create(() =>
        {
            ActiveDesignerDocument?.SyncEngine.Redo();
        }, hasDesignerDoc);

        CutCommand = ReactiveCommand.Create(() =>
        {
            if (ActiveDesignerDocument?.SelectedNodeId is { } nodeId)
            {
                MutableAstObjectNode? node = ActiveDesignerDocument.NodeMap.FindById(nodeId) as MutableAstObjectNode;
                if (node?.Parent is MutableAstObjectNode parent)
                {
                    XamlSerializationService ser = new();
                    MutableAstDocument tempDoc = new() { Root = node };
                    _clipboard = ser.Serialize(tempDoc);
                    parent.Children.Remove(node);
                    ActiveDesignerDocument.SetSelectedNode(null, ActiveDesignerDocument.SelectionSource);
                    ActiveDesignerDocument.SyncEngine.NotifyAstChanged(
                        ActiveDesignerDocument.SyncEngine.CurrentDocument!, SyncSource.DesignSurface);
                }
            }
        }, hasDesignerDoc);

        CopyCommand = ReactiveCommand.Create(() =>
        {
            if (ActiveDesignerDocument?.SelectedNodeId is { } nodeId)
            {
                MutableAstObjectNode? node = ActiveDesignerDocument.NodeMap.FindById(nodeId) as MutableAstObjectNode;
                if (node is not null)
                {
                    XamlSerializationService ser = new();
                    MutableAstDocument tempDoc = new() { Root = node };
                    _clipboard = ser.Serialize(tempDoc);
                }
            }
        }, hasDesignerDoc);

        PasteCommand = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrEmpty(_clipboard) && ActiveDesignerDocument is not null)
            {
                // Parse the clipboard XAML and add to selected parent or root
                XamlParsingService parser = new();
                ParseResult result = parser.Parse(_clipboard);
                if (result.Document is MutableAstDocument pastedDoc && pastedDoc.Root is not null)
                {
                    MutableAstObjectNode? parent = null;
                    if (ActiveDesignerDocument.SelectedNodeId is { } selId)
                    {
                        parent = ActiveDesignerDocument.NodeMap.FindById(selId) as MutableAstObjectNode;
                    }

                    parent ??= ActiveDesignerDocument.SyncEngine.CurrentDocument?.Root;

                    if (parent is not null)
                    {
                        parent.Children.Add(pastedDoc.Root);
                        ActiveDesignerDocument.SyncEngine.NotifyAstChanged(
                            ActiveDesignerDocument.SyncEngine.CurrentDocument!, SyncSource.DesignSurface);
                    }
                }
            }
        }, hasDesignerDoc);

        DeleteCommand = ReactiveCommand.Create(() =>
        {
            if (ActiveDesignerDocument?.SelectedNodeId is { } nodeId)
            {
                MutableAstObjectNode? node = ActiveDesignerDocument.NodeMap.FindById(nodeId) as MutableAstObjectNode;
                if (node?.Parent is MutableAstObjectNode parent)
                {
                    parent.Children.Remove(node);
                    ActiveDesignerDocument.SetSelectedNode(null, ActiveDesignerDocument.SelectionSource);
                    ActiveDesignerDocument.SyncEngine.NotifyAstChanged(
                        ActiveDesignerDocument.SyncEngine.CurrentDocument!, SyncSource.DesignSurface);
                }
            }
        }, hasDesignerDoc);

        SelectAllCommand = ReactiveCommand.Create(() =>
        {
            if (ActiveDesignerDocument is not null)
            {
                ActiveDesignerDocument.CodeEditor.SelectAll();
            }
        }, hasDesignerDoc);

        // View commands
        ToggleToolboxCommand = ReactiveCommand.Create(() =>
        {
            IsToolboxVisible = !IsToolboxVisible;
            SetDockableVisibility("Toolbox", IsToolboxVisible);
            SetDockableVisibility("SolutionExplorer", IsToolboxVisible);
        });
        TogglePropertiesCommand = ReactiveCommand.Create(() =>
        {
            IsPropertiesVisible = !IsPropertiesVisible;
            SetDockableVisibility("Properties", IsPropertiesVisible);
        });
        ToggleVisualTreeCommand = ReactiveCommand.Create(() =>
        {
            IsVisualTreeVisible = !IsVisualTreeVisible;
            SetDockableVisibility("VisualTree", IsVisualTreeVisible);
        });
        ToggleLogicalTreeCommand = ReactiveCommand.Create(() =>
        {
            IsLogicalTreeVisible = !IsLogicalTreeVisible;
            SetDockableVisibility("LogicalTree", IsLogicalTreeVisible);
        });
        ToggleOutputCommand = ReactiveCommand.Create(() =>
        {
            IsOutputVisible = !IsOutputVisible;
            SetDockableVisibility("Output", IsOutputVisible);
        });
        ToggleCollaborationCommand = ReactiveCommand.Create(() =>
        {
            IsCollaborationVisible = !IsCollaborationVisible;
            SetDockableVisibility("Collaboration", IsCollaborationVisible);
        });
        ResetLayoutCommand = ReactiveCommand.Create(ResetLayout);

        // Help commands
        AboutCommand = ReactiveCommand.Create(() =>
        {
            StatusText = "XAML Visual Editor — Avalonia-based WYSIWYG XAML Editor";
        });

        IObservable<bool> hasWorkspace = this.WhenAnyValue(x => x.HasWorkspace);
        RestoreWorkspaceCommand = ReactiveCommand.CreateFromTask(
            () => RunWorkspaceCommandAsync("restore"),
            hasWorkspace);
        BuildWorkspaceCommand = ReactiveCommand.CreateFromTask(
            () => RunWorkspaceCommandAsync("build"),
            hasWorkspace);
        RebuildWorkspaceCommand = ReactiveCommand.CreateFromTask(
            () => RunWorkspaceCommandAsync("build -t:Rebuild"),
            hasWorkspace);
        CleanWorkspaceCommand = ReactiveCommand.CreateFromTask(
            () => RunWorkspaceCommandAsync("clean"),
            hasWorkspace);

        // Close document command (used by tab close buttons)
        CloseDocumentCommand = ReactiveCommand.Create<IEditorDocumentViewModel>(doc =>
        {
            CloseDocument(doc);
        });

        // Update trees when active document changes
        IDisposable activeDocumentTreesSubscription = this.WhenAnyValue(x => x.ActiveDesignerDocument)
            .Subscribe(doc => UpdateTrees(doc));
        _disposables.Add(activeDocumentTreesSubscription);

        IDisposable activeDocumentDockSubscription = this.WhenAnyValue(x => x.ActiveDocument)
            .Where(doc => doc is not null)
            .Subscribe(doc => SetActiveDockDocument(doc!));
        _disposables.Add(activeDocumentDockSubscription);

        // Refresh trees on sync events from active document (Switch unsubscribes from previous)
        IDisposable activeDocumentSyncSubscription = this.WhenAnyValue(x => x.ActiveDesignerDocument)
            .Where(d => d is not null)
            .Select(d => d!.SyncEngine.SyncEvents.Select(_ => d))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(d => UpdateTrees(d));
        _disposables.Add(activeDocumentSyncSubscription);

        IDisposable activeDocumentDiagnosticsSubscription = this.WhenAnyValue(x => x.ActiveDesignerDocument)
            .Select(doc => doc is null
                ? Observable.Return<IReadOnlyList<XamlDiagnostic>>(Array.Empty<XamlDiagnostic>())
                : doc.SyncEngine.SyncEvents.Select(e => e.Diagnostics ?? Array.Empty<XamlDiagnostic>()))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(diags =>
            {
                Output.ReplaceDiagnostics(diags);
                LogDiagnosticsSummary(diags);
            });
        _disposables.Add(activeDocumentDiagnosticsSubscription);

        IDisposable saveBehaviorSubscription = this.WhenAnyValue(x => x.SaveBehavior)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsAutoSaveSelected));
                this.RaisePropertyChanged(nameof(IsManualSaveSelected));
                this.RaisePropertyChanged(nameof(IsNoSaveSelected));
            });
        _disposables.Add(saveBehaviorSubscription);

        // Sync tree selection when active document selection changes
        IDisposable selectionSyncSubscription = this.WhenAnyValue(x => x.ActiveDesignerDocument)
            .Select(doc => doc is null
                ? Observable.Return<Guid?>(null)
                : doc.WhenAnyValue(d => d.SelectedNodeId))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(id => ApplySelectionToTrees(id));
        _disposables.Add(selectionSyncSubscription);

        // Sync grid selections back to the active document
        IDisposable visualTreeSelectionSubscription = this.WhenAnyValue(x => x.VisualTree.SelectedNode)
            .CombineLatest(this.WhenAnyValue(x => x.ActiveDesignerDocument), (node, doc) => (node, doc))
            .Where(t => t.doc is not null)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Where(_ => !_suppressTreeSelectionSync)
            .Subscribe(t => t.doc!.SetSelectedNode(t.node?.AstNodeId, SyncSource.TreeView));
        _disposables.Add(visualTreeSelectionSubscription);

        IDisposable logicalTreeSelectionSubscription = this.WhenAnyValue(x => x.LogicalTree.SelectedNode)
            .CombineLatest(this.WhenAnyValue(x => x.ActiveDesignerDocument), (node, doc) => (node, doc))
            .Where(t => t.doc is not null)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Where(_ => !_suppressTreeSelectionSync)
            .Subscribe(t => t.doc!.SetSelectedNode(t.node?.AstNodeId, SyncSource.TreeView));
        _disposables.Add(logicalTreeSelectionSubscription);

        // Update title when active document changes
        IDisposable titleUpdateSubscription = this.WhenAnyValue(x => x.ActiveDocument)
            .Select(doc => doc is not null ? $"XAML Visual Editor — {doc.FileName}" : "XAML Visual Editor")
            .Subscribe(t => Title = t);
        _disposables.Add(titleUpdateSubscription);

        // Update collaboration status
        IDisposable collaborationStatusSubscription = this.WhenAnyValue(x => x.Collaboration.IsSessionActive)
            .Select(active => active ? "● Collab Connected" : string.Empty)
            .Subscribe(s => CollaborationStatusText = s);
        _disposables.Add(collaborationStatusSubscription);

        SolutionExplorer.FileOpenRequested += path => { _ = OpenFileAsync(path); };

        IDisposable activeDocumentTypeSubscription = this.WhenAnyValue(x => x.ActiveDocument)
            .Subscribe(doc =>
            {
                ActiveDesignerDocument = doc as DesignerDocumentViewModel;
                ActiveTextDocument = doc as TextDocumentViewModel;
            });
        _disposables.Add(activeDocumentTypeSubscription);
    }

    private async System.Threading.Tasks.Task NewDocumentAsync()
    {
        string tempPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"Untitled-{Documents.Count + 1}.axaml");

        // Create a basic XAML file
        string defaultXaml = """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="NewDocument.MainView"
                         Width="400" Height="300">
                <Grid RowDefinitions="Auto,*,Auto">
                    <TextBlock Grid.Row="0"
                               Text="Hello, Avalonia!"
                               FontSize="24"
                               FontWeight="Bold"
                               HorizontalAlignment="Center"
                               Margin="0,16,0,8" />
                    <StackPanel Grid.Row="1"
                                Spacing="8"
                                HorizontalAlignment="Center"
                                VerticalAlignment="Center">
                        <TextBox Width="200" Watermark="Enter text here..." />
                        <Button Content="Click Me"
                                HorizontalAlignment="Center" />
                    </StackPanel>
                    <TextBlock Grid.Row="2"
                               Text="Status: Ready"
                               FontSize="12"
                               Margin="8"
                               Opacity="0.6" />
                </Grid>
            </UserControl>
            """;

        await System.IO.File.WriteAllTextAsync(tempPath, defaultXaml);

        DesignerDocumentViewModel doc = new(tempPath, _metadataService);
        Documents.Add(doc);
        ActiveDocument = doc;
        AddDocumentToDock(doc);
        AttachAutoSave(doc);

        try
        {
            await doc.LoadAsync();
            UpdateTrees(doc);
            StatusText = $"Created {doc.FileName}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to load new document: {ex.Message}");
            StatusText = $"Failed to create document: {ex.Message}";
        }
    }

    private async System.Threading.Tasks.Task OpenDocumentAsync()
    {
        try
        {
            string? filePath = await OpenFileInteraction.Handle(Unit.Default);
            if (!string.IsNullOrEmpty(filePath))
            {
                await OpenFileAsync(filePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Open document failed: {ex.Message}");
            StatusText = $"Open document failed: {ex.Message}";
        }
    }

    private async System.Threading.Tasks.Task SaveActiveDocumentAsync()
    {
        if (SaveBehavior == SaveBehavior.NoSaving)
        {
            StatusText = "Saving is disabled";
            return;
        }

        if (ActiveDocument is not null)
        {
            await ActiveDocument.SaveCommand.Execute();
            StatusText = $"Saved {ActiveDocument.FileName}";
        }
    }

    private async System.Threading.Tasks.Task SaveAllAsync()
    {
        if (SaveBehavior == SaveBehavior.NoSaving)
        {
            StatusText = "Saving is disabled";
            return;
        }

        foreach (IEditorDocumentViewModel doc in Documents)
        {
            if (doc.IsModified)
            {
                await doc.SaveCommand.Execute();
            }
        }
        StatusText = "All documents saved";
    }

    private void AttachAutoSave(DesignerDocumentViewModel doc)
    {
        if (_autoSaveSubscriptions.ContainsKey(doc))
        {
            return;
        }

        IDisposable subscription = doc.SyncEngine.SyncEvents
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(evt =>
            {
                _ = AutoSaveAsync(doc);
            });

        _autoSaveSubscriptions[doc] = subscription;
    }

    private void AttachAutoSave(TextDocumentViewModel doc)
    {
        if (_autoSaveSubscriptions.ContainsKey(doc))
        {
            return;
        }

        IDisposable subscription = Observable.FromEventPattern<EventHandler, EventArgs>(
                h => doc.Document.TextChanged += h,
                h => doc.Document.TextChanged -= h)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(evt =>
            {
                _ = AutoSaveAsync(doc);
            });

        _autoSaveSubscriptions[doc] = subscription;
    }

    private void DetachAutoSave(IEditorDocumentViewModel doc)
    {
        if (_autoSaveSubscriptions.Remove(doc, out IDisposable? subscription))
        {
            subscription.Dispose();
        }
    }

    private async System.Threading.Tasks.Task AutoSaveAsync(DesignerDocumentViewModel doc)
    {
        if (SaveBehavior != SaveBehavior.AutoSave)
        {
            return;
        }

        if (!doc.IsModified)
        {
            return;
        }

        await doc.SaveCommand.Execute();
        StatusText = $"Auto-saved {doc.FileName}";
    }

    private async System.Threading.Tasks.Task AutoSaveAsync(TextDocumentViewModel doc)
    {
        if (SaveBehavior != SaveBehavior.AutoSave)
        {
            return;
        }

        if (!doc.IsModified)
        {
            return;
        }

        await doc.SaveCommand.Execute();
        StatusText = $"Auto-saved {doc.FileName}";
    }

    private void ResetLayout()
    {
        IsToolboxVisible = true;
        IsPropertiesVisible = true;
        IsVisualTreeVisible = true;
        IsLogicalTreeVisible = true;
        IsOutputVisible = true;
        IsCollaborationVisible = false;

        IRootDock layout = DockFactory.CreateDefaultLayout();
        DockFactory.InitLayout(layout);
        DockFactory.ConfigureToolViewModels(layout);
        DockLayout = layout;

        // Delete persisted layout so it reloads default on next start
        try
        {
            string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            string layoutPath = System.IO.Path.Combine(appData, "XamlVisualEditor", "dock-layout.json");
            if (System.IO.File.Exists(layoutPath))
            {
                System.IO.File.Delete(layoutPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to delete layout file: {ex.Message}");
        }

        StatusText = "Layout reset";
    }

    private void UpdateTrees(DesignerDocumentViewModel? doc)
    {
        if (doc is null)
        {
            VisualTree.SetRoot(null);
            LogicalTree.SetRoot(null);
            return;
        }

        // Preserve expansion state before rebuilding
        _visualExpandedIds.Clear();
        VisualTree.Root?.CollectExpandedIds(_visualExpandedIds);
        _logicalExpandedIds.Clear();
        LogicalTree.Root?.CollectExpandedIds(_logicalExpandedIds);

        MutableAstDocument? astDoc = doc.SyncEngine.CurrentDocument;
        VisualTreeNodeViewModel? visualRoot = VisualTreeNodeViewModel.FromAstDocument(astDoc);
        LogicalTreeNodeViewModel? logicalRoot = LogicalTreeNodeViewModel.FromAstDocument(astDoc);

        if (visualRoot is not null && _visualExpandedIds.Count > 0)
        {
            visualRoot.ApplyExpandedIds(_visualExpandedIds);
        }

        if (logicalRoot is not null && _logicalExpandedIds.Count > 0)
        {
            logicalRoot.ApplyExpandedIds(_logicalExpandedIds);
        }

        VisualTree.SetRoot(visualRoot);
        LogicalTree.SetRoot(logicalRoot);

        ApplySelectionToTrees(doc.SelectedNodeId);
    }

    private void ApplySelectionToTrees(Guid? nodeId)
    {
        _suppressTreeSelectionSync = true;
        try
        {
            if (nodeId is null)
            {
                VisualTree.SelectNode(null);
                LogicalTree.SelectNode(null);
                return;
            }

            VisualTreeNodeViewModel? visualNode = VisualTree.Root?.FindByNodeId(nodeId.Value);
            if (visualNode is not null)
            {
                visualNode.ExpandPathToNode(nodeId.Value);
            }
            VisualTree.SelectNode(visualNode);

            LogicalTreeNodeViewModel? logicalNode = LogicalTree.Root?.FindByNodeId(nodeId.Value);
            if (logicalNode is not null)
            {
                logicalNode.ExpandPathToNode(nodeId.Value);
            }
            LogicalTree.SelectNode(logicalNode);
        }
        finally
        {
            _suppressTreeSelectionSync = false;
        }
    }

    private void AddDocumentToDock(IEditorDocumentViewModel document)
    {
        if (DockLayout is null)
        {
            return;
        }

        IDockable? dockDoc = document switch
        {
            DesignerDocumentViewModel designer => DockFactory.AddDocument(DockLayout, designer),
            TextDocumentViewModel text => DockFactory.AddTextDocument(DockLayout, text),
            _ => null
        };

        if (dockDoc is not null)
        {
            _dockDocuments[document.FilePath] = dockDoc;
        }
    }

    private void SetActiveDockDocument(IEditorDocumentViewModel document)
    {
        if (DockLayout is null)
        {
            return;
        }

        if (_dockDocuments.TryGetValue(document.FilePath, out IDockable? dockDoc))
        {
            DockFactory.SetActiveDockable(dockDoc);
        }
    }

    private void CloseDockDocument(IEditorDocumentViewModel document)
    {
        if (DockLayout is null)
        {
            return;
        }

        if (_dockDocuments.TryGetValue(document.FilePath, out IDockable? dockDoc))
        {
            DockFactory.CloseDockable(dockDoc);
            _dockDocuments.Remove(document.FilePath);
        }
    }

    private void WireDockEvents()
    {
        DockFactory.ActiveDockableChanged += OnActiveDockableChanged;
        DockFactory.DockableClosed += OnDockableClosed;
    }

    private void OnActiveDockableChanged(object? sender, ActiveDockableChangedEventArgs e)
    {
        switch (e.Dockable)
        {
            case DesignerDocument designer:
                ActiveDocument = designer.DocumentViewModel;
                break;
            case TextDocument text:
                ActiveDocument = text.DocumentViewModel;
                break;
        }
    }

    private void OnDockableClosed(object? sender, DockableClosedEventArgs e)
    {
        if (e.Dockable is DesignerDocument designer)
        {
            _isClosingFromDock = true;
            try
            {
                CloseDocument(designer.DocumentViewModel);
            }
            finally
            {
                _isClosingFromDock = false;
            }
            return;
        }

        if (e.Dockable is TextDocument text)
        {
            _isClosingFromDock = true;
            try
            {
                CloseDocument(text.DocumentViewModel);
            }
            finally
            {
                _isClosingFromDock = false;
            }
        }
    }

    private void SetDockableVisibility(string id, bool isVisible)
    {
        if (DockLayout is null)
        {
            return;
        }

        IDockable? dockable = XamlEditorDockFactory.FindDockable<IDockable>(DockLayout, id);
        if (dockable is null)
        {
            return;
        }

        if (isVisible)
        {
            DockFactory.RestoreDockable(dockable);
            DockFactory.SetActiveDockable(dockable);
        }
        else
        {
            DockFactory.HideDockable(dockable);
        }
    }

    /// <summary>
    /// Closes a document.
    /// </summary>
    public void CloseDocument(IEditorDocumentViewModel doc)
    {
        if (!_isClosingFromDock)
        {
            CloseDockDocument(doc);
        }

        Documents.Remove(doc);
        if (ReferenceEquals(ActiveDocument, doc))
        {
            ActiveDocument = Documents.FirstOrDefault();
        }
        DetachAutoSave(doc);
        doc.Dispose();
        StatusText = $"Closed {doc.FileName}";
    }

    /// <summary>
    /// Opens a specific file.
    /// </summary>
    public async System.Threading.Tasks.Task OpenFileAsync(string filePath)
    {
        string extension = System.IO.Path.GetExtension(filePath);
        if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            await LoadWorkspaceAsync(filePath);
            return;
        }

        if (extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".axaml", StringComparison.OrdinalIgnoreCase))
        {
            await TryLoadWorkspaceForXamlAsync(filePath);
        }

        // Check if already open
        IEditorDocumentViewModel? existing = Documents.FirstOrDefault(d => d.FilePath == filePath);
        if (existing is not null)
        {
            ActiveDocument = existing;
            SetActiveDockDocument(existing);
            return;
        }

        if (IsXamlFile(filePath))
        {
            DesignerDocumentViewModel doc = new(filePath, _metadataService);
            Documents.Add(doc);
            ActiveDocument = doc;
            AddDocumentToDock(doc);
            AttachAutoSave(doc);
            await doc.LoadAsync();
            UpdateTrees(doc);
        }
        else
        {
            TextDocumentViewModel doc = new(filePath);
            Documents.Add(doc);
            ActiveDocument = doc;
            AddDocumentToDock(doc);
            AttachAutoSave(doc);
            await doc.LoadAsync();
            UpdateTrees(null);
        }

        // Add to recent files
        if (!RecentFiles.Contains(filePath))
        {
            RecentFiles.Insert(0, filePath);
            if (RecentFiles.Count > 10)
            {
                RecentFiles.RemoveAt(RecentFiles.Count - 1);
            }
        }

        StatusText = $"Opened {System.IO.Path.GetFileName(filePath)}";
    }

    private async System.Threading.Tasks.Task LoadWorkspaceAsync(string workspacePath)
    {
        if (_workspaceService is null || _metadataService is null)
        {
            StatusText = "Workspace services are unavailable";
            return;
        }

        string extension = System.IO.Path.GetExtension(workspacePath);
        string workspaceName = System.IO.Path.GetFileName(workspacePath);
        StatusText = $"Loading workspace {workspaceName}";
        LogOutput("Info", $"Loading workspace: {workspacePath}");

        WorkspaceModel workspace;
        try
        {
            workspace = extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                ? await _workspaceService.LoadSolutionAsync(workspacePath)
                : await _workspaceService.LoadProjectAsync(workspacePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Workspace load failed: {ex.Message}");
            Console.WriteLine($"Workspace load failed: {ex.Message}");
            LogOutput("Error", $"Workspace load failed: {ex.Message}");
            LogWorkspaceEnvironment(workspacePath);
            StatusText = $"Workspace load failed: {ex.Message}";
            HasWorkspace = false;
            return;
        }

        _workspace = workspace;
        _workspacePath = workspacePath;
        HasWorkspace = true;

        string? name = System.IO.Path.GetFileNameWithoutExtension(workspacePath);
        SolutionExplorer.LoadWorkspace(workspace, name);
        SolutionExplorer.IsVisible = true;

        bool hasAnyProjectOutputs;
        bool hasMissingProjectOutputs;
        HashSet<string> assemblyPaths = CollectWorkspaceAssemblies(
            workspace,
            out hasAnyProjectOutputs,
            out hasMissingProjectOutputs);
        if (!hasAnyProjectOutputs || hasMissingProjectOutputs)
        {
            await RunDotNetCommandAsync(workspacePath, "restore");
            await RunDotNetCommandAsync(workspacePath, "build");
            assemblyPaths = CollectWorkspaceAssemblies(
                workspace,
                out hasAnyProjectOutputs,
                out hasMissingProjectOutputs);
        }

        if (assemblyPaths.Count > 0)
        {
            ApplyAssemblyResolver(assemblyPaths);
            _metadataService.LoadAssemblies(assemblyPaths);
            RefreshOpenDocumentsAfterMetadataLoad();
        }

        StatusText = $"Loaded workspace {name}";
        LogOutput("Info", $"Loaded workspace: {name}");
    }

    private async System.Threading.Tasks.Task TryLoadWorkspaceForXamlAsync(string xamlFilePath)
    {
        if (_workspace is not null && WorkspaceContainsFile(_workspace, xamlFilePath))
        {
            return;
        }

        string? workspacePath = FindWorkspacePathForFile(xamlFilePath);
        if (string.IsNullOrEmpty(workspacePath))
        {
            return;
        }

        if (string.Equals(_workspacePath, workspacePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await LoadWorkspaceAsync(workspacePath);
    }

    private static bool WorkspaceContainsFile(WorkspaceModel workspace, string xamlFilePath)
    {
        foreach (ProjectModel project in workspace.Projects)
        {
            foreach (XamlFileModel file in project.XamlFiles)
            {
                if (string.Equals(file.FilePath, xamlFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsXamlFile(string filePath)
    {
        return filePath.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)
            || filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindWorkspacePathForFile(string filePath)
    {
        string? currentDir = System.IO.Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(currentDir))
        {
            string? solutionPath = GetFirstFile(currentDir, "*.sln");
            if (string.IsNullOrEmpty(solutionPath))
            {
                solutionPath = GetFirstFile(currentDir, "*.slnx");
            }
            if (!string.IsNullOrEmpty(solutionPath))
            {
                return solutionPath;
            }

            string? projectPath = GetFirstFile(currentDir, "*.csproj");
            if (!string.IsNullOrEmpty(projectPath))
            {
                return projectPath;
            }

            currentDir = System.IO.Path.GetDirectoryName(currentDir);
        }

        return null;
    }

    private static string? GetFirstFile(string directory, string pattern)
    {
        try
        {
            foreach (string file in System.IO.Directory.EnumerateFiles(directory, pattern))
            {
                return file;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to enumerate '{directory}': {ex.Message}");
        }

        return null;
    }

    private async System.Threading.Tasks.Task RunDotNetCommandAsync(string workspacePath, string command)
    {
        string? workingDirectory = System.IO.Path.GetDirectoryName(workspacePath);
        if (string.IsNullOrEmpty(workingDirectory))
        {
            return;
        }

        LogOutput("Info", $"dotnet {command} {workspacePath}");

        System.Diagnostics.ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            Arguments = $"{command} \"{workspacePath}\"",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using System.Diagnostics.Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to run dotnet {command}: {ex.Message}");
            StatusText = $"dotnet {command} failed";
            LogOutput("Error", $"dotnet {command} failed: {ex.Message}");
            return;
        }

        string stdOut = await process.StandardOutput.ReadToEndAsync();
        string stdErr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            System.Diagnostics.Trace.TraceWarning($"dotnet {command} failed: {stdErr}");
            StatusText = $"dotnet {command} failed";
            if (!string.IsNullOrWhiteSpace(stdErr))
            {
                LogOutput("Error", stdErr.Trim());
            }
        }
        else if (!string.IsNullOrWhiteSpace(stdOut))
        {
            System.Diagnostics.Trace.TraceInformation(stdOut);
            LogOutput("Info", stdOut.Trim());
        }
    }

    private void LogOutput(string level, string message)
    {
        Output.AddMessage(level, message);
        Console.WriteLine(message);
    }

    private void LogDiagnosticsSummary(IReadOnlyList<XamlDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        int errorCount = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        int warningCount = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        if (errorCount == 0 && warningCount == 0)
        {
            return;
        }

        LogOutput("Info", $"XAML diagnostics: {errorCount} error(s), {warningCount} warning(s)");
    }

    private async System.Threading.Tasks.Task RunWorkspaceCommandAsync(string command)
    {
        if (string.IsNullOrEmpty(_workspacePath) || _workspace is null)
        {
            return;
        }

        StatusText = $"Running dotnet {command}...";
        await RunDotNetCommandAsync(_workspacePath, command);

        if (_metadataService is null)
        {
            return;
        }

        bool hasAnyProjectOutputs;
        bool hasMissingProjectOutputs;
        HashSet<string> assemblyPaths = CollectWorkspaceAssemblies(
            _workspace,
            out hasAnyProjectOutputs,
            out hasMissingProjectOutputs);

        if (assemblyPaths.Count > 0)
        {
            ApplyAssemblyResolver(assemblyPaths);
            _metadataService.LoadAssemblies(assemblyPaths);
            RefreshOpenDocumentsAfterMetadataLoad();
        }
    }

    private void RefreshOpenDocumentsAfterMetadataLoad()
    {
        foreach (IEditorDocumentViewModel doc in Documents)
        {
            if (doc is DesignerDocumentViewModel designer)
            {
                designer.DesignSurface.RequestRebuild();
            }
        }
    }

    private static HashSet<string> CollectWorkspaceAssemblies(
        WorkspaceModel workspace,
        out bool hasAnyProjectOutputs,
        out bool hasMissingProjectOutputs)
    {
        HashSet<string> assemblyPaths = new(StringComparer.OrdinalIgnoreCase);
        hasAnyProjectOutputs = false;
        hasMissingProjectOutputs = false;

        foreach (ProjectModel project in workspace.Projects)
        {
            foreach (AssemblyReference reference in project.References)
            {
                if (System.IO.File.Exists(reference.Path))
                {
                    assemblyPaths.Add(reference.Path);
                }
            }

            List<string> outputs = FindProjectOutputs(project).ToList();
            if (outputs.Count == 0)
            {
                hasMissingProjectOutputs = true;
            }

            foreach (string outputPath in outputs)
            {
                if (System.IO.File.Exists(outputPath))
                {
                    assemblyPaths.Add(outputPath);
                    hasAnyProjectOutputs = true;
                }
            }
        }

        return assemblyPaths;
    }

    private static IEnumerable<string> FindProjectOutputs(ProjectModel project)
    {
        if (string.IsNullOrWhiteSpace(project.ProjectPath))
        {
            return Array.Empty<string>();
        }

        string? projectDir = System.IO.Path.GetDirectoryName(project.ProjectPath);
        if (string.IsNullOrEmpty(projectDir))
        {
            return Array.Empty<string>();
        }

        string[] roots =
        {
            System.IO.Path.Combine(projectDir, "bin", "Debug"),
            System.IO.Path.Combine(projectDir, "bin", "Release"),
            System.IO.Path.Combine(projectDir, "obj", "Debug"),
            System.IO.Path.Combine(projectDir, "obj", "Release")
        };

        List<string> outputs = new();
        foreach (string root in roots)
        {
            if (!System.IO.Directory.Exists(root))
            {
                continue;
            }

            try
            {
                foreach (string file in System.IO.Directory.EnumerateFiles(root, "*.dll", System.IO.SearchOption.AllDirectories))
                {
                    outputs.Add(file);
                }

                foreach (string file in System.IO.Directory.EnumerateFiles(root, "*.exe", System.IO.SearchOption.AllDirectories))
                {
                    outputs.Add(file);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"Failed to enumerate outputs from '{root}': {ex.Message}");
            }
        }

        return outputs;
    }

    private static void LogWorkspaceEnvironment(string workspacePath)
    {
        try
        {
            Console.WriteLine($"Workspace path: {workspacePath}");
            Console.WriteLine($"Working directory: {System.IO.Directory.GetCurrentDirectory()}");
            Console.WriteLine($"DOTNET_ROOT: {Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? string.Empty}");
            Console.WriteLine($"PATH: {Environment.GetEnvironmentVariable("PATH") ?? string.Empty}");

            string info = GetDotNetInfo();
            if (!string.IsNullOrWhiteSpace(info))
            {
                Console.WriteLine("dotnet --info:\n" + info);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to log workspace environment: {ex.Message}");
        }
    }

    private static string GetDotNetInfo()
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "dotnet",
                Arguments = "--info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = new() { StartInfo = startInfo };
            process.Start();
            string stdOut = process.StandardOutput.ReadToEnd();
            string stdErr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(stdOut))
            {
                return stdOut.Trim();
            }

            return stdErr.Trim();
        }
        catch (Exception ex)
        {
            return $"dotnet --info failed: {ex.Message}";
        }
    }

    private void ApplyAssemblyResolver(IEnumerable<string> assemblyPaths)
    {
        _assemblyResolver?.Dispose();
        _assemblyResolver = new WorkspaceAssemblyResolver(assemblyPaths);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        Collaboration.Dispose();
        _assemblyResolver?.Dispose();

        foreach (IDisposable subscription in _autoSaveSubscriptions.Values)
        {
            subscription.Dispose();
        }
        _autoSaveSubscriptions.Clear();

        DockFactory.ActiveDockableChanged -= OnActiveDockableChanged;
        DockFactory.DockableClosed -= OnDockableClosed;

        if (DockLayout is not null)
        {
            XamlEditorDockFactory.SaveLayout(DockLayout);
        }

        foreach (IEditorDocumentViewModel doc in Documents)
        {
            doc.Dispose();
        }
    }
}

// ==============================================
// Solution Explorer ViewModel
// ==============================================

/// <summary>
/// Represents a node in the Solution Explorer tree.
/// </summary>
public sealed class SolutionExplorerNodeViewModel : ReactiveObject
{
    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets the icon identifier (emoji for simplicity).</summary>
    public string Icon { get; }

    /// <summary>Gets the full path (for files) or project path (for projects).</summary>
    public string? FullPath { get; }

    /// <summary>Gets the node kind.</summary>
    public SolutionExplorerNodeKind Kind { get; }

    /// <summary>Gets the child nodes.</summary>
    public ObservableCollection<SolutionExplorerNodeViewModel> Children { get; } = new();

    /// <summary>Gets or sets whether this node is expanded.</summary>
    [Reactive]
    public bool IsExpanded { get; set; }

    /// <summary>Gets or sets whether this node is selected.</summary>
    [Reactive]
    public bool IsSelected { get; set; }

    /// <summary>Raised when a file node is double-clicked (opened).</summary>
    public event Action<string>? FileOpened;

    /// <summary>
    /// Command to open the associated file.
    /// </summary>
    public ReactiveCommand<Unit, Unit>? OpenCommand { get; }

    public SolutionExplorerNodeViewModel(string name, string icon, SolutionExplorerNodeKind kind, string? fullPath = null)
    {
        Name = name;
        Icon = icon;
        Kind = kind;
        FullPath = fullPath;

        if ((kind == SolutionExplorerNodeKind.XamlFile || kind == SolutionExplorerNodeKind.File) && fullPath is not null)
        {
            OpenCommand = ReactiveCommand.Create(() => FileOpened?.Invoke(fullPath));
        }
    }

    /// <summary>
    /// Creates a Solution Explorer tree from a WorkspaceModel.
    /// </summary>
    public static SolutionExplorerNodeViewModel FromWorkspace(WorkspaceModel workspace, string? solutionName = null)
    {
        string rootName = solutionName ?? "Solution";
        SolutionExplorerNodeViewModel root = new(rootName, "🗂", SolutionExplorerNodeKind.Solution);
        root.IsExpanded = true;

        Dictionary<string, SolutionExplorerNodeViewModel> folderNodes =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (ProjectModel project in workspace.Projects)
        {
            SolutionExplorerNodeViewModel projectNode = new(
                project.Name, "📦", SolutionExplorerNodeKind.Project, project.ProjectPath);
            projectNode.IsExpanded = true;

            SolutionExplorerNodeViewModel projectParent = root;
            if (workspace.ProjectFolders.TryGetValue(project.ProjectPath, out string? folderPath) &&
                !string.IsNullOrWhiteSpace(folderPath))
            {
                projectParent = EnsureFolderPath(root, folderNodes, folderPath);
            }

            AddProjectFiles(projectNode, project.Files);

            // References folder
            if (project.References.Count > 0)
            {
                SolutionExplorerNodeViewModel refsFolder = new("References", "📚", SolutionExplorerNodeKind.Folder);

                foreach (AssemblyReference asmRef in project.References)
                {
                    SolutionExplorerNodeViewModel refNode = new(
                        asmRef.Name, "🔗", SolutionExplorerNodeKind.Reference, asmRef.Path);
                    refsFolder.Children.Add(refNode);
                }

                projectNode.Children.Add(refsFolder);
            }

            projectParent.Children.Add(projectNode);
        }

        return root;
    }

    private static void AddProjectFiles(
        SolutionExplorerNodeViewModel projectNode,
        IReadOnlyList<ProjectFileModel> files)
    {
        Dictionary<string, SolutionExplorerNodeViewModel> folders =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (ProjectFileModel file in files)
        {
            string relative = file.RelativePath.Replace('\\', '/');
            string[] parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            SolutionExplorerNodeViewModel current = projectNode;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string folderPath = string.Join('/', parts.Take(i + 1));
                if (!folders.TryGetValue(folderPath, out SolutionExplorerNodeViewModel? folderNode))
                {
                    folderNode = new SolutionExplorerNodeViewModel(parts[i], "📁", SolutionExplorerNodeKind.Folder);
                    folders[folderPath] = folderNode;
                    current.Children.Add(folderNode);
                }

                current = folderNode;
            }

            string fileName = parts[^1];
            SolutionExplorerNodeKind kind = IsXamlFile(file.FilePath)
                ? SolutionExplorerNodeKind.XamlFile
                : SolutionExplorerNodeKind.File;

            SolutionExplorerNodeViewModel fileNode = new(
                fileName, GetFileIcon(file.FilePath), kind, file.FilePath);
            current.Children.Add(fileNode);
        }
    }

    private static SolutionExplorerNodeViewModel EnsureFolderPath(
        SolutionExplorerNodeViewModel root,
        Dictionary<string, SolutionExplorerNodeViewModel> folderNodes,
        string folderPath)
    {
        string[] segments = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        SolutionExplorerNodeViewModel current = root;
        string currentPath = string.Empty;

        foreach (string segment in segments)
        {
            currentPath = string.IsNullOrEmpty(currentPath)
                ? segment
                : currentPath + "/" + segment;

            if (!folderNodes.TryGetValue(currentPath, out SolutionExplorerNodeViewModel? node))
            {
                node = new SolutionExplorerNodeViewModel(segment, "📁", SolutionExplorerNodeKind.SolutionFolder);
                node.IsExpanded = true;
                folderNodes[currentPath] = node;
                current.Children.Add(node);
            }

            current = node;
        }

        return current;
    }

    private static bool IsXamlFile(string filePath)
    {
        return filePath.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)
            || filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFileIcon(string filePath)
    {
        string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".axaml" => "📄",
            ".xaml" => "📄",
            ".cs" => "🧩",
            ".json" => "🧾",
            ".xml" => "🧾",
            ".md" => "📝",
            _ => "📄"
        };
    }
}

/// <summary>
/// Kinds of nodes in the Solution Explorer tree.
/// </summary>
public enum SolutionExplorerNodeKind
{
    /// <summary>Solution root.</summary>
    Solution,

    /// <summary>Project.</summary>
    Project,

    /// <summary>Folder.</summary>
    Folder,

    /// <summary>Solution folder.</summary>
    SolutionFolder,

    /// <summary>XAML file.</summary>
    XamlFile,

    /// <summary>Generic file.</summary>
    File,

    /// <summary>Assembly reference.</summary>
    Reference
}

/// <summary>
/// ViewModel for the Solution Explorer tool panel.
/// </summary>
public sealed class SolutionExplorerViewModel : ReactiveObject
{
    /// <summary>Gets or sets the root node of the solution tree.</summary>
    [Reactive]
    public SolutionExplorerNodeViewModel? Root { get; set; }

    /// <summary>Gets or sets whether the panel is visible.</summary>
    [Reactive]
    public bool IsVisible { get; set; }

    /// <summary>Gets or sets the filter text.</summary>
    [Reactive]
    public string? FilterText { get; set; }

    /// <summary>Raised when a XAML file is opened from the tree.</summary>
    public event Action<string>? FileOpenRequested;

    public SolutionExplorerViewModel()
    {
        // Filter support (future enhancement)
        this.WhenAnyValue(x => x.FilterText)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => { /* Filtering can be added here */ });
    }

    /// <summary>
    /// Loads a workspace into the Solution Explorer.
    /// </summary>
    public void LoadWorkspace(WorkspaceModel workspace, string? solutionName = null)
    {
        Root = SolutionExplorerNodeViewModel.FromWorkspace(workspace, solutionName);
        WireFileOpen(Root);
    }

    private void WireFileOpen(SolutionExplorerNodeViewModel node)
    {
        node.FileOpened += path => FileOpenRequested?.Invoke(path);

        foreach (SolutionExplorerNodeViewModel child in node.Children)
        {
            WireFileOpen(child);
        }
    }
}
