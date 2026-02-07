using System.Collections.Generic;

namespace XamlVisualEditor.Core.Interfaces;

/// <summary>
/// Provides XAML parsing capabilities with error recovery.
/// </summary>
public interface IXamlParsingService
{
    /// <summary>
    /// Parses XAML text into a mutable AST document.
    /// </summary>
    /// <param name="xamlText">The XAML source text.</param>
    /// <param name="options">Parser configuration options.</param>
    /// <returns>A parse result containing the document and/or diagnostics.</returns>
    ParseResult Parse(string xamlText, XamlParserOptions? options = null);
}

/// <summary>
/// Result of a XAML parsing operation.
/// </summary>
public sealed class ParseResult
{
    /// <summary>
    /// Gets the parsed AST document, or null if parsing completely failed.
    /// </summary>
    public IXamlDocumentModel? Document { get; init; }

    /// <summary>
    /// Gets the diagnostics produced during parsing.
    /// </summary>
    public required IReadOnlyList<XamlDiagnostic> Diagnostics { get; init; }

    /// <summary>
    /// Gets whether the parse result contains any errors.
    /// </summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Gets whether a document was produced (possibly partial).
    /// </summary>
    public bool HasDocument => Document is not null;
}

/// <summary>
/// Abstraction over a mutable XAML AST document.
/// </summary>
public interface IXamlDocumentModel
{
    /// <summary>
    /// Gets the unique identifier for this document.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Fires when any node in the document changes.
    /// </summary>
    event Action<AstChange>? Changed;
}

/// <summary>
/// Serializes a mutable AST document back to XAML text.
/// </summary>
public interface IXamlSerializationService
{
    /// <summary>
    /// Serializes the document to formatted XAML text.
    /// </summary>
    string Serialize(IXamlDocumentModel document, SerializationOptions? options = null);

    /// <summary>
    /// Computes minimal text edits to update <paramref name="currentText"/>
    /// to reflect the given AST changes.
    /// </summary>
    IReadOnlyList<TextEdit> ComputeMinimalEdits(
        IXamlDocumentModel document,
        string currentText,
        IReadOnlyList<AstChange> changes);
}

/// <summary>
/// Provides type metadata for XAML intellisense, property editing, and control instantiation.
/// </summary>
public interface ITypeMetadataService
{
    /// <summary>
    /// Resolves a type by XML namespace and local name.
    /// </summary>
    TypeMetadata? GetType(string xmlNamespace, string typeName);

    /// <summary>
    /// Returns all available types, optionally filtered by XML namespace.
    /// </summary>
    IReadOnlyList<TypeMetadata> GetAvailableTypes(string? xmlNamespace = null);

    /// <summary>
    /// Returns all properties for the given type (including inherited).
    /// </summary>
    IReadOnlyList<PropertyMetadata> GetProperties(TypeMetadata type);

    /// <summary>
    /// Returns all events for the given type.
    /// </summary>
    IReadOnlyList<EventMetadata> GetEvents(TypeMetadata type);

    /// <summary>
    /// Returns all available XML namespaces.
    /// </summary>
    IReadOnlyList<string> GetAvailableNamespaces();

    /// <summary>
    /// Loads an assembly for metadata lookup.
    /// </summary>
    void LoadAssembly(string assemblyPath);

    /// <summary>
    /// Loads multiple assemblies for metadata lookup.
    /// </summary>
    void LoadAssemblies(IEnumerable<string> assemblyPaths);

    /// <summary>
    /// Resolves a CLR type from previously loaded assemblies.
    /// </summary>
    Type? ResolveClrType(TypeMetadata type);
}

/// <summary>
/// Manages MSBuild workspace loading and project enumeration.
/// </summary>
public interface IWorkspaceService
{
    /// <summary>
    /// Loads a solution file and enumerates its projects and XAML files.
    /// </summary>
    Task<WorkspaceModel> LoadSolutionAsync(string solutionPath, CancellationToken ct = default);

    /// <summary>
    /// Loads a single project file.
    /// </summary>
    Task<WorkspaceModel> LoadProjectAsync(string projectPath, CancellationToken ct = default);

    /// <summary>
    /// Creates a standalone workspace for a single XAML file with bundled metadata.
    /// </summary>
    WorkspaceModel CreateStandaloneWorkspace(string xamlFilePath);
}

/// <summary>
/// Describes a property of an Avalonia control for the property editor.
/// </summary>
public interface IPropertyDescriptor
{
    /// <summary>Gets the property name.</summary>
    string Name { get; }

    /// <summary>Gets the display-friendly name.</summary>
    string DisplayName { get; }

    /// <summary>Gets the category for grouping.</summary>
    string Category { get; }

    /// <summary>Gets an optional description.</summary>
    string? Description { get; }

    /// <summary>Gets the CLR type of the property.</summary>
    Type PropertyType { get; }

    /// <summary>Gets the kind of Avalonia property.</summary>
    PropertyKind Kind { get; }

    /// <summary>Gets whether the property is read-only.</summary>
    bool IsReadOnly { get; }

    /// <summary>Gets the default value.</summary>
    object? DefaultValue { get; }
}
