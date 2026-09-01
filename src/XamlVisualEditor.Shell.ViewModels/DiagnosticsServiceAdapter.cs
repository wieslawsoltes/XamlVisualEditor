using System.Collections.Generic;
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
    private static readonly IReadOnlyList<DiagnosticsChannelInfo> EmptyChannels = Array.Empty<DiagnosticsChannelInfo>();
    private readonly MainWindowViewModel _mainViewModel;
    private IDisposable? _activeDocumentSubscription;
    private IDisposable? _diagnosticsSubscription;
    private IDisposable? _documentsSubscription;
    private readonly Dictionary<IEditorDocumentViewModel, IDisposable> _documentDiagnosticsSubscriptions = new();
    private TextDocumentViewModel? _activeDocument;
    private IReadOnlyList<DiagnosticsChannelInfo> _lastChannels = EmptyChannels;

    public DiagnosticsServiceAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _activeDocumentSubscription = _mainViewModel
            .WhenAnyValue(vm => vm.ActiveTextDocument)
            .Subscribe(UpdateActiveDocument);

        _documentsSubscription = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => _mainViewModel.Documents.CollectionChanged += h,
                h => _mainViewModel.Documents.CollectionChanged -= h)
            .Subscribe(_ =>
            {
                SyncDocumentSubscriptions();
                PublishSnapshotDiagnostics();
            });

        SyncDocumentSubscriptions();
        PublishSnapshotDiagnostics();
    }

    public event EventHandler<DiagnosticsPublishedEventArgs>? DiagnosticsPublished;

    public event EventHandler<DiagnosticsChannelsChangedEventArgs>? ChannelsChanged;

    public event EventHandler<DiagnosticsChannelPublishedEventArgs>? DiagnosticsChannelPublished;

    public event EventHandler<DiagnosticsSnapshotPublishedEventArgs>? DiagnosticsSnapshotPublished;

    public event EventHandler<DiagnosticsChangedEventArgs>? DiagnosticsChanged;

    public async Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(string? filePath, CancellationToken ct)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return GetDiagnosticsCore(filePath, null);
        }

        return await Dispatcher.UIThread.InvokeAsync(
            () => GetDiagnosticsCore(filePath, null),
            DispatcherPriority.Background,
            ct);
    }

    public async Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(DiagnosticsQuery query, CancellationToken ct)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return GetDiagnosticsCore(query.FilePath, query.ChannelId);
        }

        return await Dispatcher.UIThread.InvokeAsync(
            () => GetDiagnosticsCore(query.FilePath, query.ChannelId),
            DispatcherPriority.Background,
            ct);
    }

    public async Task<IReadOnlyList<DiagnosticsChannelInfo>> GetChannelsAsync(CancellationToken ct)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return GetChannelsCore();
        }

        return await Dispatcher.UIThread.InvokeAsync(
            GetChannelsCore,
            DispatcherPriority.Background,
            ct);
    }

    public async Task<IReadOnlyList<DiagnosticsDocumentSnapshot>> GetDiagnosticsSnapshotAsync(
        DiagnosticsQuery query,
        CancellationToken ct)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return GetDiagnosticsSnapshotCore(query);
        }

        return await Dispatcher.UIThread.InvokeAsync(
            () => GetDiagnosticsSnapshotCore(query),
            DispatcherPriority.Background,
            ct);
    }

    private IReadOnlyList<LanguageDiagnostic> GetDiagnosticsCore(string? filePath, string? channelId)
    {
        IEditorDocumentViewModel? document = string.IsNullOrWhiteSpace(filePath)
            ? _mainViewModel.ActiveDocument
            : FindDocument(filePath);
        if (document is null)
        {
            return Array.Empty<LanguageDiagnostic>();
        }

        return FilterDiagnostics(GetDocumentDiagnostics(document), channelId);
    }

    private IReadOnlyList<DiagnosticsChannelInfo> GetChannelsCore()
    {
        IReadOnlyList<LanguageDiagnostic> diagnostics = GetAllDiagnostics();
        return BuildChannels(diagnostics);
    }

    public void Dispose()
    {
        _diagnosticsSubscription?.Dispose();
        _activeDocumentSubscription?.Dispose();
        _documentsSubscription?.Dispose();
        _diagnosticsSubscription = null;
        _activeDocumentSubscription = null;
        _documentsSubscription = null;

        foreach (IDisposable subscription in _documentDiagnosticsSubscriptions.Values)
        {
            subscription.Dispose();
        }

        _documentDiagnosticsSubscriptions.Clear();
    }

    private void UpdateActiveDocument(TextDocumentViewModel? document)
    {
        _diagnosticsSubscription?.Dispose();
        _diagnosticsSubscription = null;
        _activeDocument = document;

        if (document is null)
        {
            DiagnosticsChanged?.Invoke(this, new DiagnosticsChangedEventArgs(null));
            DiagnosticsPublished?.Invoke(this, new DiagnosticsPublishedEventArgs(null, Array.Empty<LanguageDiagnostic>()));
            PublishChannelDiagnostics(null, Array.Empty<LanguageDiagnostic>());
            PublishSnapshotDiagnostics();
            return;
        }

        _diagnosticsSubscription = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => document.Diagnostics.CollectionChanged += h,
                h => document.Diagnostics.CollectionChanged -= h)
            .Subscribe(_ =>
            {
                IReadOnlyList<LanguageDiagnostic> diagnostics = document.Diagnostics.ToList();
                DiagnosticsChanged?.Invoke(this, new DiagnosticsChangedEventArgs(document.FilePath));
                DiagnosticsPublished?.Invoke(this, new DiagnosticsPublishedEventArgs(document.FilePath, diagnostics));
                PublishChannelDiagnostics(document.FilePath, diagnostics);
                PublishSnapshotDiagnostics();
            });

        IReadOnlyList<LanguageDiagnostic> initialDiagnostics = document.Diagnostics.ToList();
        DiagnosticsChanged?.Invoke(this, new DiagnosticsChangedEventArgs(document.FilePath));
        DiagnosticsPublished?.Invoke(this, new DiagnosticsPublishedEventArgs(document.FilePath, initialDiagnostics));
        PublishChannelDiagnostics(document.FilePath, initialDiagnostics);
        PublishSnapshotDiagnostics();
    }

    private void PublishChannelDiagnostics(string? filePath, IReadOnlyList<LanguageDiagnostic> diagnostics)
    {
        IReadOnlyList<DiagnosticsChannelInfo> channels = BuildChannels(GetAllDiagnostics());
        if (!ChannelsEqual(_lastChannels, channels))
        {
            _lastChannels = channels;
            ChannelsChanged?.Invoke(this, new DiagnosticsChannelsChangedEventArgs(channels));
        }

        if (channels.Count == 0)
        {
            return;
        }

        Dictionary<string, List<LanguageDiagnostic>> grouped = new(StringComparer.OrdinalIgnoreCase);
        foreach (LanguageDiagnostic diagnostic in diagnostics)
        {
            string channelId = GetChannelId(diagnostic.Source);
            if (!grouped.TryGetValue(channelId, out List<LanguageDiagnostic>? list))
            {
                list = new List<LanguageDiagnostic>();
                grouped[channelId] = list;
            }

            list.Add(diagnostic);
        }

        foreach (DiagnosticsChannelInfo channel in channels)
        {
            grouped.TryGetValue(channel.Id, out List<LanguageDiagnostic>? list);
            DiagnosticsChannelPublished?.Invoke(this, new DiagnosticsChannelPublishedEventArgs(
                channel.Id,
                filePath,
                list ?? (IReadOnlyList<LanguageDiagnostic>)Array.Empty<LanguageDiagnostic>()));
        }
    }

    private void PublishSnapshotDiagnostics()
    {
        IReadOnlyList<DiagnosticsDocumentSnapshot> snapshots = GetDiagnosticsSnapshotCore(new DiagnosticsQuery(null, null));
        DiagnosticsSnapshotPublished?.Invoke(this, new DiagnosticsSnapshotPublishedEventArgs(snapshots));
    }

    private IReadOnlyList<DiagnosticsDocumentSnapshot> GetDiagnosticsSnapshotCore(DiagnosticsQuery query)
    {
        List<DiagnosticsDocumentSnapshot> snapshots = new();
        if (!string.IsNullOrWhiteSpace(query.FilePath))
        {
            IEditorDocumentViewModel? doc = FindDocument(query.FilePath);
            if (doc is null)
            {
                return Array.Empty<DiagnosticsDocumentSnapshot>();
            }

            IReadOnlyList<LanguageDiagnostic> diagnostics = FilterDiagnostics(GetDocumentDiagnostics(doc), query.ChannelId);
            if (diagnostics.Count == 0)
            {
                return Array.Empty<DiagnosticsDocumentSnapshot>();
            }

            snapshots.Add(new DiagnosticsDocumentSnapshot(doc.FilePath, diagnostics));
            return snapshots;
        }

        foreach (IEditorDocumentViewModel doc in _mainViewModel.Documents)
        {
            IReadOnlyList<LanguageDiagnostic> diagnostics = FilterDiagnostics(GetDocumentDiagnostics(doc), query.ChannelId);
            if (diagnostics.Count == 0)
            {
                continue;
            }

            snapshots.Add(new DiagnosticsDocumentSnapshot(doc.FilePath, diagnostics));
        }

        return snapshots;
    }

    private IReadOnlyList<LanguageDiagnostic> GetAllDiagnostics()
    {
        List<LanguageDiagnostic> all = new();
        foreach (IEditorDocumentViewModel doc in _mainViewModel.Documents)
        {
            all.AddRange(GetDocumentDiagnostics(doc));
        }

        return all;
    }

    private IReadOnlyList<LanguageDiagnostic> FilterDiagnostics(
        IReadOnlyList<LanguageDiagnostic> diagnostics,
        string? channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return diagnostics;
        }

        List<LanguageDiagnostic> filtered = new();
        foreach (LanguageDiagnostic diagnostic in diagnostics)
        {
            if (string.Equals(GetChannelId(diagnostic.Source), channelId, StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(diagnostic);
            }
        }

        return filtered;
    }

    private void SyncDocumentSubscriptions()
    {
        HashSet<IEditorDocumentViewModel> active = new(_mainViewModel.Documents);
        foreach (IEditorDocumentViewModel existing in _documentDiagnosticsSubscriptions.Keys.ToList())
        {
            if (!active.Contains(existing))
            {
                _documentDiagnosticsSubscriptions[existing].Dispose();
                _documentDiagnosticsSubscriptions.Remove(existing);
            }
        }

        foreach (IEditorDocumentViewModel doc in active)
        {
            if (_documentDiagnosticsSubscriptions.ContainsKey(doc))
            {
                continue;
            }

            INotifyCollectionChanged? diagnosticsCollection = GetDiagnosticsCollection(doc);
            if (diagnosticsCollection is null)
            {
                continue;
            }

            IDisposable subscription = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                    h => diagnosticsCollection.CollectionChanged += h,
                    h => diagnosticsCollection.CollectionChanged -= h)
                .Subscribe(_ =>
                {
                    PublishSnapshotDiagnostics();
                    PublishChannelDiagnostics(doc.FilePath, GetDocumentDiagnostics(doc));
                });

            _documentDiagnosticsSubscriptions[doc] = subscription;
        }
    }

    private static INotifyCollectionChanged? GetDiagnosticsCollection(IEditorDocumentViewModel document)
    {
        return document switch
        {
            TextDocumentViewModel text => text.Diagnostics,
            DesignerDocumentViewModel designer => designer.CodeEditor.Diagnostics,
            _ => null,
        };
    }

    private static IReadOnlyList<LanguageDiagnostic> GetDocumentDiagnostics(IEditorDocumentViewModel document)
    {
        return document switch
        {
            TextDocumentViewModel text => text.Diagnostics.ToList(),
            DesignerDocumentViewModel designer => MapDesignerDiagnostics(designer),
            _ => Array.Empty<LanguageDiagnostic>(),
        };
    }

    private static IReadOnlyList<LanguageDiagnostic> MapDesignerDiagnostics(DesignerDocumentViewModel designer)
    {
        List<XamlDiagnostic> source = designer.CodeEditor.Diagnostics.ToList();
        List<LanguageDiagnostic> diagnostics = new(source.Count);
        foreach (XamlDiagnostic diagnostic in source)
        {
            LanguageTextPosition start = new(diagnostic.Line, diagnostic.Column);
            LanguageTextPosition end = new(diagnostic.Line, diagnostic.Column + Math.Max(1, diagnostic.Length));
            diagnostics.Add(new LanguageDiagnostic
            {
                FilePath = designer.FilePath,
                Message = diagnostic.Message,
                Severity = diagnostic.Severity,
                Range = new LanguageTextRange(start, end),
                Source = "XAML"
            });
        }

        return diagnostics;
    }

    private IEditorDocumentViewModel? FindDocument(string filePath)
    {
        foreach (IEditorDocumentViewModel doc in _mainViewModel.Documents)
        {
            if (string.Equals(doc.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return doc;
            }
        }

        return null;
    }

    private static IReadOnlyList<DiagnosticsChannelInfo> BuildChannels(IReadOnlyList<LanguageDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return EmptyChannels;
        }

        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        List<DiagnosticsChannelInfo> channels = new();
        foreach (LanguageDiagnostic diagnostic in diagnostics)
        {
            string channelId = GetChannelId(diagnostic.Source);
            if (ids.Add(channelId))
            {
                channels.Add(new DiagnosticsChannelInfo(channelId, channelId));
            }
        }

        return channels;
    }

    private static string GetChannelId(string? source)
    {
        return string.IsNullOrWhiteSpace(source) ? "default" : source;
    }

    private static bool ChannelsEqual(IReadOnlyList<DiagnosticsChannelInfo> left, IReadOnlyList<DiagnosticsChannelInfo> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i].Id, right[i].Id, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
