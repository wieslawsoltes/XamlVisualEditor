using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Xaml.Parsing;
using XamlVisualEditor.Xaml.Serialization;

namespace XamlVisualEditor.Sync;

/// <summary>
/// Central bi-directional sync engine that coordinates all mutation sources
/// (code editor, designer, property editor, tree view, collaboration) through
/// a single source-of-truth mutable AST.
/// </summary>
public sealed class SyncEngine : ReactiveObject, ISyncEngine
{
    private readonly IXamlParsingService _parser;
    private readonly IXamlSerializationService _serializer;
    private readonly AstNodeMap? _nodeMap;
    private readonly UndoRedoService _undoRedo;
    private readonly Subject<SyncEvent> _syncSubject = new();
    private readonly List<AstChange> _pendingChanges = new();
    private readonly object _syncLock = new();

    private MutableAstDocument? _document;
    private volatile bool _isSyncing;
    private volatile bool _isDisposed;
    private string _lastKnownText = string.Empty;

    /// <summary>
    /// Creates a new sync engine with the specified parsing and serialization services.
    /// </summary>
    public SyncEngine(IXamlParsingService parser, IXamlSerializationService serializer)
    {
        _parser = parser;
        _serializer = serializer;
        _undoRedo = new UndoRedoService();
    }

    /// <summary>
    /// Creates a new sync engine with the specified parsing, serialization services, and node map.
    /// </summary>
    public SyncEngine(IXamlParsingService parser, IXamlSerializationService serializer, AstNodeMap nodeMap)
    {
        _parser = parser;
        _serializer = serializer;
        _nodeMap = nodeMap;
        _undoRedo = new UndoRedoService();
    }

    /// <summary>
    /// Creates a new sync engine with the specified parsing, serialization services, node map, and undo/redo service.
    /// </summary>
    public SyncEngine(IXamlParsingService parser, IXamlSerializationService serializer, AstNodeMap nodeMap, UndoRedoService undoRedo)
    {
        _parser = parser;
        _serializer = serializer;
        _nodeMap = nodeMap;
        _undoRedo = undoRedo;
    }

    /// <inheritdoc />
    public IXamlDocumentModel? Document => _document;

    /// <summary>
    /// Gets the mutable AST document.
    /// </summary>
    public MutableAstDocument? CurrentDocument => _document;

    /// <summary>
    /// Gets the undo/redo service.
    /// </summary>
    public UndoRedoService UndoRedo => _undoRedo;

    /// <summary>
    /// Gets the current XAML text.
    /// </summary>
    public string? CurrentText => string.IsNullOrEmpty(_lastKnownText) ? null : _lastKnownText;

    /// <inheritdoc />
    public event Action<SyncEvent>? SyncCompleted;

    /// <summary>
    /// Gets an observable stream of sync events.
    /// </summary>
    public IObservable<SyncEvent> SyncEvents => _syncSubject.AsObservable();

    /// <inheritdoc />
    public Task LoadAsync(string xamlText, CancellationToken ct = default)
    {
        ParseResult result = _parser.Parse(xamlText, new XamlParserOptions
        {
            UseTolerantParser = false
        });

        if (result.Document is MutableAstDocument doc)
        {
            SetDocument(doc);
            _lastKnownText = xamlText;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string> SaveAsync(CancellationToken ct = default)
    {
        if (_document is null)
        {
            return Task.FromResult(string.Empty);
        }

        string text = _serializer.Serialize(_document);
        _lastKnownText = text;
        return Task.FromResult(text);
    }

    /// <inheritdoc />
    public void NotifyTextChanged(string newText, SyncSource source)
    {
        lock (_syncLock)
        {
            if (_isSyncing || _isDisposed) return;
            _isSyncing = true;
        }

        try
        {
            // Re-parse the text
            ParseResult result = _parser.Parse(newText, new XamlParserOptions
            {
                UseTolerantParser = true
            });

            if (result.Document is MutableAstDocument newDoc)
            {
                SetDocument(newDoc);
                _lastKnownText = newText;

                SyncEvent syncEvent = new()
                {
                    Source = source,
                    Changes = Array.Empty<AstChange>(),
                    UpdatedText = newText,
                    Diagnostics = result.Diagnostics
                };

                SyncCompleted?.Invoke(syncEvent);
                _syncSubject.OnNext(syncEvent);
            }
        }
        finally
        {
            lock (_syncLock)
            {
                _isSyncing = false;
            }
        }
    }

    /// <inheritdoc />
    public void NotifyAstChanged(AstChange change, SyncSource source)
    {
        lock (_syncLock)
        {
            if (_isSyncing || _isDisposed) return;
            _isSyncing = true;
        }

        try
        {
            _pendingChanges.Add(change);

            // Commit as an undoable operation
            string description = change switch
            {
                NodeAdded na => $"Add {na.NodeTypeName}",
                NodeRemoved => "Remove node",
                NodeMoved => "Move node",
                PropertyValueChanged pvc => $"Set {pvc.PropertyName}",
                TextContentChanged => "Edit text",
                _ => "Edit"
            };
            _undoRedo.CommitBatch(description);

            // Serialize the current AST to text
            if (_document is not null)
            {
                IReadOnlyList<TextEdit> edits = _serializer.ComputeMinimalEdits(
                    _document, _lastKnownText, _pendingChanges);

                // Apply edits to get new text
                string newText = ApplyEdits(_lastKnownText, edits);
                _lastKnownText = newText;

                SyncEvent syncEvent = new()
                {
                    Source = source,
                    Changes = _pendingChanges.ToList(),
                    UpdatedText = newText,
                    Diagnostics = Array.Empty<XamlDiagnostic>()
                };

                _pendingChanges.Clear();
                SyncCompleted?.Invoke(syncEvent);
                _syncSubject.OnNext(syncEvent);
            }
        }
        finally
        {
            lock (_syncLock)
            {
                _isSyncing = false;
            }
        }
    }

    /// <summary>
    /// Gets the current XAML text representation.
    /// </summary>
    public string GetCurrentText() => _lastKnownText;

    /// <summary>
    /// Notifies the sync engine that the AST document has changed (e.g., from designer or collaboration).
    /// Re-serializes the document to produce updated XAML text.
    /// </summary>
    public void NotifyAstChanged(MutableAstDocument document, SyncSource source)
    {
        lock (_syncLock)
        {
            if (_isSyncing || _isDisposed) return;
            _isSyncing = true;
        }

        try
        {
            if (document != _document)
            {
                SetDocument(document);
            }

            string newText = _serializer.Serialize(document);
            _lastKnownText = newText;

            SyncEvent syncEvent = new()
            {
                Source = source,
                Changes = Array.Empty<AstChange>(),
                UpdatedText = newText,
                Diagnostics = Array.Empty<XamlDiagnostic>()
            };

            SyncCompleted?.Invoke(syncEvent);
            _syncSubject.OnNext(syncEvent);
        }
        finally
        {
            lock (_syncLock)
            {
                _isSyncing = false;
            }
        }
    }

    private void SetDocument(MutableAstDocument doc)
    {
        if (_document is not null)
        {
            _document.Changed -= OnDocumentChanged;
        }

        _document = doc;
        _document.Changed += OnDocumentChanged;

        // Clear stale entries and register all nodes in the node map
        if (_nodeMap is not null)
        {
            _nodeMap.Clear();
            if (doc.Root is not null)
            {
                _nodeMap.RegisterTree(doc.Root);
            }
        }
    }

    private void OnDocumentChanged(AstChange change)
    {
        // Record changes for undo/redo when not replaying undo/redo
        _undoRedo.RecordChange(change);
    }

    /// <summary>
    /// Commits any pending AST changes as a single undoable operation.
    /// </summary>
    public void CommitUndoBatch(string description)
    {
        _undoRedo.CommitBatch(description);
    }

    /// <summary>
    /// Performs an undo operation: reverts the last committed batch of changes
    /// by re-serializing the AST after applying inverse changes.
    /// </summary>
    public void Undo()
    {
        IReadOnlyList<AstChange>? inverseChanges = _undoRedo.Undo();
        if (inverseChanges is null || _document is null)
        {
            return;
        }

        ApplyChangesToAst(inverseChanges);
        ResyncFromAst(SyncSource.CodeEditor);
    }

    /// <summary>
    /// Performs a redo operation: re-applies the last undone batch of changes.
    /// </summary>
    public void Redo()
    {
        IReadOnlyList<AstChange>? changes = _undoRedo.Redo();
        if (changes is null || _document is null)
        {
            return;
        }

        ApplyChangesToAst(changes);
        ResyncFromAst(SyncSource.CodeEditor);
    }

    private void ApplyChangesToAst(IReadOnlyList<AstChange> changes)
    {
        if (_document is null || _nodeMap is null) return;

        foreach (AstChange change in changes)
        {
            switch (change)
            {
                case PropertyValueChanged pvc:
                {
                    MutableAstNode? node = _nodeMap.FindById(pvc.NodeId);
                    if (node is MutableAstObjectNode objNode)
                    {
                        objNode.SetPropertyValue(pvc.PropertyName, pvc.NewValue);
                    }
                    break;
                }
                case TextContentChanged tcc:
                {
                    MutableAstNode? node = _nodeMap.FindById(tcc.NodeId);
                    if (node is MutableAstTextNode textNode)
                    {
                        textNode.Text = tcc.NewText;
                    }
                    break;
                }
                case NodeAdded added:
                {
                    MutableAstNode? parent = _nodeMap.FindById(added.ParentId);
                    if (parent is MutableAstObjectNode parentObj)
                    {
                        // Re-create a placeholder node with the original ID.
                        // In a full implementation the node would be deserialized from a snapshot.
                        MutableAstObjectNode newNode = new() { TypeName = added.NodeTypeName };
                        int index = Math.Min(added.Index, parentObj.Children.Count);
                        parentObj.Children.Insert(index, newNode);
                        _nodeMap.Register(newNode);
                    }
                    break;
                }
                case NodeRemoved removed:
                {
                    MutableAstNode? parent = _nodeMap.FindById(removed.ParentId);
                    if (parent is MutableAstObjectNode parentObj && removed.Index < parentObj.Children.Count)
                    {
                        MutableAstNode child = parentObj.Children[removed.Index];
                        parentObj.Children.RemoveAt(removed.Index);
                        _nodeMap.Unregister(child.Id);
                    }
                    break;
                }
                case NodeMoved moved:
                {
                    MutableAstNode? oldParent = _nodeMap.FindById(moved.OldParentId);
                    MutableAstNode? newParent = _nodeMap.FindById(moved.NewParentId);
                    MutableAstNode? movedNode = _nodeMap.FindById(moved.NodeId);
                    if (oldParent is MutableAstObjectNode oldParentObj &&
                        newParent is MutableAstObjectNode newParentObj &&
                        movedNode is not null)
                    {
                        oldParentObj.Children.Remove(movedNode);
                        int index = Math.Min(moved.NewIndex, newParentObj.Children.Count);
                        newParentObj.Children.Insert(index, movedNode);
                    }
                    break;
                }
            }
        }
    }

    private void ResyncFromAst(SyncSource source)
    {
        if (_document is null) return;

        string newText = _serializer.Serialize(_document);
        _lastKnownText = newText;

        SyncEvent syncEvent = new()
        {
            Source = source,
            Changes = Array.Empty<AstChange>(),
            UpdatedText = newText,
            Diagnostics = Array.Empty<XamlDiagnostic>()
        };

        SyncCompleted?.Invoke(syncEvent);
        _syncSubject.OnNext(syncEvent);
    }

    private static string ApplyEdits(string text, IReadOnlyList<TextEdit> edits)
    {
        if (edits.Count == 0) return text;

        // Apply edits in reverse offset order to maintain positions
        List<TextEdit> sorted = edits.OrderByDescending(e => e.Offset).ToList();
        System.Text.StringBuilder sb = new(text);

        foreach (TextEdit edit in sorted)
        {
            sb.Remove(edit.Offset, edit.Length);
            sb.Insert(edit.Offset, edit.NewText);
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
        }

        if (_document is not null)
        {
            _document.Changed -= OnDocumentChanged;
        }

        _undoRedo.Dispose();
        _syncSubject.OnCompleted();
        _syncSubject.Dispose();
    }
}
