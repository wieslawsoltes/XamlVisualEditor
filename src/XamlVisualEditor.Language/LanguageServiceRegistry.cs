using XamlVisualEditor.Core.Interfaces;

namespace XamlVisualEditor.Language;

/// <summary>
/// Resolves language services for documents.
/// </summary>
public sealed class LanguageServiceRegistry : ILanguageIntellisenseRegistry
{
    private readonly IReadOnlyList<ILanguageIntellisenseService> _services;

    public LanguageServiceRegistry(IEnumerable<ILanguageIntellisenseService> services)
    {
        _services = services.ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ILanguageIntellisenseService> Services => _services;

    /// <inheritdoc />
    public ILanguageIntellisenseService? GetService(string filePath, string? languageId)
    {
        foreach (ILanguageIntellisenseService service in _services)
        {
            if (service.CanHandle(filePath, languageId))
            {
                return service;
            }
        }

        return null;
    }
}
