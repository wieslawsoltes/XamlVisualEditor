namespace XamlVisualEditor.Core.Interfaces;

/// <summary>
/// Bi-directional sync engine coordinating all mutation sources.
/// </summary>
public interface ISyncEngine : IDisposable
{
    /// <summary>
    /// Gets the current document model.
    /// </summary>
    IXamlDocumentModel? Document { get; }

    /// <summary>
    /// Loads XAML text into the sync engine, creating a new document.
    /// </summary>
    Task LoadAsync(string xamlText, CancellationToken ct = default);

    /// <summary>
    /// Saves the current document as XAML text.
    /// </summary>
    Task<string> SaveAsync(CancellationToken ct = default);

    /// <summary>
    /// Notifies the sync engine of a text change from the code editor.
    /// </summary>
    void NotifyTextChanged(string newText, SyncSource source);

    /// <summary>
    /// Notifies the sync engine of an AST change from any source.
    /// </summary>
    void NotifyAstChanged(AstChange change, SyncSource source);

    /// <summary>
    /// Fires when synchronization completes.
    /// </summary>
    event Action<SyncEvent>? SyncCompleted;
}

/// <summary>
/// Describes a sync event that was propagated.
/// </summary>
public sealed class SyncEvent
{
    /// <summary>Gets the source that triggered the sync.</summary>
    public required SyncSource Source { get; init; }

    /// <summary>Gets the AST changes that were applied.</summary>
    public required IReadOnlyList<AstChange> Changes { get; init; }

    /// <summary>Gets the updated XAML text (if applicable).</summary>
    public string? UpdatedText { get; init; }

    /// <summary>Gets the updated XAML text (alias for UpdatedText).</summary>
    public string? XamlText => UpdatedText;

    /// <summary>Gets the diagnostics from the parse.</summary>
    public IReadOnlyList<XamlDiagnostic>? Diagnostics { get; init; }
}

/// <summary>
/// Bridges the collaboration system to the sync engine.
/// </summary>
public interface ICollaborationBridge
{
    /// <summary>
    /// Sends local AST changes to remote collaborators.
    /// </summary>
    Task SendChangesAsync(IReadOnlyList<AstChange> changes, CancellationToken ct = default);

    /// <summary>
    /// Fires when changes are received from remote collaborators.
    /// </summary>
    event Action<IReadOnlyList<AstChange>>? RemoteChangesReceived;

    /// <summary>
    /// Gets whether a collaboration session is active.
    /// </summary>
    bool IsConnected { get; }
}
