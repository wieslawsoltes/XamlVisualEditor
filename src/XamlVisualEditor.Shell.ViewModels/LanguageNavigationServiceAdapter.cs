using System;
using System.Collections.Generic;
using System.Linq;
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

    public Task<IReadOnlyList<LanguageLocation>> FindImplementationsAsync(
        LanguagePositionContext context,
        CancellationToken ct)
    {
        // The current intellisense contract does not expose a dedicated implementation query.
        // Use definitions as a compatibility fallback.
        return FindDefinitionsAsync(context, ct);
    }

    public async Task<IReadOnlyList<LanguageSymbol>> GetWorkspaceSymbolsAsync(
        LanguageSymbolQuery query,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Query))
        {
            return Array.Empty<LanguageSymbol>();
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<LanguageSymbol> results = new();
        foreach (ILanguageIntellisenseService service in _languageRegistry.Services.Distinct())
        {
            IReadOnlyList<LanguageSymbol> symbols = await service.GetWorkspaceSymbolsAsync(query, ct).ConfigureAwait(false);
            foreach (LanguageSymbol symbol in symbols)
            {
                string key = $"{symbol.FilePath}|{symbol.Range.Start.Line}|{symbol.Range.Start.Column}|{symbol.Name}|{symbol.Kind}";
                if (seen.Add(key))
                {
                    results.Add(symbol);
                }
            }
        }

        return results;
    }

    public async Task<LanguageRenameInfo?> PrepareRenameAsync(
        LanguagePositionContext context,
        CancellationToken ct)
    {
        ILanguageIntellisenseService? service = ResolveService(context.FilePath);
        if (service is null)
        {
            return null;
        }

        return await service.PrepareRenameAsync(context, ct).ConfigureAwait(false);
    }

    public async Task<LanguageWorkspaceEdit?> RenameAsync(
        LanguageRenameContext context,
        CancellationToken ct)
    {
        ILanguageIntellisenseService? service = ResolveService(context.FilePath);
        if (service is null)
        {
            return null;
        }

        return await service.RenameSymbolAsync(context, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LanguageCodeAction>> GetCodeActionsAsync(
        LanguageCodeActionContext context,
        CancellationToken ct)
    {
        ILanguageIntellisenseService? service = ResolveService(context.FilePath);
        if (service is null)
        {
            return Array.Empty<LanguageCodeAction>();
        }

        return await service.GetCodeActionsAsync(context, ct).ConfigureAwait(false);
    }

    public Task<LanguageCodeAction?> ResolveCodeActionAsync(LanguageCodeAction action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<LanguageCodeAction?>(action);
    }

    public async Task<bool> ApplyCodeActionAsync(LanguageCodeAction action, CancellationToken ct)
    {
        if (action.Edit is null)
        {
            return false;
        }

        await ApplyWorkspaceEditAsync(action.Edit, ct).ConfigureAwait(false);
        return true;
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

    private async Task ApplyWorkspaceEditAsync(LanguageWorkspaceEdit edit, CancellationToken ct)
    {
        foreach (LanguageDocumentEdit docEdit in edit.DocumentEdits)
        {
            if (string.IsNullOrWhiteSpace(docEdit.FilePath) || docEdit.Edits.Count == 0)
            {
                continue;
            }

            ct.ThrowIfCancellationRequested();

            IEditorDocument? document = _editor.GetOpenDocuments()
                .FirstOrDefault(doc =>
                    string.Equals(doc.FilePath, docEdit.FilePath, StringComparison.OrdinalIgnoreCase));

            if (document is null)
            {
                document = await _editor
                    .OpenDocumentAsync(docEdit.FilePath, EditorDocumentOpenBehavior.DocumentOnly, ct)
                    .ConfigureAwait(false);
            }

            if (document is null)
            {
                continue;
            }

            await document.ApplyEditsAsync(docEdit.Edits, ct).ConfigureAwait(false);
        }
    }
}
