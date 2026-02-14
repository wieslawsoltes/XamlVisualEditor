using XamlVisualEditor.Core;

namespace XamlVisualEditor.Extensions;

/// <summary>Provides access to editor documents.</summary>
public interface IEditorServices
{
    /// <summary>Gets the active editor document.</summary>
    IEditorDocument? ActiveDocument { get; }

    /// <summary>Gets the open editor documents.</summary>
    IReadOnlyList<IEditorDocument> GetOpenDocuments();

    /// <summary>Opens a document in the editor.</summary>
    Task<IEditorDocument?> OpenDocumentAsync(string filePath, CancellationToken ct);

    /// <summary>Opens a document in the editor with the specified behavior.</summary>
    Task<IEditorDocument?> OpenDocumentAsync(string filePath, EditorDocumentOpenBehavior behavior, CancellationToken ct);

    /// <summary>Opens a document and navigates to a location.</summary>
    Task<bool> OpenLocationAsync(LanguageLocation location, CancellationToken ct);

    /// <summary>Raised when the active document changes.</summary>
    event EventHandler<EditorActiveDocumentChangedEventArgs>? ActiveDocumentChanged;
}

/// <summary>Specifies how documents are opened.</summary>
public enum EditorDocumentOpenBehavior
{
    /// <summary>Allow loading a workspace when opening a workspace file.</summary>
    AllowWorkspaceLoad = 0,

    /// <summary>Open the file as a document only.</summary>
    DocumentOnly = 1
}

/// <summary>Represents a single editor document.</summary>
public interface IEditorDocument
{
    /// <summary>Gets the file path for the document.</summary>
    string FilePath { get; }

    /// <summary>Gets the language identifier for the document.</summary>
    string? LanguageId { get; }

    /// <summary>Gets or sets the caret offset.</summary>
    int CaretOffset { get; set; }

    /// <summary>Gets or sets the selection start offset.</summary>
    int SelectionStart { get; set; }

    /// <summary>Gets or sets the selection length.</summary>
    int SelectionLength { get; set; }

    /// <summary>Gets the document text.</summary>
    Task<string> GetTextAsync(CancellationToken ct);

    /// <summary>Applies text edits to the document.</summary>
    Task ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct);

    /// <summary>Raised when document text changes.</summary>
    event EventHandler<EditorDocumentChangedEventArgs>? Changed;

    /// <summary>Raised when selection changes.</summary>
    event EventHandler<EditorSelectionChangedEventArgs>? SelectionChanged;
}

/// <summary>Provides active document change data.</summary>
public sealed class EditorActiveDocumentChangedEventArgs : EventArgs
{
    /// <summary>Gets the new active document.</summary>
    public IEditorDocument? Document { get; init; }
}

/// <summary>Provides document change data.</summary>
public sealed class EditorDocumentChangedEventArgs : EventArgs
{
    /// <summary>Gets the document file path.</summary>
    public required string FilePath { get; init; }
}

/// <summary>Provides selection change data.</summary>
public sealed class EditorSelectionChangedEventArgs : EventArgs
{
    /// <summary>Gets the document file path.</summary>
    public required string FilePath { get; init; }

    /// <summary>Gets the selection start offset.</summary>
    public required int SelectionStart { get; init; }

    /// <summary>Gets the selection length.</summary>
    public required int SelectionLength { get; init; }
}
