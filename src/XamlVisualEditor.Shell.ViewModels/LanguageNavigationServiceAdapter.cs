using System;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Adapts language intellisense services for navigation APIs.</summary>
public sealed class LanguageNavigationServiceAdapter : ILanguageNavigationService
{
    private readonly ILanguageIntellisenseRegistry _languageRegistry;
    private readonly IEditorServices _editor;

    public LanguageNavigationServiceAdapter(
        ILanguageIntellisenseRegistry languageRegistry,
        IEditorServices editor)
    {
        _languageRegistry = languageRegistry;
        _editor = editor;
    }

    public async Task<IReadOnlyList<LanguageLocation>> FindDefinitionsAsync(
        LanguagePositionContext context,
        CancellationToken ct)
    {
        ILanguageIntellisenseService? service = ResolveService(context.FilePath);
        if (service is null)
        {
            return Array.Empty<LanguageLocation>();
        }

        return await service.FindDefinitionsAsync(context, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LanguageLocation>> FindReferencesAsync(
        LanguagePositionContext context,
        CancellationToken ct)
    {
        ILanguageIntellisenseService? service = ResolveService(context.FilePath);
        if (service is null)
        {
            return Array.Empty<LanguageLocation>();
        }

        return await service.FindReferencesAsync(context, ct).ConfigureAwait(false);
    }

    private ILanguageIntellisenseService? ResolveService(string filePath)
    {
        string? languageId = null;
        foreach (IEditorDocument doc in _editor.GetOpenDocuments())
        {
            if (string.Equals(doc.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                languageId = doc.LanguageId;
                break;
            }
        }

        return _languageRegistry.GetService(filePath, languageId);
    }
}
