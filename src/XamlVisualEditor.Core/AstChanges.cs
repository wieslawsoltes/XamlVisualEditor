namespace XamlVisualEditor.Core;

/// <summary>
/// Base record for all AST change notifications.
/// </summary>
/// <param name="NodeId">The unique identifier of the affected AST node.</param>
public abstract record AstChange(Guid NodeId);

/// <summary>
/// An AST node was added as a child of another node.
/// </summary>
public sealed record NodeAdded(
    Guid NodeId,
    Guid ParentId,
    int Index,
    string NodeTypeName
) : AstChange(NodeId);

/// <summary>
/// An AST node was removed from its parent.
/// </summary>
public sealed record NodeRemoved(
    Guid NodeId,
    Guid ParentId,
    int Index,
    string NodeTypeName = ""
) : AstChange(NodeId);

/// <summary>
/// An AST node was moved from one parent/position to another.
/// </summary>
public sealed record NodeMoved(
    Guid NodeId,
    Guid OldParentId,
    int OldIndex,
    Guid NewParentId,
    int NewIndex
) : AstChange(NodeId);

/// <summary>
/// A property value was changed on an AST node.
/// </summary>
public sealed record PropertyValueChanged(
    Guid NodeId,
    string PropertyName,
    string? OldValue,
    string? NewValue
) : AstChange(NodeId);

/// <summary>
/// The text content of a text node was changed.
/// </summary>
public sealed record TextContentChanged(
    Guid NodeId,
    string OldText,
    string NewText
) : AstChange(NodeId);
