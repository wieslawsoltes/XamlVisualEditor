using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;

namespace XamlVisualEditor.Extensions;

/// <summary>Registers language service providers for extensions.</summary>
public interface IExtensionLanguageServices
{
    /// <summary>Registers a completion provider.</summary>
    IDisposable RegisterCompletionProvider(string languageId, IExtensionCompletionProvider provider);

    /// <summary>Registers a hover provider.</summary>
    IDisposable RegisterHoverProvider(string languageId, IExtensionHoverProvider provider);

    /// <summary>Registers a definition provider.</summary>
    IDisposable RegisterDefinitionProvider(string languageId, IExtensionDefinitionProvider provider);

    /// <summary>Registers a references provider.</summary>
    IDisposable RegisterReferencesProvider(string languageId, IExtensionReferencesProvider provider);

    /// <summary>Registers a signature help provider.</summary>
    IDisposable RegisterSignatureHelpProvider(string languageId, IExtensionSignatureHelpProvider provider);

    /// <summary>Registers a code actions provider.</summary>
    IDisposable RegisterCodeActionsProvider(string languageId, IExtensionCodeActionsProvider provider);

    /// <summary>Registers a formatting provider.</summary>
    IDisposable RegisterFormattingProvider(string languageId, IExtensionFormattingProvider provider);

    /// <summary>Registers a diagnostics provider.</summary>
    IDisposable RegisterDiagnosticsProvider(string languageId, IExtensionDiagnosticsProvider provider);

    /// <summary>Registers a document sync provider.</summary>
    IDisposable RegisterDocumentSyncProvider(string languageId, IExtensionDocumentSyncProvider provider);
}

/// <summary>Provides completion items for a language.</summary>
public interface IExtensionCompletionProvider
{
    /// <summary>Gets completion items for the given context.</summary>
    Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(CompletionContext context, CancellationToken ct);
}

/// <summary>Provides hover information for a language.</summary>
public interface IExtensionHoverProvider
{
    /// <summary>Gets hover information at the specified position.</summary>
    Task<LanguageHover?> GetHoverAsync(LanguagePositionContext context, CancellationToken ct);
}

/// <summary>Provides definition locations for a language.</summary>
public interface IExtensionDefinitionProvider
{
    /// <summary>Finds definitions at the specified position.</summary>
    Task<IReadOnlyList<LanguageLocation>> FindDefinitionsAsync(LanguagePositionContext context, CancellationToken ct);
}

/// <summary>Provides reference locations for a language.</summary>
public interface IExtensionReferencesProvider
{
    /// <summary>Finds references at the specified position.</summary>
    Task<IReadOnlyList<LanguageLocation>> FindReferencesAsync(LanguagePositionContext context, CancellationToken ct);
}

/// <summary>Provides signature help for a language.</summary>
public interface IExtensionSignatureHelpProvider
{
    /// <summary>Gets signature help at the specified position.</summary>
    Task<LanguageSignatureHelp?> GetSignatureHelpAsync(LanguagePositionContext context, CancellationToken ct);
}

/// <summary>Provides code actions for a language.</summary>
public interface IExtensionCodeActionsProvider
{
    /// <summary>Gets code actions at the specified position.</summary>
    Task<IReadOnlyList<LanguageCodeAction>> GetCodeActionsAsync(LanguageCodeActionContext context, CancellationToken ct);
}

/// <summary>Provides formatting edits for a language.</summary>
public interface IExtensionFormattingProvider
{
    /// <summary>Gets formatting edits for the specified document.</summary>
    Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(LanguageDocumentContext context, CancellationToken ct);
}

/// <summary>Provides diagnostics for a language.</summary>
public interface IExtensionDiagnosticsProvider
{
    /// <summary>Raised when diagnostics for a document change.</summary>
    event EventHandler<LanguageDiagnosticsChangedEventArgs>? DiagnosticsChanged;

    /// <summary>Gets diagnostics for the specified document.</summary>
    Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(LanguageDocumentContext context, CancellationToken ct);
}

/// <summary>Receives document open/change notifications.</summary>
public interface IExtensionDocumentSyncProvider
{
    /// <summary>Notifies that a document was opened.</summary>
    Task DocumentOpenedAsync(LanguageDocumentContext context, CancellationToken ct);

    /// <summary>Notifies that a document changed.</summary>
    Task DocumentChangedAsync(LanguageDocumentContext context, CancellationToken ct);
}
