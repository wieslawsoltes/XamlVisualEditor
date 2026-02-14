namespace XamlVisualEditor.Extensions;

/// <summary>Provides designer-centric APIs for extensions.</summary>
public interface IDesignerHost
{
    /// <summary>Gets the active designer document path, when available.</summary>
    string? ActiveDocumentPath { get; }

    /// <summary>Raised when the active designer document changes.</summary>
    event EventHandler<DesignerDocumentChangedEventArgs>? ActiveDocumentChanged;

    /// <summary>Raised when designer selection changes.</summary>
    event EventHandler<DesignerSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Gets currently selected nodes.</summary>
    Task<IReadOnlyList<DesignerNodeSummary>> GetSelectedNodesAsync(CancellationToken cancellationToken);

    /// <summary>Gets the visual tree snapshot for the active document.</summary>
    Task<IReadOnlyList<DesignerNodeSummary>> GetVisualTreeAsync(CancellationToken cancellationToken);

    /// <summary>Gets the logical tree snapshot for the active document.</summary>
    Task<IReadOnlyList<DesignerNodeSummary>> GetLogicalTreeAsync(CancellationToken cancellationToken);

    /// <summary>Gets available editable properties for a node.</summary>
    Task<IReadOnlyList<DesignerPropertyInfo>> GetPropertiesAsync(string nodeId, CancellationToken cancellationToken);

    /// <summary>Gets available events for a node.</summary>
    Task<IReadOnlyList<DesignerEventInfo>> GetEventsAsync(string nodeId, CancellationToken cancellationToken);

    /// <summary>Sets a property value for a node.</summary>
    Task<bool> SetPropertyAsync(string nodeId, string propertyName, string? value, CancellationToken cancellationToken);

    /// <summary>Inserts a new element under the specified parent, selected node, or document root.</summary>
    Task<string?> InsertElementAsync(
        string typeName,
        string xmlNamespace,
        string? parentNodeId,
        CancellationToken cancellationToken);

    /// <summary>Deletes a node from the active designer document.</summary>
    Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken);

    /// <summary>Wraps an existing node with a new wrapper element.</summary>
    Task<string?> WrapNodeAsync(
        string nodeId,
        string wrapperTypeName,
        string wrapperXmlNamespace,
        CancellationToken cancellationToken);

    /// <summary>Selects a node in the active designer document.</summary>
    Task<bool> SelectNodeAsync(string nodeId, CancellationToken cancellationToken);

    /// <summary>Reveals a node in the active designer document.</summary>
    Task<bool> RevealNodeAsync(string nodeId, CancellationToken cancellationToken);

    /// <summary>Begins an edit transaction scope.</summary>
    IDesignerTransaction BeginTransaction(string name);
}

/// <summary>Provides information about active designer document changes.</summary>
public sealed class DesignerDocumentChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public DesignerDocumentChangedEventArgs(string? documentPath)
    {
        DocumentPath = documentPath;
    }

    /// <summary>Gets the active document path.</summary>
    public string? DocumentPath { get; }
}

/// <summary>Provides information about designer selection changes.</summary>
public sealed class DesignerSelectionChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public DesignerSelectionChangedEventArgs(IReadOnlyList<DesignerNodeSummary> selectedNodes)
    {
        SelectedNodes = selectedNodes;
    }

    /// <summary>Gets the selected nodes snapshot.</summary>
    public IReadOnlyList<DesignerNodeSummary> SelectedNodes { get; }
}

/// <summary>Describes a node in the designer tree.</summary>
public sealed record DesignerNodeSummary(
    string NodeId,
    string TypeName,
    string? DisplayName,
    string? ParentNodeId,
    int ChildCount);

/// <summary>Describes a property available on a designer node.</summary>
public sealed record DesignerPropertyInfo(
    string Name,
    string PropertyType,
    string? Value,
    bool IsReadOnly,
    string? Category,
    string? Description,
    string? DefaultValue,
    bool IsAttached,
    string? OwnerType,
    IReadOnlyList<string>? EnumOptions);

/// <summary>Describes an event available on a designer node.</summary>
public sealed record DesignerEventInfo(
    string Name,
    string? HandlerName,
    string? Description);

/// <summary>Represents a transactional scope for designer mutations.</summary>
public interface IDesignerTransaction : IDisposable
{
    /// <summary>Commits the transaction.</summary>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>Rolls back the transaction.</summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}
