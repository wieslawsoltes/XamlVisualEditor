using System;
using System.Collections.Generic;

namespace XamlVisualEditor.Shell.ViewModels;

public sealed class AnimationUndoRedoService : IDisposable
{
    private readonly Stack<AnimationEdit> _undoStack = new();
    private readonly Stack<AnimationEdit> _redoStack = new();
    private bool _isDisposed;

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public void Record(AnimationEdit edit)
    {
        if (_isDisposed)
        {
            return;
        }

        _undoStack.Push(edit);
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (!CanUndo || _isDisposed)
        {
            return;
        }

        AnimationEdit edit = _undoStack.Pop();
        edit.Undo();
        _redoStack.Push(edit);
    }

    public void Redo()
    {
        if (!CanRedo || _isDisposed)
        {
            return;
        }

        AnimationEdit edit = _redoStack.Pop();
        edit.Apply();
        _undoStack.Push(edit);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Clear();
    }
}

public sealed class AnimationEdit
{
    public AnimationEdit(string description, Action apply, Action undo)
    {
        Description = description;
        Apply = apply;
        Undo = undo;
    }

    public string Description { get; }

    public Action Apply { get; }

    public Action Undo { get; }
}

public enum AnimationKeyframeEditKind
{
    Time,
    Value,
    Easing
}

public sealed class KeyframeEditChange
{
    public KeyframeEditChange(AnimationKeyframeViewModel keyframe, AnimationKeyframeEditKind kind, object? oldValue, object? newValue)
    {
        Keyframe = keyframe;
        Kind = kind;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public AnimationKeyframeViewModel Keyframe { get; }

    public AnimationKeyframeEditKind Kind { get; }

    public object? OldValue { get; }

    public object? NewValue { get; }
}
