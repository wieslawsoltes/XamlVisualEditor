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
}
