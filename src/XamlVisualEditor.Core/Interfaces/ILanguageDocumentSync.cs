namespace XamlVisualEditor.Core.Interfaces;

/// <summary>
/// Supports sending document open/change events to language services.
/// </summary>
public interface ILanguageDocumentSync
{
    /// <summary>Notifies the service that a document was opened.</summary>
    Task DocumentOpenedAsync(LanguageDocumentContext context, CancellationToken ct = default);

    /// <summary>Notifies the service that a document changed.</summary>
    Task DocumentChangedAsync(LanguageDocumentContext context, CancellationToken ct = default);
}
