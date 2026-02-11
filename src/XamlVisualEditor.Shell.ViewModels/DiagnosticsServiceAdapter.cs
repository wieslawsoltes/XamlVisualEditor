using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Adapts diagnostics from the shell to extension diagnostics service.</summary>
public sealed class DiagnosticsServiceAdapter : IDiagnosticsService, IDisposable
{
    private readonly MainWindowViewModel _mainViewModel;
    private IDisposable? _activeDocumentSubscription;
    private IDisposable? _diagnosticsSubscription;
    private TextDocumentViewModel? _activeDocument;

    public DiagnosticsServiceAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _activeDocumentSubscription = _mainViewModel
            .WhenAnyValue(vm => vm.ActiveTextDocument)
            .Subscribe(UpdateActiveDocument);
    }

    public event EventHandler<DiagnosticsChangedEventArgs>? DiagnosticsChanged;

    public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(string? filePath, CancellationToken ct)
    {
        TextDocumentViewModel? document = _activeDocument ?? _mainViewModel.ActiveTextDocument;
        if (document is null)
        {
            return Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());
        }

        if (string.IsNullOrWhiteSpace(filePath) || string.Equals(document.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(document.Diagnostics.ToList());
        }

        return Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());
    }

    public void Dispose()
    {
        _diagnosticsSubscription?.Dispose();
        _activeDocumentSubscription?.Dispose();
        _diagnosticsSubscription = null;
        _activeDocumentSubscription = null;
    }

    private void UpdateActiveDocument(TextDocumentViewModel? document)
    {
        _diagnosticsSubscription?.Dispose();
        _diagnosticsSubscription = null;
        _activeDocument = document;

        if (document is null)
        {
            DiagnosticsChanged?.Invoke(this, new DiagnosticsChangedEventArgs(null));
            return;
        }

        _diagnosticsSubscription = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => document.Diagnostics.CollectionChanged += h,
                h => document.Diagnostics.CollectionChanged -= h)
            .Subscribe(_ => DiagnosticsChanged?.Invoke(this, new DiagnosticsChangedEventArgs(document.FilePath)));

        DiagnosticsChanged?.Invoke(this, new DiagnosticsChangedEventArgs(document.FilePath));
    }
}
