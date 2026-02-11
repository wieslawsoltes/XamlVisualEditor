using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Threading;
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

    public async Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(string? filePath, CancellationToken ct)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return GetDiagnosticsCore(filePath);
        }

        return await Dispatcher.UIThread.InvokeAsync(
            () => GetDiagnosticsCore(filePath),
            DispatcherPriority.Background,
            ct);
    }

    private IReadOnlyList<LanguageDiagnostic> GetDiagnosticsCore(string? filePath)
    {
        TextDocumentViewModel? document = _activeDocument ?? _mainViewModel.ActiveTextDocument;
        if (document is null)
        {
            return Array.Empty<LanguageDiagnostic>();
        }

        if (string.IsNullOrWhiteSpace(filePath) || string.Equals(document.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            return document.Diagnostics.ToList();
        }

        return Array.Empty<LanguageDiagnostic>();
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
