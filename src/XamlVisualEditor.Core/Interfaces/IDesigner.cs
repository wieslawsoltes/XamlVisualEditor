namespace XamlVisualEditor.Core.Interfaces;

/// <summary>
/// Abstraction over the visual design surface.
/// </summary>
public interface IDesignSurface
{
    /// <summary>
    /// Gets the current document model displayed on the surface.
    /// </summary>
    IXamlDocumentModel? Document { get; }

    /// <summary>
    /// Gets the root design item.
    /// </summary>
    IDesignItem? RootItem { get; }

    /// <summary>
    /// Gets the currently selected design items.
    /// </summary>
    IReadOnlyList<IDesignItem> SelectedItems { get; }

    /// <summary>
    /// Selects a design item, optionally adding to the current selection.
    /// </summary>
    void Select(IDesignItem item, bool addToSelection = false);

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    void ClearSelection();

    /// <summary>
    /// Fires when the selection changes.
    /// </summary>
    event Action<IReadOnlyList<IDesignItem>>? SelectionChanged;
}

/// <summary>
/// Represents a single design item bridging an AST node and its visual control.
/// </summary>
public interface IDesignItem
{
    /// <summary>Gets the unique ID of the AST node this item represents.</summary>
    Guid AstNodeId { get; }

    /// <summary>Gets the type name of the control.</summary>
    string TypeName { get; }

    /// <summary>Gets the parent design item, or null for the root.</summary>
    IDesignItem? Parent { get; }

    /// <summary>Gets the child design items.</summary>
    IReadOnlyList<IDesignItem> Children { get; }

    /// <summary>Gets the property descriptors for this item.</summary>
    IReadOnlyList<IPropertyDescriptor> Properties { get; }

    /// <summary>
    /// Sets a property value on this design item.
    /// </summary>
    void SetProperty(string name, string? value);

    /// <summary>
    /// Gets a property value from this design item.
    /// </summary>
    string? GetProperty(string name);
}

/// <summary>
/// Provides code completion data for XAML intellisense.
/// </summary>
public interface ICompletionProvider
{
    /// <summary>
    /// Determines whether this provider should be triggered for the given context.
    /// </summary>
    bool ShouldTrigger(CompletionContext context);

    /// <summary>
    /// Returns completion items for the given context.
    /// </summary>
    IReadOnlyList<CompletionItem> GetCompletions(CompletionContext context);
}

/// <summary>
/// Context information for code completion.
/// </summary>
public sealed class CompletionContext
{
    /// <summary>Gets the current document model.</summary>
    public IXamlDocumentModel? Document { get; init; }

    /// <summary>Gets the caret offset in the text.</summary>
    public required int Offset { get; init; }

    /// <summary>Gets the text before the caret (current line or relevant segment).</summary>
    public required string TextBefore { get; init; }

    /// <summary>Gets the trigger type.</summary>
    public required CompletionTrigger Trigger { get; init; }

    /// <summary>Gets the type metadata service for resolving types.</summary>
    public ITypeMetadataService? Metadata { get; init; }
}

/// <summary>
/// A single code completion item.
/// </summary>
public sealed class CompletionItem
{
    /// <summary>Gets the text to display in the completion list.</summary>
    public required string DisplayText { get; init; }

    /// <summary>Gets the text to insert when this item is selected.</summary>
    public required string InsertText { get; init; }

    /// <summary>Gets an optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the sort priority (lower = higher priority).</summary>
    public int Priority { get; init; }

    /// <summary>Gets the kind of completion for icon selection.</summary>
    public CompletionItemKind Kind { get; init; }
}

/// <summary>
/// Kind of completion item for icon display.
/// </summary>
public enum CompletionItemKind
{
    /// <summary>A control/element type.</summary>
    Element,

    /// <summary>A property/attribute.</summary>
    Property,

    /// <summary>A property value (enum member, resource, etc.).</summary>
    Value,

    /// <summary>An XML namespace declaration.</summary>
    Namespace,

    /// <summary>A markup extension.</summary>
    MarkupExtension,

    /// <summary>A closing tag.</summary>
    ClosingTag
}
