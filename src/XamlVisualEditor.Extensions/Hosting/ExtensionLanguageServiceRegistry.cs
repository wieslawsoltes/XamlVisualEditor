using XamlVisualEditor.Core;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Stores extension language service providers.</summary>
public sealed class ExtensionLanguageServiceRegistry : IExtensionLanguageServices
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ExtensionLanguageProviderSet> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised when diagnostics change for a document.</summary>
    public event EventHandler<LanguageDiagnosticsChangedEventArgs>? DiagnosticsChanged;

    /// <inheritdoc />
    public IDisposable RegisterCompletionProvider(string languageId, IExtensionCompletionProvider provider)
    {
        return RegisterProvider(languageId, provider, set => set.CompletionProviders.Add(provider),
            set => set.CompletionProviders.Remove(provider));
    }

    /// <inheritdoc />
    public IDisposable RegisterHoverProvider(string languageId, IExtensionHoverProvider provider)
    {
        return RegisterProvider(languageId, provider, set => set.HoverProviders.Add(provider),
            set => set.HoverProviders.Remove(provider));
    }

    /// <inheritdoc />
    public IDisposable RegisterDefinitionProvider(string languageId, IExtensionDefinitionProvider provider)
    {
        return RegisterProvider(languageId, provider, set => set.DefinitionProviders.Add(provider),
            set => set.DefinitionProviders.Remove(provider));
    }

    /// <inheritdoc />
    public IDisposable RegisterReferencesProvider(string languageId, IExtensionReferencesProvider provider)
    {
        return RegisterProvider(languageId, provider, set => set.ReferencesProviders.Add(provider),
            set => set.ReferencesProviders.Remove(provider));
    }

    /// <inheritdoc />
    public IDisposable RegisterSignatureHelpProvider(string languageId, IExtensionSignatureHelpProvider provider)
    {
        return RegisterProvider(languageId, provider, set => set.SignatureHelpProviders.Add(provider),
            set => set.SignatureHelpProviders.Remove(provider));
    }

    /// <inheritdoc />
    public IDisposable RegisterCodeActionsProvider(string languageId, IExtensionCodeActionsProvider provider)
    {
        return RegisterProvider(languageId, provider, set => set.CodeActionsProviders.Add(provider),
            set => set.CodeActionsProviders.Remove(provider));
    }

    /// <inheritdoc />
    public IDisposable RegisterFormattingProvider(string languageId, IExtensionFormattingProvider provider)
    {
        return RegisterProvider(languageId, provider, set => set.FormattingProviders.Add(provider),
            set => set.FormattingProviders.Remove(provider));
    }

    /// <inheritdoc />
    public IDisposable RegisterDiagnosticsProvider(string languageId, IExtensionDiagnosticsProvider provider)
    {
        return RegisterProvider(
            languageId,
            provider,
            set => set.DiagnosticsProviders.Add(provider),
            set => set.DiagnosticsProviders.Remove(provider),
            () => provider.DiagnosticsChanged += OnDiagnosticsChanged,
            () => provider.DiagnosticsChanged -= OnDiagnosticsChanged);
    }

    /// <inheritdoc />
    public IDisposable RegisterDocumentSyncProvider(string languageId, IExtensionDocumentSyncProvider provider)
    {
        return RegisterProvider(languageId, provider, set => set.DocumentSyncProviders.Add(provider),
            set => set.DocumentSyncProviders.Remove(provider));
    }

    internal bool HasProviders(string? languageId)
    {
        if (string.IsNullOrWhiteSpace(languageId))
        {
            return false;
        }

        lock (_gate)
        {
            return HasProvidersUnsafe(languageId) || HasProvidersUnsafe("*");
        }
    }

    internal IReadOnlyList<IExtensionCompletionProvider> GetCompletionProviders(string? languageId)
        => CollectProviders(languageId, set => set.CompletionProviders);

    internal IReadOnlyList<IExtensionHoverProvider> GetHoverProviders(string? languageId)
        => CollectProviders(languageId, set => set.HoverProviders);

    internal IReadOnlyList<IExtensionDefinitionProvider> GetDefinitionProviders(string? languageId)
        => CollectProviders(languageId, set => set.DefinitionProviders);

    internal IReadOnlyList<IExtensionReferencesProvider> GetReferencesProviders(string? languageId)
        => CollectProviders(languageId, set => set.ReferencesProviders);

    internal IReadOnlyList<IExtensionSignatureHelpProvider> GetSignatureHelpProviders(string? languageId)
        => CollectProviders(languageId, set => set.SignatureHelpProviders);

    internal IReadOnlyList<IExtensionCodeActionsProvider> GetCodeActionsProviders(string? languageId)
        => CollectProviders(languageId, set => set.CodeActionsProviders);

    internal IReadOnlyList<IExtensionFormattingProvider> GetFormattingProviders(string? languageId)
        => CollectProviders(languageId, set => set.FormattingProviders);

    internal IReadOnlyList<IExtensionDiagnosticsProvider> GetDiagnosticsProviders(string? languageId)
        => CollectProviders(languageId, set => set.DiagnosticsProviders);

    internal IReadOnlyList<IExtensionDocumentSyncProvider> GetDocumentSyncProviders(string? languageId)
        => CollectProviders(languageId, set => set.DocumentSyncProviders);

    private IDisposable RegisterProvider<T>(
        string languageId,
        T provider,
        Action<ExtensionLanguageProviderSet> add,
        Action<ExtensionLanguageProviderSet> remove,
        Action? attach = null,
        Action? detach = null)
    {
        if (string.IsNullOrWhiteSpace(languageId))
        {
            throw new ArgumentException("Language id is required.", nameof(languageId));
        }

        if (provider is null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        lock (_gate)
        {
            ExtensionLanguageProviderSet set = GetOrCreate(languageId);
            add(set);
        }

        attach?.Invoke();

        return new Registration(() =>
        {
            lock (_gate)
            {
                if (_providers.TryGetValue(languageId, out ExtensionLanguageProviderSet? set))
                {
                    remove(set);
                    if (set.IsEmpty)
                    {
                        _providers.Remove(languageId);
                    }
                }
            }

            detach?.Invoke();
        });
    }

    private ExtensionLanguageProviderSet GetOrCreate(string languageId)
    {
        if (!_providers.TryGetValue(languageId, out ExtensionLanguageProviderSet? set))
        {
            set = new ExtensionLanguageProviderSet();
            _providers[languageId] = set;
        }

        return set;
    }

    private bool HasProvidersUnsafe(string languageId)
    {
        return _providers.TryGetValue(languageId, out ExtensionLanguageProviderSet? set) && !set.IsEmpty;
    }

    private IReadOnlyList<T> CollectProviders<T>(
        string? languageId,
        Func<ExtensionLanguageProviderSet, List<T>> selector)
    {
        if (string.IsNullOrWhiteSpace(languageId))
        {
            return Array.Empty<T>();
        }

        List<T> results = new();
        lock (_gate)
        {
            if (_providers.TryGetValue(languageId, out ExtensionLanguageProviderSet? set))
            {
                results.AddRange(selector(set));
            }

            if (_providers.TryGetValue("*", out ExtensionLanguageProviderSet? global))
            {
                results.AddRange(selector(global));
            }
        }

        return results.Count == 0 ? Array.Empty<T>() : results;
    }

    private void OnDiagnosticsChanged(object? sender, LanguageDiagnosticsChangedEventArgs e)
    {
        DiagnosticsChanged?.Invoke(this, e);
    }

    private sealed class ExtensionLanguageProviderSet
    {
        public List<IExtensionCompletionProvider> CompletionProviders { get; } = new();
        public List<IExtensionHoverProvider> HoverProviders { get; } = new();
        public List<IExtensionDefinitionProvider> DefinitionProviders { get; } = new();
        public List<IExtensionReferencesProvider> ReferencesProviders { get; } = new();
        public List<IExtensionSignatureHelpProvider> SignatureHelpProviders { get; } = new();
        public List<IExtensionCodeActionsProvider> CodeActionsProviders { get; } = new();
        public List<IExtensionFormattingProvider> FormattingProviders { get; } = new();
        public List<IExtensionDiagnosticsProvider> DiagnosticsProviders { get; } = new();
        public List<IExtensionDocumentSyncProvider> DocumentSyncProviders { get; } = new();

        public bool IsEmpty => CompletionProviders.Count == 0
            && HoverProviders.Count == 0
            && DefinitionProviders.Count == 0
            && ReferencesProviders.Count == 0
            && SignatureHelpProviders.Count == 0
            && CodeActionsProviders.Count == 0
            && FormattingProviders.Count == 0
            && DiagnosticsProviders.Count == 0
            && DocumentSyncProviders.Count == 0;
    }

    private sealed class Registration : IDisposable
    {
        private readonly Action _dispose;
        private bool _isDisposed;

        public Registration(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _dispose();
            _isDisposed = true;
        }
    }
}
