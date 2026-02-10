using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AvaloniaEdit.Document;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Sync;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Xaml.Intellisense;

namespace XamlVisualEditor.CodeEditor;

/// <summary>
/// ViewModel for the XAML code editor panel.
/// Wraps an AvaloniaEdit TextDocument and integrates with the sync engine.
/// </summary>
public sealed class CodeEditorViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private readonly SyncEngine _syncEngine;
    private readonly CompletionProviderRegistry _completionRegistry;
    private readonly ITypeMetadataService? _metadataService;
    private readonly ILogger<CodeEditorViewModel> _logger;
    private bool _suppressTextChanged;
    private int _ignoreCaretUpdates;

    /// <summary>
    /// Gets the text document for AvaloniaEdit.
    /// </summary>
    public TextDocument Document { get; } = new();

    /// <summary>
    /// Gets or sets the caret offset.
    /// </summary>
    [Reactive]
    public int CaretOffset { get; set; }

    /// <summary>
    /// Gets the current line number (1-based).
    /// </summary>
    [Reactive]
    public int CurrentLine { get; set; } = 1;

    /// <summary>
    /// Gets the current column number (1-based).
    /// </summary>
    [Reactive]
    public int CurrentColumn { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether the document has been modified.
    /// </summary>
    [Reactive]
    public bool IsModified { get; set; }

    /// <summary>
    /// Gets or sets whether word wrap is enabled.
    /// </summary>
    [Reactive]
    public bool WordWrap { get; set; }

    /// <summary>
    /// Gets or sets whether line numbers are shown.
    /// </summary>
    [Reactive]
    public bool ShowLineNumbers { get; set; } = true;

    /// <summary>
    /// Gets or sets the font size.
    /// </summary>
    [Reactive]
    public double FontSize { get; set; } = 14.0;

    /// <summary>
    /// Gets the AST node ID at the current caret position, if any.
    /// </summary>
    [Reactive]
    public Guid? CaretNodeId { get; set; }

    /// <summary>
    /// Gets or sets the selection start offset.
    /// </summary>
    [Reactive]
    public int SelectionStart { get; set; }

    /// <summary>
    /// Gets or sets the selection length.
    /// </summary>
    [Reactive]
    public int SelectionLength { get; set; }

    /// <summary>
    /// Gets or sets the current execution line number (1-based).
    /// </summary>
    [Reactive]
    public int? ExecutionLine { get; set; }

    /// <summary>
    /// Gets the version used to refresh breakpoint line highlights.
    /// </summary>
    [Reactive]
    public int BreakpointHighlightVersion { get; private set; }

    /// <summary>
    /// Gets the diagnostics for the current document.
    /// </summary>
    public ObservableCollection<XamlDiagnostic> Diagnostics { get; } = new();

    /// <summary>
    /// Gets the diagnostic colorizer for rendering error squiggles.
    /// </summary>
    public DiagnosticColorizer DiagnosticColorizer { get; } = new();

    /// <summary>
    /// Gets the execution line colorizer for debug line highlighting.
    /// </summary>
    public ExecutionLineColorizer ExecutionLineColorizer { get; } = new();

    /// <summary>
    /// Gets the breakpoint line colorizer for gutter line highlighting.
    /// </summary>
    public BreakpointLineColorizer BreakpointLineColorizer { get; } = new();

    /// <summary>
    /// Gets the completion items for the popup.
    /// </summary>
    public ObservableCollection<CompletionItem> CompletionItems { get; } = new();

    /// <summary>
    /// Gets or sets whether the completion popup is visible.
    /// </summary>
    [Reactive]
    public bool IsCompletionOpen { get; set; }

    /// <summary>
    /// Fires when the caret moves to a new AST node.
    /// </summary>
    public event Action<Guid?>? CaretNodeChanged;

    /// <summary>
    /// Command to trigger completion.
    /// </summary>
    public ReactiveCommand<Unit, Unit> TriggerCompletionCommand { get; }

    /// <summary>
    /// Command to undo.
    /// </summary>
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }

    /// <summary>
    /// Command to redo.
    /// </summary>
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }

    /// <summary>
    /// Command to format (pretty-print) the document.
    /// </summary>
    public ReactiveCommand<Unit, Unit> FormatDocumentCommand { get; }

    /// <summary>
    /// Command to increase font size.
    /// </summary>
    public ReactiveCommand<Unit, Unit> IncreaseFontSizeCommand { get; }

    /// <summary>
    /// Command to decrease font size.
    /// </summary>
    public ReactiveCommand<Unit, Unit> DecreaseFontSizeCommand { get; }

    public CodeEditorViewModel(
        SyncEngine syncEngine,
        CompletionProviderRegistry completionRegistry,
        ITypeMetadataService? metadataService = null,
        ILogger<CodeEditorViewModel>? logger = null)
    {
        _syncEngine = syncEngine;
        _completionRegistry = completionRegistry;
        _metadataService = metadataService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CodeEditorViewModel>.Instance;

        TriggerCompletionCommand = ReactiveCommand.Create(TriggerCompletion);
        UndoCommand = ReactiveCommand.Create(() => Document.UndoStack.Undo());
        RedoCommand = ReactiveCommand.Create(() => Document.UndoStack.Redo());
        FormatDocumentCommand = ReactiveCommand.Create(FormatDocument);
        IncreaseFontSizeCommand = ReactiveCommand.Create(() => { FontSize = Math.Min(FontSize + 2, 48); });
        DecreaseFontSizeCommand = ReactiveCommand.Create(() => { FontSize = Math.Max(FontSize - 2, 8); });

        // Subscribe to text changes from the document (debounced)
        IDisposable textChangedSubscription = Observable.FromEventPattern<EventHandler, EventArgs>(
                h => Document.TextChanged += h,
                h => Document.TextChanged -= h)
            .Where(_ => !_suppressTextChanged)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => OnTextChanged());
        _disposables.Add(textChangedSubscription);

        // Subscribe to sync events to receive AST→text updates
        IDisposable syncSubscription = _syncEngine.SyncEvents
            .Where(e => e.Source == SyncSource.DesignSurface || e.Source == SyncSource.Collaboration)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(OnSyncEvent);
        _disposables.Add(syncSubscription);

        // Map caret offset to line/column and AST node
        IDisposable caretSubscription = this.WhenAnyValue(x => x.CaretOffset)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(offset => UpdateCaretPosition(offset));
        _disposables.Add(caretSubscription);
    }

    public void BumpBreakpointHighlightVersion()
    {
        BreakpointHighlightVersion++;
    }

    /// <summary>
    /// Sets the document text without triggering a sync back.
    /// </summary>
    public void SetTextSilently(string text)
    {
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

    /// <summary>
    /// Selects all text in the document.
    /// </summary>
    public void SelectAll()
    {
        SelectionStart = 0;
        SelectionLength = Document.TextLength;
    }

    /// <summary>
    /// Sets the caret offset safely within document bounds.
    /// </summary>
    public void SetCaretOffset(int offset)
    {
        if (Document.TextLength == 0)
        {
            CaretOffset = 0;
            return;
        }

        int clamped = Math.Clamp(offset, 0, Document.TextLength);
        CaretOffset = clamped;
    }

    public int GetOffsetForLineColumn(int line, int column)
    {
        int lineNumber = Math.Clamp(line, 1, Document.LineCount);
        DocumentLine docLine = Document.GetLineByNumber(lineNumber);
        int col = Math.Max(1, column);
        int offset = Math.Min(docLine.EndOffset, docLine.Offset + col - 1);
        return offset;
    }

    /// <summary>
    /// Sets the caret offset without emitting a caret node change event.
    /// </summary>
    public void SetCaretOffsetFromSync(int offset)
    {
        if (Document.TextLength == 0)
        {
            _ignoreCaretUpdates++;
            CaretOffset = 0;
            return;
        }

        int clamped = Math.Clamp(offset, 0, Document.TextLength);
        _ignoreCaretUpdates++;
        CaretOffset = clamped;
    }

    /// <summary>
    /// Gets the document offset for the start of the specified AST node.
    /// </summary>
    public int? GetOffsetForNode(MutableAstObjectNode node)
    {
        if (node.Line <= 0)
        {
            return null;
        }

        int lineNumber = Math.Clamp(node.Line, 1, Document.LineCount);
        DocumentLine line = Document.GetLineByNumber(lineNumber);
        int column = Math.Max(1, node.Column);
        int offset = Math.Min(line.EndOffset, line.Offset + column - 1);
        return offset;
    }

    private void OnTextChanged()
    {
        IsModified = true;

        string text = Document.Text;

        // Commit any pending AST changes as an undo batch before re-parsing
        _syncEngine.CommitUndoBatch("Text edit");

        _syncEngine.NotifyTextChanged(text, SyncSource.CodeEditor);
    }

    private void OnSyncEvent(SyncEvent syncEvent)
    {
        if (syncEvent.XamlText is not null)
        {
            int caretOffset = CaretOffset;
            SetTextSilently(syncEvent.XamlText);
            SetCaretOffsetFromSync(caretOffset);
        }

        // Update diagnostics
        Diagnostics.Clear();
        if (syncEvent.Diagnostics is not null)
        {
            foreach (XamlDiagnostic diagnostic in syncEvent.Diagnostics)
            {
                Diagnostics.Add(diagnostic);
            }

            // Update colorizer for squiggly underlines
            DiagnosticColorizer.UpdateDiagnostics(syncEvent.Diagnostics);
        }
        else
        {
            DiagnosticColorizer.UpdateDiagnostics(Array.Empty<XamlDiagnostic>());
        }
    }

    private void UpdateCaretPosition(int offset)
    {
        try
        {
            bool suppressCaretEvent = false;
            if (_ignoreCaretUpdates > 0)
            {
                _ignoreCaretUpdates--;
                suppressCaretEvent = true;
            }

            if (offset < 0 || offset > Document.TextLength)
            {
                return;
            }

            DocumentLine line = Document.GetLineByOffset(offset);
            CurrentLine = line.LineNumber;
            CurrentColumn = offset - line.Offset + 1;

            // Map caret to AST node by line/column
            Guid? nodeId = FindNodeAtPosition(CurrentLine, CurrentColumn);
            if (nodeId != CaretNodeId)
            {
                CaretNodeId = nodeId;
                if (!suppressCaretEvent)
                {
                    CaretNodeChanged?.Invoke(nodeId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Caret position calculation error: {Message}", ex.Message);
        }
    }

    private Guid? FindNodeAtPosition(int line, int column)
    {
        MutableAstDocument? doc = _syncEngine.CurrentDocument;
        if (doc?.Root is null)
        {
            return null;
        }

        // Walk the AST to find the deepest node containing this position
        return FindNodeRecursive(doc.Root, line, column);
    }

    private static Guid? FindNodeRecursive(MutableAstObjectNode node, int line, int column)
    {
        // Check children first (deepest match wins)
        foreach (MutableAstNode child in node.Children)
        {
            if (child is MutableAstObjectNode childObj)
            {
                Guid? childResult = FindNodeRecursive(childObj, line, column);
                if (childResult is not null)
                {
                    return childResult;
                }
            }
        }

        // Check if position is within this node's range.
        // EndLine > 0 means we have precise range info; otherwise fall back to start-line-only check.
        if (node.Line > 0)
        {
            if (node.EndLine > 0)
            {
                // Precise range check
                if (line >= node.Line && line <= node.EndLine)
                {
                    return node.Id;
                }
            }
            else if (line >= node.Line)
            {
                return node.Id;
            }
        }

        return null;
    }

    private void TriggerCompletion()
    {
        CompletionItems.Clear();

        string text = Document.Text;
        int offset = CaretOffset;

        if (offset < 0 || offset > text.Length)
        {
            return;
        }

        CompletionContext context = BuildCompletionContext(text, offset, CompletionTrigger.Invoked, null);

        IReadOnlyList<CompletionItem> items = _completionRegistry.GetCompletions(context);

        foreach (CompletionItem item in items)
        {
            CompletionItems.Add(item);
        }

        IsCompletionOpen = CompletionItems.Count > 0;
    }

    private void FormatDocument()
    {
        // Re-serialize the current AST to get formatted output
        MutableAstDocument? doc = _syncEngine.CurrentDocument;
        if (doc is null)
        {
            return;
        }

        // Trigger a sync from current AST which will produce formatted XAML
        _syncEngine.NotifyAstChanged(doc, SyncSource.CodeEditor);
    }

    /// <summary>
    /// Applies a completion item to the document.
    /// </summary>
    public void ApplyCompletion(CompletionItem item)
    {
        if (item.InsertText is null)
        {
            return;
        }

        // Simple insertion at caret - a production editor would handle replacement ranges
        Document.Insert(CaretOffset, item.InsertText);
        IsCompletionOpen = false;
    }

    /// <summary>
    /// Gets completions for the given context, delegating to the provider registry.
    /// </summary>
    public IReadOnlyList<CompletionItem> GetCompletions(CompletionContext context)
    {
        CompletionContext prepared = EnsureMetadata(context);
        return _completionRegistry.GetCompletions(prepared);
    }

    private CompletionContext BuildCompletionContext(
        string text,
        int offset,
        CompletionTrigger trigger,
        char? triggerCharacter)
    {
        CompletionContext context = new()
        {
            TextBefore = text.Substring(0, offset),
            DocumentText = text,
            LanguageId = "xml",
            Offset = offset,
            Trigger = trigger,
            TriggerCharacter = triggerCharacter,
            Metadata = _metadataService
        };

        return context;
    }

    private CompletionContext EnsureMetadata(CompletionContext context)
    {
        if (context.Metadata is not null || _metadataService is null)
        {
            return context;
        }

        return new CompletionContext
        {
            Document = context.Document,
            Offset = context.Offset,
            TextBefore = context.TextBefore,
            DocumentText = context.DocumentText,
            FilePath = context.FilePath,
            LanguageId = context.LanguageId,
            Trigger = context.Trigger,
            TriggerCharacter = context.TriggerCharacter,
            Metadata = _metadataService
        };
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
