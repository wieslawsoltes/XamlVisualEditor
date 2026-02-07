using XamlVisualEditor.Core;

namespace XamlVisualEditor.Sync;

/// <summary>
/// Manages undo/redo operations based on AST change records.
/// </summary>
public sealed class UndoRedoService : IDisposable
{
    private readonly Stack<UndoFrame> _undoStack = new();
    private readonly Stack<UndoFrame> _redoStack = new();
    private readonly List<AstChange> _currentBatch = new();
    private bool _isUndoRedoInProgress;
    private bool _isDisposed;

    /// <summary>
    /// Gets whether an undo operation is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether a redo operation is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Gets the number of items on the undo stack.
    /// </summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>
    /// Gets the number of items on the redo stack.
    /// </summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>
    /// Fires when the undo/redo state changes.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// Records an AST change for undo tracking.
    /// </summary>
    public void RecordChange(AstChange change)
    {
        if (_isUndoRedoInProgress || _isDisposed)
        {
            return;
        }

        _currentBatch.Add(change);
    }

    /// <summary>
    /// Commits the current batch of changes as a single undo frame.
    /// </summary>
    public void CommitBatch(string description)
    {
        if (_currentBatch.Count == 0 || _isDisposed)
        {
            return;
        }

        UndoFrame frame = new()
        {
            Description = description,
            Changes = _currentBatch.ToList()
        };

        _undoStack.Push(frame);
        _redoStack.Clear();
        _currentBatch.Clear();
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Performs an undo operation, returning the changes to reverse.
    /// </summary>
    public IReadOnlyList<AstChange>? Undo()
    {
        if (!CanUndo || _isDisposed)
        {
            return null;
        }

        _isUndoRedoInProgress = true;
        try
        {
            UndoFrame frame = _undoStack.Pop();
            _redoStack.Push(frame);
            StateChanged?.Invoke();

            // Return reversed changes for applying
            return CreateInverseChanges(frame.Changes);
        }
        finally
        {
            _isUndoRedoInProgress = false;
        }
    }

    /// <summary>
    /// Performs a redo operation, returning the changes to reapply.
    /// </summary>
    public IReadOnlyList<AstChange>? Redo()
    {
        if (!CanRedo || _isDisposed)
        {
            return null;
        }

        _isUndoRedoInProgress = true;
        try
        {
            UndoFrame frame = _redoStack.Pop();
            _undoStack.Push(frame);
            StateChanged?.Invoke();

            // Return original changes for reapplying
            return frame.Changes;
        }
        finally
        {
            _isUndoRedoInProgress = false;
        }
    }

    /// <summary>
    /// Clears all undo and redo history.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _currentBatch.Clear();
        StateChanged?.Invoke();
    }

    private static IReadOnlyList<AstChange> CreateInverseChanges(IReadOnlyList<AstChange> changes)
    {
        List<AstChange> inverse = new(changes.Count);

        // Process in reverse order
        for (int i = changes.Count - 1; i >= 0; i--)
        {
            AstChange change = changes[i];
            AstChange? inverseChange = change switch
            {
                NodeAdded added => new NodeRemoved(added.NodeId, added.ParentId, added.Index),
                NodeRemoved removed => new NodeAdded(removed.NodeId, removed.ParentId, removed.Index, removed.NodeTypeName),
                PropertyValueChanged propChanged => new PropertyValueChanged(
                    propChanged.NodeId, propChanged.PropertyName, propChanged.NewValue, propChanged.OldValue),
                TextContentChanged textChanged => new TextContentChanged(
                    textChanged.NodeId, textChanged.NewText, textChanged.OldText),
                NodeMoved moved => new NodeMoved(
                    moved.NodeId, moved.NewParentId, moved.NewIndex, moved.OldParentId, moved.OldIndex),
                _ => null
            };

            if (inverseChange is not null)
            {
                inverse.Add(inverseChange);
            }
        }

        return inverse;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Clear();
    }
}

/// <summary>
/// Represents a single undoable operation consisting of one or more AST changes.
/// </summary>
public sealed class UndoFrame
{
    /// <summary>
    /// Gets the human-readable description of the operation.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the AST changes that comprise this operation.
    /// </summary>
    public required IReadOnlyList<AstChange> Changes { get; init; }
}
