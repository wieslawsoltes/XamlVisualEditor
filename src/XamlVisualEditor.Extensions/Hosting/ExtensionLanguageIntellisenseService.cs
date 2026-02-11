using System.IO;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Aggregates extension language providers into a single service.</summary>
public sealed class ExtensionLanguageIntellisenseService
    : ILanguageIntellisenseService, ILanguageDocumentSync, ILanguageDiagnosticsSource
{
    private readonly ExtensionLanguageServiceRegistry _registry;

    public ExtensionLanguageIntellisenseService(ExtensionLanguageServiceRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public string LanguageId => "extension";

    /// <inheritdoc />
    public event EventHandler<LanguageDiagnosticsChangedEventArgs> DiagnosticsChanged
    {
        add => _registry.DiagnosticsChanged += value;
        remove => _registry.DiagnosticsChanged -= value;
    }

    /// <inheritdoc />
    public bool CanHandle(string filePath, string? languageId)
    {
        return _registry.HasProviders(languageId);
    }

    /// <inheritdoc />
    public Task InitializeWorkspaceAsync(string workspacePath, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearWorkspaceAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken ct = default)
    {
        string? languageId = context.LanguageId ?? ResolveLanguageId(context.FilePath);
        IReadOnlyList<IExtensionCompletionProvider> providers =
            _registry.GetCompletionProviders(languageId);
        if (providers.Count == 0)
        {
            return Array.Empty<CompletionItem>();
        }

        List<CompletionItem> results = new();
        foreach (IExtensionCompletionProvider provider in providers)
        {
            try
            {
                IReadOnlyList<CompletionItem> items = await provider.GetCompletionsAsync(context, ct)
                    .ConfigureAwait(false);
                if (items.Count > 0)
                {
                    results.AddRange(items);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        return results.Count == 0 ? Array.Empty<CompletionItem>() : results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default)
    {
        string? languageId = ResolveLanguageId(context.FilePath);
        IReadOnlyList<IExtensionDiagnosticsProvider> providers =
            _registry.GetDiagnosticsProviders(languageId);
        if (providers.Count == 0)
        {
            return Array.Empty<LanguageDiagnostic>();
        }

        List<LanguageDiagnostic> results = new();
        foreach (IExtensionDiagnosticsProvider provider in providers)
        {
            try
            {
                IReadOnlyList<LanguageDiagnostic> items = await provider.GetDiagnosticsAsync(context, ct)
                    .ConfigureAwait(false);
                if (items.Count > 0)
                {
                    results.AddRange(items);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        return results.Count == 0 ? Array.Empty<LanguageDiagnostic>() : results;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LanguageSemanticToken>> GetSemanticTokensAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<LanguageSemanticToken>>(Array.Empty<LanguageSemanticToken>());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default)
    {
        string? languageId = ResolveLanguageId(context.FilePath);
        IReadOnlyList<IExtensionFormattingProvider> providers =
            _registry.GetFormattingProviders(languageId);
        foreach (IExtensionFormattingProvider provider in providers)
        {
            try
            {
                IReadOnlyList<TextEdit> edits = await provider.GetFormattingEditsAsync(context, ct)
                    .ConfigureAwait(false);
                if (edits.Count > 0)
                {
                    return edits;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        return Array.Empty<TextEdit>();
    }

    /// <inheritdoc />
    public async Task<LanguageHover?> GetHoverAsync(LanguagePositionContext context, CancellationToken ct = default)
    {
        IReadOnlyList<IExtensionHoverProvider> providers = _registry.GetHoverProviders(ResolveLanguageId(context.FilePath));
        foreach (IExtensionHoverProvider provider in providers)
        {
            try
            {
                LanguageHover? hover = await provider.GetHoverAsync(context, ct).ConfigureAwait(false);
                if (hover is not null)
                {
                    return hover;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LanguageLocation>> FindDefinitionsAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        IReadOnlyList<IExtensionDefinitionProvider> providers =
            _registry.GetDefinitionProviders(ResolveLanguageId(context.FilePath));
        if (providers.Count == 0)
        {
            return Array.Empty<LanguageLocation>();
        }

        List<LanguageLocation> results = new();
        foreach (IExtensionDefinitionProvider provider in providers)
        {
            try
            {
                IReadOnlyList<LanguageLocation> items = await provider.FindDefinitionsAsync(context, ct)
                    .ConfigureAwait(false);
                if (items.Count > 0)
                {
                    results.AddRange(items);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        return results.Count == 0 ? Array.Empty<LanguageLocation>() : results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LanguageLocation>> FindReferencesAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        IReadOnlyList<IExtensionReferencesProvider> providers =
            _registry.GetReferencesProviders(ResolveLanguageId(context.FilePath));
        if (providers.Count == 0)
        {
            return Array.Empty<LanguageLocation>();
        }

        List<LanguageLocation> results = new();
        foreach (IExtensionReferencesProvider provider in providers)
        {
            try
            {
                IReadOnlyList<LanguageLocation> items = await provider.FindReferencesAsync(context, ct)
                    .ConfigureAwait(false);
                if (items.Count > 0)
                {
                    results.AddRange(items);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        return results.Count == 0 ? Array.Empty<LanguageLocation>() : results;
    }

    /// <inheritdoc />
    public Task<LanguageRenameInfo?> PrepareRenameAsync(LanguagePositionContext context, CancellationToken ct = default)
    {
        return Task.FromResult<LanguageRenameInfo?>(null);
    }

    /// <inheritdoc />
    public Task<LanguageWorkspaceEdit?> RenameSymbolAsync(LanguageRenameContext context, CancellationToken ct = default)
    {
        return Task.FromResult<LanguageWorkspaceEdit?>(null);
    }

    /// <inheritdoc />
    public async Task<LanguageSignatureHelp?> GetSignatureHelpAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        IReadOnlyList<IExtensionSignatureHelpProvider> providers =
            _registry.GetSignatureHelpProviders(ResolveLanguageId(context.FilePath));
        foreach (IExtensionSignatureHelpProvider provider in providers)
        {
            try
            {
                LanguageSignatureHelp? help = await provider.GetSignatureHelpAsync(context, ct).ConfigureAwait(false);
                if (help is not null)
                {
                    return help;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LanguageCodeAction>> GetCodeActionsAsync(
        LanguageCodeActionContext context,
        CancellationToken ct = default)
    {
        IReadOnlyList<IExtensionCodeActionsProvider> providers =
            _registry.GetCodeActionsProviders(ResolveLanguageId(context.FilePath));
        if (providers.Count == 0)
        {
            return Array.Empty<LanguageCodeAction>();
        }

        List<LanguageCodeAction> results = new();
        foreach (IExtensionCodeActionsProvider provider in providers)
        {
            try
            {
                IReadOnlyList<LanguageCodeAction> items = await provider.GetCodeActionsAsync(context, ct)
                    .ConfigureAwait(false);
                if (items.Count > 0)
                {
                    results.AddRange(items);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        return results.Count == 0 ? Array.Empty<LanguageCodeAction>() : results;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LanguageSymbol>> GetDocumentSymbolsAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<LanguageSymbol>>(Array.Empty<LanguageSymbol>());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LanguageSymbol>> GetWorkspaceSymbolsAsync(
        LanguageSymbolQuery query,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<LanguageSymbol>>(Array.Empty<LanguageSymbol>());
    }

    /// <inheritdoc />
    public async Task DocumentOpenedAsync(LanguageDocumentContext context, CancellationToken ct = default)
    {
        IReadOnlyList<IExtensionDocumentSyncProvider> providers =
            _registry.GetDocumentSyncProviders(ResolveLanguageId(context.FilePath));
        foreach (IExtensionDocumentSyncProvider provider in providers)
        {
            try
            {
                await provider.DocumentOpenedAsync(context, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }
    }

    /// <inheritdoc />
    public async Task DocumentChangedAsync(LanguageDocumentContext context, CancellationToken ct = default)
    {
        IReadOnlyList<IExtensionDocumentSyncProvider> providers =
            _registry.GetDocumentSyncProviders(ResolveLanguageId(context.FilePath));
        foreach (IExtensionDocumentSyncProvider provider in providers)
        {
            try
            {
                await provider.DocumentChangedAsync(context, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }
    }

    private static string? ResolveLanguageId(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        string extension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        return extension.TrimStart('.');
    }
}
