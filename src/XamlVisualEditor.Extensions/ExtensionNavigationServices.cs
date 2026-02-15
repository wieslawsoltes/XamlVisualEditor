using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;

namespace XamlVisualEditor.Extensions;

/// <summary>Provides language navigation services for extensions.</summary>
public interface ILanguageNavigationService
{
    /// <summary>Finds definitions for the symbol at the specified position.</summary>
    Task<IReadOnlyList<LanguageLocation>> FindDefinitionsAsync(LanguagePositionContext context, CancellationToken ct);

    /// <summary>Finds references for the symbol at the specified position.</summary>
    Task<IReadOnlyList<LanguageLocation>> FindReferencesAsync(LanguagePositionContext context, CancellationToken ct);

    /// <summary>Finds implementations for the symbol at the specified position.</summary>
    Task<IReadOnlyList<LanguageLocation>> FindImplementationsAsync(LanguagePositionContext context, CancellationToken ct);

    /// <summary>Searches workspace symbols.</summary>
    Task<IReadOnlyList<LanguageSymbol>> GetWorkspaceSymbolsAsync(LanguageSymbolQuery query, CancellationToken ct);

    /// <summary>Prepares rename for the symbol at the specified position.</summary>
    Task<LanguageRenameInfo?> PrepareRenameAsync(LanguagePositionContext context, CancellationToken ct);

    /// <summary>Renames the symbol at the specified position.</summary>
    Task<LanguageWorkspaceEdit?> RenameAsync(LanguageRenameContext context, CancellationToken ct);

    /// <summary>Gets code actions at the specified position.</summary>
    Task<IReadOnlyList<LanguageCodeAction>> GetCodeActionsAsync(LanguageCodeActionContext context, CancellationToken ct);

    /// <summary>Resolves additional code action data if required.</summary>
    Task<LanguageCodeAction?> ResolveCodeActionAsync(LanguageCodeAction action, CancellationToken ct);

    /// <summary>Applies a code action edit.</summary>
    Task<bool> ApplyCodeActionAsync(LanguageCodeAction action, CancellationToken ct);
}
