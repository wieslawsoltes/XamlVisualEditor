namespace XamlVisualEditor.Core;

/// <summary>
/// Metadata about a resolved type available for XAML usage.
/// </summary>
public sealed class TypeMetadata
{
    /// <summary>Gets the full CLR type name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the short type name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the XML namespace this type is available under.</summary>
    public required string XmlNamespace { get; init; }

    /// <summary>Gets the CLR namespace.</summary>
    public required string ClrNamespace { get; init; }

    /// <summary>Gets the assembly name.</summary>
    public required string AssemblyName { get; init; }

    /// <summary>Gets whether this type derives from Control.</summary>
    public bool IsControl { get; init; }

    /// <summary>Gets whether this type derives from Panel.</summary>
    public bool IsPanel { get; init; }

    /// <summary>Gets whether this type derives from ContentControl.</summary>
    public bool IsContentControl { get; init; }

    /// <summary>Gets the base type metadata, if any.</summary>
    public TypeMetadata? BaseType { get; init; }

    public override string ToString() => FullName;
}

/// <summary>
/// Metadata about a property on an Avalonia type.
/// </summary>
public sealed class PropertyMetadata
{
    /// <summary>Gets the property name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the full type name of the property value type.</summary>
    public required string TypeFullName { get; init; }

    /// <summary>Gets the kind of Avalonia property.</summary>
    public required PropertyKind Kind { get; init; }

    /// <summary>Gets whether the property is read-only.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>Gets the category for grouping in the property editor.</summary>
    public string Category { get; init; } = "Misc";

    /// <summary>Gets the default value, if known.</summary>
    public object? DefaultValue { get; init; }

    /// <summary>Gets an optional description.</summary>
    public string? Description { get; init; }

    public override string ToString() => $"{Name} : {TypeFullName}";
}

/// <summary>
/// Metadata about an event on an Avalonia type.
/// </summary>
public sealed class EventMetadata
{
    /// <summary>Gets the event name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the full type name of the event handler delegate.</summary>
    public required string HandlerTypeFullName { get; init; }

    /// <summary>Gets an optional description.</summary>
    public string? Description { get; init; }

    public override string ToString() => $"{Name} : {HandlerTypeFullName}";
}

/// <summary>
/// Represents a loaded workspace (solution, project, or standalone file).
/// </summary>
public sealed class WorkspaceModel
{
    /// <summary>Gets the projects in this workspace.</summary>
    public required IReadOnlyList<ProjectModel> Projects { get; init; }
}

/// <summary>
/// Represents a single project in a workspace.
/// </summary>
public sealed class ProjectModel
{
    /// <summary>Gets the project name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the absolute project file path.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Gets the XAML files in this project.</summary>
    public required IReadOnlyList<XamlFileModel> XamlFiles { get; init; }

    /// <summary>Gets the assembly references.</summary>
    public required IReadOnlyList<AssemblyReference> References { get; init; }
}

/// <summary>
/// Represents a XAML file within a project.
/// </summary>
public sealed class XamlFileModel
{
    /// <summary>Gets the absolute file path.</summary>
    public required string FilePath { get; init; }

    /// <summary>Gets the path relative to the project.</summary>
    public required string RelativePath { get; init; }

    /// <summary>Gets the design-time DataContext type, if specified.</summary>
    public string? DesignDataContext { get; init; }
}

/// <summary>
/// Represents an assembly reference from a project.
/// </summary>
public sealed class AssemblyReference
{
    /// <summary>Gets the assembly name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the absolute path to the assembly file.</summary>
    public required string Path { get; init; }
}

/// <summary>
/// State of a remote collaborator.
/// </summary>
public sealed class ParticipantState
{
    /// <summary>Gets the participant's unique identifier.</summary>
    public required string ParticipantId { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the cursor color assigned to this participant.</summary>
    public required string CursorColor { get; init; }

    /// <summary>Gets the code editor caret offset, if available.</summary>
    public int? CodeEditorCaretOffset { get; init; }

    /// <summary>Gets the selected design item ID, if any.</summary>
    public Guid? SelectedDesignItemId { get; init; }

    /// <summary>Gets the timestamp of last activity.</summary>
    public DateTimeOffset LastActivity { get; init; }
}
