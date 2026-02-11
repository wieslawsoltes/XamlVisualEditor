using XamlVisualEditor.Core;

namespace XamlVisualEditor.Core.Interfaces;

/// <summary>
/// Context for operations that need a document and its text.
/// </summary>
public class LanguageDocumentContext
{
    /// <summary>Gets the file path for the document.</summary>
    public required string FilePath { get; init; }

    /// <summary>Gets the current document text.</summary>
    public required string Text { get; init; }
}

/// <summary>
/// Context for operations that need a caret position.
/// </summary>
public class LanguagePositionContext : LanguageDocumentContext
{
    /// <summary>Gets the caret offset in the document.</summary>
    public required int Offset { get; init; }
}

/// <summary>
/// Context for code action operations.
/// </summary>
public sealed class LanguageCodeActionContext : LanguagePositionContext
{
    /// <summary>Gets the length of the selection.</summary>
    public int Length { get; init; }
}

/// <summary>
/// Context for workspace symbol searches.
/// </summary>
public sealed class LanguageSymbolQuery
{
    /// <summary>Gets the query text.</summary>
    public required string Query { get; init; }
}

/// <summary>
/// Context for rename operations.
/// </summary>
public sealed class LanguageRenameContext : LanguagePositionContext
{
    /// <summary>Gets the new symbol name.</summary>
    public required string NewName { get; init; }
}

/// <summary>
/// Defines a language-specific intellisense service.
/// </summary>
public interface ILanguageIntellisenseService
{
    /// <summary>Gets the language identifier for this service.</summary>
    string LanguageId { get; }

    /// <summary>Determines whether this service can handle the provided document.</summary>
    bool CanHandle(string filePath, string? languageId);

    /// <summary>Loads or updates the workspace context for a solution or project.</summary>
    System.Threading.Tasks.Task InitializeWorkspaceAsync(string workspacePath, CancellationToken ct = default);

    /// <summary>Clears any loaded workspace context.</summary>
    System.Threading.Tasks.Task ClearWorkspaceAsync(CancellationToken ct = default);

    /// <summary>Gets completion items for the given context.</summary>
    System.Threading.Tasks.Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken ct = default);

    /// <summary>Gets diagnostics for the given document.</summary>
    System.Threading.Tasks.Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default);

    /// <summary>Gets semantic tokens for the given document.</summary>
    System.Threading.Tasks.Task<IReadOnlyList<LanguageSemanticToken>> GetSemanticTokensAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default);

    /// <summary>Formats the document and returns edits.</summary>
    System.Threading.Tasks.Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default);

    /// <summary>Gets hover information at the specified position.</summary>
    System.Threading.Tasks.Task<LanguageHover?> GetHoverAsync(
        LanguagePositionContext context,
        CancellationToken ct = default);

    /// <summary>Finds definitions for the symbol at the specified position.</summary>
    System.Threading.Tasks.Task<IReadOnlyList<LanguageLocation>> FindDefinitionsAsync(
        LanguagePositionContext context,
        CancellationToken ct = default);

    /// <summary>Finds references for the symbol at the specified position.</summary>
    System.Threading.Tasks.Task<IReadOnlyList<LanguageLocation>> FindReferencesAsync(
        LanguagePositionContext context,
        CancellationToken ct = default);

    /// <summary>Gets rename information for the symbol at the specified position.</summary>
    System.Threading.Tasks.Task<LanguageRenameInfo?> PrepareRenameAsync(
        LanguagePositionContext context,
        CancellationToken ct = default);

    /// <summary>Renames the symbol at the specified position.</summary>
    System.Threading.Tasks.Task<LanguageWorkspaceEdit?> RenameSymbolAsync(
        LanguageRenameContext context,
        CancellationToken ct = default);

    /// <summary>Gets signature help at the specified position.</summary>
    System.Threading.Tasks.Task<LanguageSignatureHelp?> GetSignatureHelpAsync(
        LanguagePositionContext context,
        CancellationToken ct = default);

    /// <summary>Gets code actions at the specified position.</summary>
    System.Threading.Tasks.Task<IReadOnlyList<LanguageCodeAction>> GetCodeActionsAsync(
        LanguageCodeActionContext context,
        CancellationToken ct = default);

    /// <summary>Gets document symbols for the current document.</summary>
    System.Threading.Tasks.Task<IReadOnlyList<LanguageSymbol>> GetDocumentSymbolsAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default);

    /// <summary>Gets workspace symbols using the provided query.</summary>
    System.Threading.Tasks.Task<IReadOnlyList<LanguageSymbol>> GetWorkspaceSymbolsAsync(
        LanguageSymbolQuery query,
        CancellationToken ct = default);
}

/// <summary>
/// Resolves language services for a given document.
/// </summary>
public interface ILanguageIntellisenseRegistry
{
    /// <summary>Returns a service that can handle the given document.</summary>
    ILanguageIntellisenseService? GetService(string filePath, string? languageId);

    /// <summary>Gets all registered language services.</summary>
    IReadOnlyList<ILanguageIntellisenseService> Services { get; }
}
