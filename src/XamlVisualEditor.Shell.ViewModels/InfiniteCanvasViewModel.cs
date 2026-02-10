using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using Dock.Model.ReactiveUI.Controls;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Shell;

namespace XamlVisualEditor.Shell.ViewModels;

public sealed record CanvasDropInfo(double X, double Y, IReadOnlyList<string> Paths);

public sealed record OpenDocumentEntry(IEditorDocumentViewModel Document)
{
    public string FilePath => Document.FilePath;

    public string DisplayName => Document.FileName;
}

public sealed class InfiniteCanvasViewModel : ReactiveObject, IDisposable
{
    private const double DefaultDocumentWidth = 520;
    private const double DefaultDocumentHeight = 360;
    private const int SaveThrottleMs = 400;
    private const double OpenDocumentSeedOffset = 28;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xaml",
        ".axaml",
        ".cs",
        ".csx",
        ".json",
        ".xml",
        ".txt",
        ".md",
        ".yaml",
        ".yml"
    };

    private readonly CompositeDisposable _disposables = new();
    private readonly Dictionary<CanvasMdiDocument, IDisposable> _documentSubscriptions = new();
    private readonly Dictionary<IEditorDocumentViewModel, CanvasMdiDocument> _documentMap = new();
    private readonly Subject<Unit> _saveRequests = new();
    private readonly ILanguageIntellisenseRegistry? _languageRegistry;
    private readonly DocumentDock _documentDock;
    private readonly ILogger<InfiniteCanvasViewModel> _logger;
    private bool _isLoadingLayout;
    private int _openDocumentSeed;

    public InfiniteCanvasViewModel(
        ILanguageIntellisenseRegistry? languageRegistry,
        ILogger<InfiniteCanvasViewModel>? logger = null)
    {
        _languageRegistry = languageRegistry;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InfiniteCanvasViewModel>.Instance;
        DockFactory = new CanvasDockFactory();
        DockLayout = DockFactory.CreateLayout();
        DockFactory.InitLayout(DockLayout);
        DockFactory.EnsureOwnerReferences(DockLayout);

        _documentDock = DockFactory.DocumentDock ?? throw new InvalidOperationException("Document dock was not created.");
        Documents = _documentDock.VisibleDockables as ObservableCollection<IDockable>
            ?? throw new InvalidOperationException("Document dock collection is not initialized.");
        Documents.CollectionChanged += OnDocumentsChanged;

        DropFilesCommand = ReactiveCommand.CreateFromTask<CanvasDropInfo>(HandleDropAsync);
        AddOpenDocumentCommand = ReactiveCommand.Create<OpenDocumentEntry?>(AddOpenDocument);
        AddAllOpenDocumentsCommand = ReactiveCommand.Create(AddAllOpenDocuments);

        DockFactory.DockableClosed += OnDockableClosed;

        IDisposable saveSubscription = _saveRequests
            .Throttle(TimeSpan.FromMilliseconds(SaveThrottleMs))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => SaveLayout());
        _disposables.Add(saveSubscription);

        LoadLayout();
    }

    public CanvasDockFactory DockFactory { get; }

    public IRootDock DockLayout { get; }

    public ObservableCollection<IDockable> Documents { get; }

    public ObservableCollection<OpenDocumentEntry> OpenDocuments { get; } = new();

    public ReactiveCommand<CanvasDropInfo, Unit> DropFilesCommand { get; }

    public ReactiveCommand<OpenDocumentEntry?, Unit> AddOpenDocumentCommand { get; }

    public ReactiveCommand<Unit, Unit> AddAllOpenDocumentsCommand { get; }

    [Reactive]
    public OpenDocumentEntry? SelectedOpenDocument { get; set; }

    public void Dispose()
    {
        SaveLayout();
        DockFactory.DockableClosed -= OnDockableClosed;
        _disposables.Dispose();

        foreach (CanvasMdiDocument doc in _documentMap.Values.ToList())
        {
            if (doc.IsOwned)
            {
                doc.DocumentViewModel.Dispose();
            }
        }

        _documentMap.Clear();
    }

    public void UpdateOpenDocuments(IEnumerable<IEditorDocumentViewModel> documents)
    {
        HashSet<IEditorDocumentViewModel> existing = new(OpenDocuments.Select(d => d.Document));
        List<IEditorDocumentViewModel> source = documents.ToList();

        for (int i = OpenDocuments.Count - 1; i >= 0; i--)
        {
            if (!source.Contains(OpenDocuments[i].Document))
            {
                OpenDocuments.RemoveAt(i);
            }
        }

        foreach (IEditorDocumentViewModel doc in source)
        {
            if (!existing.Contains(doc))
            {
                OpenDocuments.Add(new OpenDocumentEntry(doc));
            }
        }
    }

    public void RemoveOpenDocumentItem(IEditorDocumentViewModel document)
    {
        if (!_documentMap.TryGetValue(document, out CanvasMdiDocument? dockable))
        {
            return;
        }

        if (dockable.IsOwned)
        {
            return;
        }

        DockFactory.CloseDockable(dockable);
    }

    private async System.Threading.Tasks.Task HandleDropAsync(CanvasDropInfo drop)
    {
        double offset = 0;
        foreach (string path in drop.Paths)
        {
            if (!IsSupportedPath(path))
            {
                continue;
            }

            DockRect bounds = new(drop.X + offset, drop.Y + offset, DefaultDocumentWidth, DefaultDocumentHeight);
            AddOrActivateFileDocument(path, bounds, isOwned: true);
            offset += 24;
        }

        await System.Threading.Tasks.Task.CompletedTask;
    }

    private void AddOpenDocument(OpenDocumentEntry? entry)
    {
        if (entry?.Document is null)
        {
            return;
        }

        DockRect bounds = CreateSeedBounds();
        AddOrActivateOpenDocument(entry.Document, bounds);
    }

    private void AddAllOpenDocuments()
    {
        foreach (OpenDocumentEntry entry in OpenDocuments)
        {
            AddOpenDocument(entry);
        }
    }

    private void AddOrActivateOpenDocument(IEditorDocumentViewModel document, DockRect bounds)
    {
        if (_documentMap.TryGetValue(document, out CanvasMdiDocument? existing))
        {
            DockFactory.SetActiveDockable(existing);
            DockFactory.SetFocusedDockable(_documentDock, existing);
            return;
        }

        CanvasMdiDocument dockable = new(document, isOwned: false)
        {
            MdiBounds = bounds,
            MdiState = MdiWindowState.Normal
        };

        AddDockable(dockable);
    }

    private void AddOrActivateFileDocument(string filePath, DockRect bounds, bool isOwned)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        TextDocumentViewModel document = new(filePath, _languageRegistry);
        CanvasMdiDocument dockable = new(document, isOwned)
        {
            MdiBounds = bounds,
            MdiState = MdiWindowState.Normal
        };

        AddDockable(dockable);
        _ = document.LoadAsync();
    }

    private void AddDockable(CanvasMdiDocument dockable)
    {
        DockFactory.AddDockable(_documentDock, dockable);
        DockFactory.SetActiveDockable(dockable);
        DockFactory.SetFocusedDockable(_documentDock, dockable);
        _documentMap[dockable.DocumentViewModel] = dockable;
        RequestSave();
    }

    private DockRect CreateSeedBounds()
    {
        double offset = _openDocumentSeed * OpenDocumentSeedOffset;
        _openDocumentSeed++;
        return new DockRect(40 + offset, 40 + offset, DefaultDocumentWidth, DefaultDocumentHeight);
    }

    private void OnDockableClosed(object? sender, DockableClosedEventArgs e)
    {
        if (e.Dockable is not CanvasMdiDocument doc)
        {
            return;
        }

        if (_documentMap.Remove(doc.DocumentViewModel))
        {
            if (doc.IsOwned)
            {
                doc.DocumentViewModel.Dispose();
            }
        }

        RequestSave();
    }

    private void OnDocumentsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (IDockable item in e.OldItems)
            {
                if (item is CanvasMdiDocument doc && _documentSubscriptions.Remove(doc, out IDisposable? subscription))
                {
                    subscription.Dispose();
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (IDockable item in e.NewItems)
            {
                if (item is not CanvasMdiDocument doc || !doc.IsOwned)
                {
                    continue;
                }

                IDisposable subscription = doc.WhenAnyValue(x => x.MdiBounds, x => x.MdiState)
                    .Subscribe(_ => RequestSave());
                _documentSubscriptions[doc] = subscription;
            }
        }

        RequestSave();
    }

    private void RequestSave()
    {
        if (_isLoadingLayout)
        {
            return;
        }

        _saveRequests.OnNext(Unit.Default);
    }

    private void LoadLayout()
    {
        _isLoadingLayout = true;
        try
        {
            string path = GetLayoutPath();
            if (!File.Exists(path))
            {
                return;
            }

            string json = File.ReadAllText(path);
            MdiCanvasLayoutState? layout = JsonSerializer.Deserialize<MdiCanvasLayoutState>(json);
            if (layout?.Items is null)
            {
                return;
            }

            foreach (MdiCanvasItemState state in layout.Items)
            {
                if (!IsSupportedPath(state.FilePath))
                {
                    continue;
                }

                if (!File.Exists(state.FilePath))
                {
                    continue;
                }

                TextDocumentViewModel document = new(state.FilePath, _languageRegistry);
                CanvasMdiDocument dockable = new(document, isOwned: true)
                {
                    MdiBounds = new DockRect(state.X, state.Y, state.Width, state.Height),
                    MdiState = state.State
                };

                AddDockable(dockable);
                _ = document.LoadAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to load MDI canvas layout: {Message}", ex.Message);
        }
        finally
        {
            _isLoadingLayout = false;
        }
    }

    private void SaveLayout()
    {
        if (_isLoadingLayout)
        {
            return;
        }

        try
        {
            List<MdiCanvasItemState> items = _documentMap.Values
                .Where(doc => doc.IsOwned)
                .Select(doc => new MdiCanvasItemState(
                    doc.FilePath,
                    doc.MdiBounds.X,
                    doc.MdiBounds.Y,
                    doc.MdiBounds.Width,
                    doc.MdiBounds.Height,
                    doc.MdiState))
                .ToList();

            MdiCanvasLayoutState layout = new(items);
            string json = JsonSerializer.Serialize(layout, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetLayoutPath(), json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to save MDI canvas layout: {Message}", ex.Message);
        }
    }

    private static string GetLayoutPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, "XamlVisualEditor");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "mdi-canvas-layout.json");
    }

    private static bool IsSupportedPath(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(extension);
    }

    private sealed record MdiCanvasLayoutState(IReadOnlyList<MdiCanvasItemState> Items);

    private sealed record MdiCanvasItemState(
        string FilePath,
        double X,
        double Y,
        double Width,
        double Height,
        MdiWindowState State);
}
