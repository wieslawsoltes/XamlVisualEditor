namespace XamlVisualEditor.Core.Lsp;

/// <summary>
/// Represents a zero-based position in a text document.
/// </summary>
public readonly record struct LspPosition(int Line, int Character);

/// <summary>
/// Represents a range in a text document.
/// </summary>
public readonly record struct LspRange(LspPosition Start, LspPosition End);

/// <summary>
/// Identifies a text document by URI.
/// </summary>
public sealed class LspTextDocumentIdentifier
{
    /// <summary>Gets the document URI.</summary>
    public required Uri Uri { get; init; }
}

/// <summary>
/// Identifies a versioned text document by URI.
/// </summary>
public sealed class LspVersionedTextDocumentIdentifier
{
    /// <summary>Gets the document URI.</summary>
    public required Uri Uri { get; init; }

    /// <summary>Gets the document version.</summary>
    public int? Version { get; init; }
}

/// <summary>
/// Represents a text document item sent on open.
/// </summary>
public sealed class LspTextDocumentItem
{
    /// <summary>Gets the document URI.</summary>
    public required Uri Uri { get; init; }

    /// <summary>Gets the language identifier.</summary>
    public required string LanguageId { get; init; }

    /// <summary>Gets the document version.</summary>
    public int Version { get; init; }

    /// <summary>Gets the document text.</summary>
    public required string Text { get; init; }
}

/// <summary>
/// Represents a text document change event.
/// </summary>
public sealed class LspTextDocumentContentChangeEvent
{
    /// <summary>Gets the changed range, if applicable.</summary>
    public LspRange? Range { get; init; }

    /// <summary>Gets the range length, if applicable.</summary>
    public int? RangeLength { get; init; }

    /// <summary>Gets the changed text.</summary>
    public required string Text { get; init; }
}

/// <summary>
/// Describes client info for initialization.
/// </summary>
public sealed class LspClientInfo
{
    /// <summary>Gets the client name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the client version.</summary>
    public string? Version { get; init; }
}

/// <summary>
/// Parameters for the initialize request.
/// </summary>
public sealed class LspInitializeParams
{
    /// <summary>Gets the client process ID.</summary>
    public int? ProcessId { get; init; }

    /// <summary>Gets the root URI for the workspace.</summary>
    public string? RootUri { get; init; }

    /// <summary>Gets the client info.</summary>
    public LspClientInfo? ClientInfo { get; init; }

    /// <summary>Gets the client capabilities object.</summary>
    public object? Capabilities { get; init; }

    /// <summary>Gets optional initialization options.</summary>
    public object? InitializationOptions { get; init; }
}

/// <summary>
/// Represents server capabilities returned from initialize.
/// </summary>
public sealed class LspServerCapabilities
{
    public bool? CompletionProvider { get; init; }
    public bool? HoverProvider { get; init; }
    public bool? SignatureHelpProvider { get; init; }
    public bool? DefinitionProvider { get; init; }
    public bool? DocumentFormattingProvider { get; init; }
    public bool? CodeActionProvider { get; init; }
    public bool? RenameProvider { get; init; }
    public bool? DocumentSymbolProvider { get; init; }
    public bool? WorkspaceSymbolProvider { get; init; }
    public bool? ReferencesProvider { get; init; }
    public bool? SemanticTokensProvider { get; init; }
    public LspSemanticTokensLegend? SemanticTokensLegend { get; init; }

    public bool Supports(LspFeature feature)
    {
        return feature switch
        {
            LspFeature.Completion => CompletionProvider ?? true,
            LspFeature.Hover => HoverProvider ?? true,
            LspFeature.SignatureHelp => SignatureHelpProvider ?? true,
            LspFeature.Definition => DefinitionProvider ?? true,
            LspFeature.Formatting => DocumentFormattingProvider ?? true,
            LspFeature.CodeAction => CodeActionProvider ?? true,
            LspFeature.Rename => RenameProvider ?? true,
            LspFeature.DocumentSymbols => DocumentSymbolProvider ?? true,
            LspFeature.WorkspaceSymbols => WorkspaceSymbolProvider ?? true,
            LspFeature.References => ReferencesProvider ?? true,
            LspFeature.SemanticTokens => SemanticTokensProvider ?? true,
            _ => true
        };
    }
}

/// <summary>
/// Represents the initialize result payload.
/// </summary>
public sealed class LspInitializeResult
{
    public required LspServerCapabilities Capabilities { get; init; }
}

/// <summary>
/// Known LSP feature flags used for capability gating.
/// </summary>
public enum LspFeature
{
    Completion,
    Hover,
    SignatureHelp,
    Definition,
    Formatting,
    CodeAction,
    Rename,
    DocumentSymbols,
    WorkspaceSymbols,
    References,
    SemanticTokens
}

/// <summary>
/// Parameters for textDocument/position requests.
/// </summary>
public class LspTextDocumentPositionParams
{
    /// <summary>Gets the text document identifier.</summary>
    public required LspTextDocumentIdentifier TextDocument { get; init; }

    /// <summary>Gets the position in the document.</summary>
    public required LspPosition Position { get; init; }
}

/// <summary>
/// Parameters for completion requests.
/// </summary>
public sealed class LspCompletionParams : LspTextDocumentPositionParams
{
}

/// <summary>
/// Parameters for hover requests.
/// </summary>
public sealed class LspHoverParams : LspTextDocumentPositionParams
{
}

/// <summary>
/// Parameters for signature help requests.
/// </summary>
public sealed class LspSignatureHelpParams : LspTextDocumentPositionParams
{
}

/// <summary>
/// Parameters for definition requests.
/// </summary>
public sealed class LspDefinitionParams : LspTextDocumentPositionParams
{
}

/// <summary>
/// Context for references requests.
/// </summary>
public sealed class LspReferenceContext
{
    public bool IncludeDeclaration { get; init; }
}

/// <summary>
/// Parameters for references requests.
/// </summary>
public sealed class LspReferenceParams : LspTextDocumentPositionParams
{
    public required LspReferenceContext Context { get; init; }
}

/// <summary>
/// Parameters for rename requests.
/// </summary>
public sealed class LspRenameParams : LspTextDocumentPositionParams
{
    public required string NewName { get; init; }
}

/// <summary>
/// Parameters for prepare rename requests.
/// </summary>
public sealed class LspPrepareRenameParams : LspTextDocumentPositionParams
{
}

/// <summary>
/// Formatting options for document formatting.
/// </summary>
public sealed class LspFormattingOptions
{
    /// <summary>Gets the tab size.</summary>
    public int TabSize { get; init; }

    /// <summary>Gets whether spaces are used instead of tabs.</summary>
    public bool InsertSpaces { get; init; }
}

/// <summary>
/// Parameters for document formatting.
/// </summary>
public sealed class LspDocumentFormattingParams
{
    /// <summary>Gets the text document identifier.</summary>
    public required LspTextDocumentIdentifier TextDocument { get; init; }

    /// <summary>Gets the formatting options.</summary>
    public required LspFormattingOptions Options { get; init; }
}

/// <summary>
/// Parameters for semantic tokens requests.
/// </summary>
public sealed class LspSemanticTokensParams
{
    public required LspTextDocumentIdentifier TextDocument { get; init; }
}

/// <summary>
/// Parameters for document symbols requests.
/// </summary>
public sealed class LspDocumentSymbolParams
{
    public required LspTextDocumentIdentifier TextDocument { get; init; }
}

/// <summary>
/// Parameters for workspace symbols requests.
/// </summary>
public sealed class LspWorkspaceSymbolParams
{
    public required string Query { get; init; }
}

/// <summary>
/// Context for code actions.
/// </summary>
public sealed class LspCodeActionContext
{
    /// <summary>Gets the diagnostics associated with the context.</summary>
    public IReadOnlyList<LspDiagnostic> Diagnostics { get; init; } = Array.Empty<LspDiagnostic>();
}

/// <summary>
/// Parameters for code actions.
/// </summary>
public sealed class LspCodeActionParams
{
    /// <summary>Gets the text document identifier.</summary>
    public required LspTextDocumentIdentifier TextDocument { get; init; }

    /// <summary>Gets the relevant range.</summary>
    public required LspRange Range { get; init; }

    /// <summary>Gets the context.</summary>
    public required LspCodeActionContext Context { get; init; }
}

/// <summary>
/// Describes a diagnostic produced by an LSP server.
/// </summary>
public sealed class LspDiagnostic
{
    /// <summary>Gets the diagnostic range.</summary>
    public required LspRange Range { get; init; }

    /// <summary>Gets the severity.</summary>
    public LspDiagnosticSeverity? Severity { get; init; }

    /// <summary>Gets the diagnostic message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets an optional code.</summary>
    public string? Code { get; init; }

    /// <summary>Gets the source.</summary>
    public string? Source { get; init; }
}

/// <summary>
/// Represents LSP diagnostic severities.
/// </summary>
public enum LspDiagnosticSeverity
{
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4
}

/// <summary>
/// Represents a completion item from an LSP server.
/// </summary>
public sealed class LspCompletionItem
{
    /// <summary>Gets the label for the completion item.</summary>
    public required string Label { get; init; }

    /// <summary>Gets the insert text.</summary>
    public string? InsertText { get; init; }

    /// <summary>Gets the detail string.</summary>
    public string? Detail { get; init; }

    /// <summary>Gets optional documentation.</summary>
    public string? Documentation { get; init; }

    /// <summary>Gets the sort text.</summary>
    public string? SortText { get; init; }

    /// <summary>Gets the filter text.</summary>
    public string? FilterText { get; init; }

    /// <summary>Gets the completion item kind.</summary>
    public LspCompletionItemKind? Kind { get; init; }

    /// <summary>Gets the optional text edit.</summary>
    public LspTextEdit? TextEdit { get; init; }

    /// <summary>Gets commit characters for this completion item.</summary>
    public IReadOnlyList<string> CommitCharacters { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Represents completion item kinds.
/// </summary>
public enum LspCompletionItemKind
{
    Text = 1,
    Method = 2,
    Function = 3,
    Constructor = 4,
    Field = 5,
    Variable = 6,
    Class = 7,
    Interface = 8,
    Module = 9,
    Property = 10,
    Unit = 11,
    Value = 12,
    Enum = 13,
    Keyword = 14,
    Snippet = 15,
    Color = 16,
    File = 17,
    Reference = 18,
    Folder = 19,
    EnumMember = 20,
    Constant = 21,
    Struct = 22,
    Event = 23,
    Operator = 24,
    TypeParameter = 25
}

/// <summary>
/// Represents hover information.
/// </summary>
public sealed class LspHover
{
    /// <summary>Gets the hover contents.</summary>
    public required string Contents { get; init; }

    /// <summary>Gets the range for the hover.</summary>
    public LspRange? Range { get; init; }
}

/// <summary>
/// Represents signature help information.
/// </summary>
public sealed class LspSignatureHelp
{
    /// <summary>Gets the signature list.</summary>
    public IReadOnlyList<LspSignatureInformation> Signatures { get; init; } = Array.Empty<LspSignatureInformation>();

    /// <summary>Gets the active signature index.</summary>
    public int ActiveSignature { get; init; }

    /// <summary>Gets the active parameter index.</summary>
    public int ActiveParameter { get; init; }
}

/// <summary>
/// Represents a single callable signature.
/// </summary>
public sealed class LspSignatureInformation
{
    /// <summary>Gets the signature label.</summary>
    public required string Label { get; init; }

    /// <summary>Gets optional documentation.</summary>
    public string? Documentation { get; init; }

    /// <summary>Gets the signature parameters.</summary>
    public IReadOnlyList<LspParameterInformation> Parameters { get; init; } = Array.Empty<LspParameterInformation>();
}

/// <summary>
/// Represents a signature parameter.
/// </summary>
public sealed class LspParameterInformation
{
    /// <summary>Gets the parameter label.</summary>
    public required string Label { get; init; }

    /// <summary>Gets optional documentation.</summary>
    public string? Documentation { get; init; }
}

/// <summary>
/// Represents a document location.
/// </summary>
public sealed class LspLocation
{
    /// <summary>Gets the location URI.</summary>
    public required Uri Uri { get; init; }

    /// <summary>Gets the location range.</summary>
    public required LspRange Range { get; init; }
}

/// <summary>
/// Represents a document symbol.
/// </summary>
public sealed class LspDocumentSymbol
{
    public required string Name { get; init; }
    public LspSymbolKind Kind { get; init; }
    public required LspRange Range { get; init; }
    public required LspRange SelectionRange { get; init; }
    public IReadOnlyList<LspDocumentSymbol> Children { get; init; } = Array.Empty<LspDocumentSymbol>();
}

/// <summary>
/// Represents a symbol information entry.
/// </summary>
public sealed class LspSymbolInformation
{
    public required string Name { get; init; }
    public LspSymbolKind Kind { get; init; }
    public required LspLocation Location { get; init; }
}

/// <summary>
/// Represents semantic tokens response data.
/// </summary>
public sealed class LspSemanticTokens
{
    public IReadOnlyList<int> Data { get; init; } = Array.Empty<int>();
}

/// <summary>
/// Represents the semantic tokens legend.
/// </summary>
public sealed class LspSemanticTokensLegend
{
    public IReadOnlyList<string> TokenTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TokenModifiers { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Represents symbol kinds.
/// </summary>
public enum LspSymbolKind
{
    File = 1,
    Module = 2,
    Namespace = 3,
    Package = 4,
    Class = 5,
    Method = 6,
    Property = 7,
    Field = 8,
    Constructor = 9,
    Enum = 10,
    Interface = 11,
    Function = 12,
    Variable = 13,
    Constant = 14,
    String = 15,
    Number = 16,
    Boolean = 17,
    Array = 18,
    Object = 19,
    Key = 20,
    Null = 21,
    EnumMember = 22,
    Struct = 23,
    Event = 24,
    Operator = 25,
    TypeParameter = 26
}

/// <summary>
/// Represents a text edit.
/// </summary>
public sealed class LspTextEdit
{
    /// <summary>Gets the edit range.</summary>
    public required LspRange Range { get; init; }

    /// <summary>Gets the edit text.</summary>
    public required string NewText { get; init; }
}

/// <summary>
/// Represents a command.
/// </summary>
public sealed class LspCommand
{
    /// <summary>Gets the command title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the command identifier.</summary>
    public required string Command { get; init; }

    /// <summary>Gets the optional arguments.</summary>
    public IReadOnlyList<object> Arguments { get; init; } = Array.Empty<object>();
}

/// <summary>
/// Represents edits for a single document.
/// </summary>
public sealed class LspTextDocumentEdit
{
    /// <summary>Gets the text document identifier.</summary>
    public required LspVersionedTextDocumentIdentifier TextDocument { get; init; }

    /// <summary>Gets the edits for the document.</summary>
    public required IReadOnlyList<LspTextEdit> Edits { get; init; }
}

/// <summary>
/// Represents workspace edits.
/// </summary>
public sealed class LspWorkspaceEdit
{
    /// <summary>Gets the document edits.</summary>
    public IReadOnlyList<LspTextDocumentEdit> DocumentChanges { get; init; } = Array.Empty<LspTextDocumentEdit>();
}

/// <summary>
/// Represents a code action.
/// </summary>
public sealed class LspCodeAction
{
    /// <summary>Gets the code action title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the code action kind.</summary>
    public string? Kind { get; init; }

    /// <summary>Gets the optional edit.</summary>
    public LspWorkspaceEdit? Edit { get; init; }

    /// <summary>Gets the optional command.</summary>
    public LspCommand? Command { get; init; }
}

/// <summary>
/// Parameters for publish diagnostics notifications.
/// </summary>
public sealed class LspPublishDiagnosticsParams
{
    /// <summary>Gets the document URI.</summary>
    public required Uri Uri { get; init; }

    /// <summary>Gets the diagnostics list.</summary>
    public IReadOnlyList<LspDiagnostic> Diagnostics { get; init; } = Array.Empty<LspDiagnostic>();
}
