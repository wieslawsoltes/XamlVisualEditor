namespace XamlVisualEditor.Core;

/// <summary>
/// Severity level for XAML diagnostics.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>Informational hint.</summary>
    Info,

    /// <summary>Warning that does not prevent rendering.</summary>
    Warning,

    /// <summary>Error that prevents correct rendering or compilation.</summary>
    Error
}

/// <summary>
/// Kind of Avalonia property.
/// </summary>
public enum PropertyKind
{
    /// <summary>A styled property that participates in the styling system.</summary>
    Styled,

    /// <summary>A direct property with no styling overhead.</summary>
    Direct,

    /// <summary>An attached property defined by another type.</summary>
    Attached,

    /// <summary>A standard CLR property.</summary>
    ClrProperty,

    /// <summary>A boolean property (rendered as CheckBox).</summary>
    Boolean,

    /// <summary>A numeric property (rendered with NumericUpDown).</summary>
    Numeric,

    /// <summary>An enum property (rendered as ComboBox).</summary>
    Enum,

    /// <summary>A Brush property (rendered with color picker).</summary>
    Brush,

    /// <summary>A Thickness property (4-field editor).</summary>
    Thickness,

    /// <summary>A CornerRadius property (4-field editor).</summary>
    CornerRadius,

    /// <summary>A string property.</summary>
    String,

    /// <summary>A color property.</summary>
    Color,

    /// <summary>A point property.</summary>
    Point,

    /// <summary>A size property.</summary>
    Size,

    /// <summary>A rect property.</summary>
    Rect,

    /// <summary>A grid length property.</summary>
    GridLength,

    /// <summary>A font family property.</summary>
    FontFamily,

    /// <summary>A font weight property.</summary>
    FontWeight,

    /// <summary>A font style property.</summary>
    FontStyle,

    /// <summary>A TimeSpan property.</summary>
    TimeSpan,

    /// <summary>A Uri property.</summary>
    Uri,

    /// <summary>A collection property.</summary>
    Collection,

    /// <summary>A template property.</summary>
    Template,

    /// <summary>A markup extension property.</summary>
    MarkupExtension,

    /// <summary>An object property.</summary>
    Object
}

/// <summary>
/// Source of a synchronization event.
/// </summary>
public enum SyncSource
{
    /// <summary>Change originated from the code editor.</summary>
    CodeEditor,

    /// <summary>Change originated from the visual designer surface.</summary>
    DesignSurface,

    /// <summary>Change originated from the property editor.</summary>
    PropertyEditor,

    /// <summary>Change originated from the tree view.</summary>
    TreeView,

    /// <summary>Change originated from a remote collaborator.</summary>
    Collaboration
}

/// <summary>
/// Position of a drop operation relative to a target element.
/// </summary>
public enum DropPosition
{
    /// <summary>Insert before the target.</summary>
    Before,

    /// <summary>Insert after the target.</summary>
    After,

    /// <summary>Insert inside the target as a child.</summary>
    Inside,

    /// <summary>Replace the target element.</summary>
    Replace
}

/// <summary>
/// Type of adorner rendered on the design surface.
/// </summary>
public enum AdornerType
{
    /// <summary>Selection rectangle around selected items.</summary>
    Selection,

    /// <summary>Resize handles at corners and edges.</summary>
    ResizeHandle,

    /// <summary>Alignment snap lines.</summary>
    SnapLine,

    /// <summary>Margin and padding visualization.</summary>
    MarginPadding,

    /// <summary>Drop target indicator during drag-and-drop.</summary>
    DropTarget,

    /// <summary>Alignment guides for centering and distribution.</summary>
    AlignmentGuide
}

/// <summary>
/// Trigger type for code completion.
/// </summary>
public enum CompletionTrigger
{
    /// <summary>Explicitly invoked (e.g., Ctrl+Space).</summary>
    Invoked,

    /// <summary>Triggered by typing a character (e.g., '&lt;', ' ', '.').</summary>
    CharacterTyped,

    /// <summary>Triggered by a deletion.</summary>
    Deletion
}

/// <summary>
/// Ordering strategy for XAML attributes during serialization.
/// </summary>
public enum AttributeOrdering
{
    /// <summary>Preserve original attribute order.</summary>
    Preserve,

    /// <summary>Sort attributes alphabetically.</summary>
    Alphabetical,

    /// <summary>Sort by category (name, layout, appearance, etc.).</summary>
    ByCategory
}

/// <summary>
/// Type of a XAML collaboration operation.
/// </summary>
public enum XamlCollabOpType
{
    /// <summary>Insert a new AST node.</summary>
    InsertNode,

    /// <summary>Remove an existing AST node.</summary>
    RemoveNode,

    /// <summary>Move an AST node to a new parent/position.</summary>
    MoveNode,

    /// <summary>Set a property value on an AST node.</summary>
    SetProperty,

    /// <summary>Remove a property from an AST node.</summary>
    RemoveProperty,

    /// <summary>Set text content on an AST text node.</summary>
    SetText,

    /// <summary>Set or modify an xmlns declaration.</summary>
    SetXmlns
}
