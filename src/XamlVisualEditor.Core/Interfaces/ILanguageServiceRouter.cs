namespace XamlVisualEditor.Core.Interfaces;

/// <summary>
/// Resolves language service sessions for the requested language and workspace.
/// </summary>
public interface ILanguageServiceRouter
{
    /// <summary>Gets a language service session for the given language.</summary>
    ValueTask<ILanguageServiceSession?> GetSessionAsync(
        string languageId,
        LanguageWorkspaceInfo workspace,
        CancellationToken ct = default);
}
