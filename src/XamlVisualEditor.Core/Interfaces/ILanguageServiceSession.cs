using XamlVisualEditor.Core.Lsp;
using XamlVisualEditor.Core;

namespace XamlVisualEditor.Core.Interfaces;

/// <summary>
/// Represents a live LSP session for a specific language.
/// </summary>
public interface ILanguageServiceSession : IAsyncDisposable
{
    /// <summary>Gets the language identifier for this session.</summary>
    string LanguageId { get; }

    /// <summary>Gets the negotiated server capabilities.</summary>
    LspServerCapabilities Capabilities { get; }

    /// <summary>Gets whether the session is still alive.</summary>
    bool IsAlive { get; }

    /// <summary>Raised when diagnostics are published by the server.</summary>
    event EventHandler<LspPublishDiagnosticsParams> DiagnosticsPublished;

    /// <summary>Raised when the session faults and needs restart.</summary>
    event EventHandler<LanguageServiceSessionFaultedEventArgs>? SessionFaulted;

    /// <summary>Initializes the language server connection.</summary>
    ValueTask InitializeAsync(LspInitializeParams options, CancellationToken ct = default);

    /// <summary>Shuts down the language server connection.</summary>
    ValueTask ShutdownAsync(CancellationToken ct = default);

    /// <summary>Publishes an opened document to the server.</summary>
    ValueTask PublishDocumentAsync(LspTextDocumentItem document, CancellationToken ct = default);

    /// <summary>Applies document changes to the server.</summary>
    ValueTask ApplyDocumentChangesAsync(
        LspVersionedTextDocumentIdentifier documentId,
        IReadOnlyList<LspTextDocumentContentChangeEvent> changes,
        CancellationToken ct = default);

    /// <summary>Gets the latest diagnostics for the specified document.</summary>
    ValueTask<IReadOnlyList<LspDiagnostic>> GetDiagnosticsAsync(Uri documentUri, CancellationToken ct = default);

    /// <summary>Gets completion items for the specified position.</summary>
    ValueTask<IReadOnlyList<LspCompletionItem>> GetCompletionsAsync(
        LspCompletionParams parameters,
        CancellationToken ct = default);

    /// <summary>Gets hover information for the specified position.</summary>
    ValueTask<LspHover?> GetHoverAsync(LspHoverParams parameters, CancellationToken ct = default);

    /// <summary>Gets signature help for the specified position.</summary>
    ValueTask<LspSignatureHelp?> GetSignatureHelpAsync(
        LspSignatureHelpParams parameters,
        CancellationToken ct = default);

    /// <summary>Gets definition locations for the specified position.</summary>
    ValueTask<IReadOnlyList<LspLocation>> GetDefinitionAsync(
        LspDefinitionParams parameters,
        CancellationToken ct = default);

    /// <summary>Gets references for the specified position.</summary>
    ValueTask<IReadOnlyList<LspLocation>> GetReferencesAsync(
        LspReferenceParams parameters,
        CancellationToken ct = default);

    /// <summary>Gets rename range info for the specified position.</summary>
    ValueTask<LspRange?> PrepareRenameAsync(
        LspPrepareRenameParams parameters,
        CancellationToken ct = default);

    /// <summary>Renames the symbol at the specified position.</summary>
    ValueTask<LspWorkspaceEdit?> RenameAsync(
        LspRenameParams parameters,
        CancellationToken ct = default);

    /// <summary>Formats a document according to server rules.</summary>
    ValueTask<IReadOnlyList<LspTextEdit>> GetFormattingAsync(
        LspDocumentFormattingParams parameters,
        CancellationToken ct = default);

    /// <summary>Gets code actions for the specified range.</summary>
    ValueTask<IReadOnlyList<LspCodeAction>> GetCodeActionsAsync(
        LspCodeActionParams parameters,
        CancellationToken ct = default);

    /// <summary>Gets document symbols for the specified document.</summary>
    ValueTask<IReadOnlyList<LspDocumentSymbol>> GetDocumentSymbolsAsync(
        LspDocumentSymbolParams parameters,
        CancellationToken ct = default);

    /// <summary>Gets workspace symbols for the specified query.</summary>
    ValueTask<IReadOnlyList<LspSymbolInformation>> GetWorkspaceSymbolsAsync(
        LspWorkspaceSymbolParams parameters,
        CancellationToken ct = default);

    /// <summary>Gets semantic tokens for the specified document.</summary>
    ValueTask<LspSemanticTokens?> GetSemanticTokensAsync(
        LspSemanticTokensParams parameters,
        CancellationToken ct = default);
}
