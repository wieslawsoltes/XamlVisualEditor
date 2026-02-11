using XamlVisualEditor.Core;

namespace XamlVisualEditor.Extensions;

/// <summary>Provides access to editor documents.</summary>
public interface IEditorServices
{
    /// <summary>Gets the active editor document.</summary>
    IEditorDocument? ActiveDocument { get; }

    /// <summary>Gets the open editor documents.</summary>
    IReadOnlyList<IEditorDocument> GetOpenDocuments();

    /// <summary>Raised when the active document changes.</summary>
    event EventHandler<EditorActiveDocumentChangedEventArgs>? ActiveDocumentChanged;
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

    /// <summary>Gets the document text.</summary>
    Task<string> GetTextAsync(CancellationToken ct);

    /// <summary>Applies text edits to the document.</summary>
    Task ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct);

    /// <summary>Raised when document text changes.</summary>
    event EventHandler<EditorDocumentChangedEventArgs>? Changed;
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
