using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;
using AvaloniaTextDocument = AvaloniaEdit.Document.TextDocument;
using ReactiveUI;
using XamlVisualEditor.CodeEditor;
using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Adapts editor view models to extension editor services.</summary>
public sealed class EditorServicesAdapter : IEditorServices, IDisposable
{
    private readonly MainWindowViewModel _mainViewModel;
    private readonly Dictionary<IEditorDocumentViewModel, EditorDocumentAdapter> _documents = new();
    private readonly CompositeDisposable _disposables = new();

    public EditorServicesAdapter(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;

        _mainViewModel.Documents.CollectionChanged += OnDocumentsChanged;
        _disposables.Add(Disposable.Create(() => _mainViewModel.Documents.CollectionChanged -= OnDocumentsChanged));

        IDisposable activeSubscription = _mainViewModel.WhenAnyValue(x => x.ActiveDocument)
            .Subscribe(UpdateActiveDocument);
        _disposables.Add(activeSubscription);

        SyncDocuments();
        UpdateActiveDocument(_mainViewModel.ActiveDocument);
    }

    /// <inheritdoc />
    public IEditorDocument? ActiveDocument { get; private set; }

    /// <inheritdoc />
    public event EventHandler<EditorActiveDocumentChangedEventArgs>? ActiveDocumentChanged;

    /// <inheritdoc />
    public IReadOnlyList<IEditorDocument> GetOpenDocuments()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return GetOpenDocumentsCore();
        }

        return Dispatcher.UIThread
            .InvokeAsync(GetOpenDocumentsCore, DispatcherPriority.Background)
            .GetAwaiter()
            .GetResult();
    }

    private IReadOnlyList<IEditorDocument> GetOpenDocumentsCore()
    {
        List<IEditorDocument> results = new(_mainViewModel.Documents.Count);
        foreach (IEditorDocumentViewModel doc in _mainViewModel.Documents)
        {
            results.Add(GetAdapter(doc));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IEditorDocument?> OpenDocumentAsync(string filePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            return await OpenDocumentCoreAsync(filePath).ConfigureAwait(false);
        }

        IEditorDocument? result = null;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            result = await OpenDocumentCoreAsync(filePath);
        }, DispatcherPriority.Background, ct);

        return result;
    }

    private async Task<IEditorDocument?> OpenDocumentCoreAsync(string filePath)
    {
        await _mainViewModel.OpenFileAsync(filePath);
        IEditorDocumentViewModel? document = _mainViewModel.Documents
            .FirstOrDefault(doc => string.Equals(doc.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        return document is null ? null : GetAdapter(document);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        foreach (EditorDocumentAdapter adapter in _documents.Values)
        {
            adapter.Dispose();
        }

        _documents.Clear();
    }

    private void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncDocuments();
    }

    private void SyncDocuments()
    {
        HashSet<IEditorDocumentViewModel> active = new(_mainViewModel.Documents);
        foreach (IEditorDocumentViewModel existing in _documents.Keys.ToList())
        {
            if (!active.Contains(existing))
            {
                _documents[existing].Dispose();
                _documents.Remove(existing);
            }
        }

        foreach (IEditorDocumentViewModel doc in _mainViewModel.Documents)
        {
            _ = GetAdapter(doc);
        }
    }

    private EditorDocumentAdapter GetAdapter(IEditorDocumentViewModel document)
    {
        if (_documents.TryGetValue(document, out EditorDocumentAdapter? existing))
        {
            return existing;
        }

        EditorDocumentAdapter adapter = document switch
        {
            DesignerDocumentViewModel designer => new CodeEditorDocumentAdapter(designer.CodeEditor),
            TextDocumentViewModel text => new TextDocumentAdapter(text),
            _ => throw new InvalidOperationException("Unsupported document type: " + document.GetType().Name)
        };

        _documents[document] = adapter;
        return adapter;
    }

    private void UpdateActiveDocument(IEditorDocumentViewModel? doc)
    {
        ActiveDocument = doc is null ? null : GetAdapter(doc);
        ActiveDocumentChanged?.Invoke(this, new EditorActiveDocumentChangedEventArgs
        {
            Document = ActiveDocument
        });
    }

    private abstract class EditorDocumentAdapter : IEditorDocument, IDisposable
    {
        private readonly IDisposable _textChangedSubscription;
        private readonly CompositeDisposable _selectionSubscriptions = new();

        protected EditorDocumentAdapter(AvaloniaTextDocument document, string filePath, string? languageId)
        {
            Document = document;
            FilePath = filePath;
            LanguageId = languageId;

            _textChangedSubscription = Observable.FromEventPattern<EventHandler, EventArgs>(
                    h => Document.TextChanged += h,
                    h => Document.TextChanged -= h)
                .Subscribe(_ => Changed?.Invoke(this, new EditorDocumentChangedEventArgs
                {
                    FilePath = FilePath
                }));
        }

        protected AvaloniaTextDocument Document { get; }

        public string FilePath { get; }

        public string? LanguageId { get; }

        public abstract int CaretOffset { get; set; }

        public abstract int SelectionStart { get; set; }

        public abstract int SelectionLength { get; set; }

        public event EventHandler<EditorDocumentChangedEventArgs>? Changed;

        public event EventHandler<EditorSelectionChangedEventArgs>? SelectionChanged;

        public Task<string> GetTextAsync(CancellationToken ct)
        {
            return Task.FromResult(Document.Text);
        }

        public abstract Task ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct);

        public void Dispose()
        {
            _textChangedSubscription.Dispose();
            _selectionSubscriptions.Dispose();
        }

        protected void RegisterSelectionSubscription(IDisposable subscription)
        {
            _selectionSubscriptions.Add(subscription);
        }

        protected void RaiseSelectionChanged()
        {
            SelectionChanged?.Invoke(this, new EditorSelectionChangedEventArgs
            {
                FilePath = FilePath,
                SelectionStart = SelectionStart,
                SelectionLength = SelectionLength
            });
        }

        protected static void ApplyEdits(AvaloniaTextDocument document, IReadOnlyList<TextEdit> edits)
        {
            if (edits.Count == 0)
            {
                return;
            }

            foreach (TextEdit edit in edits.OrderByDescending(e => e.Offset))
            {
                int offset = Math.Clamp(edit.Offset, 0, document.TextLength);
                int length = Math.Clamp(edit.Length, 0, document.TextLength - offset);
                document.Replace(offset, length, edit.NewText);
            }
        }
    }

    private sealed class CodeEditorDocumentAdapter : EditorDocumentAdapter
    {
        private readonly CodeEditorViewModel _editor;

        public override int CaretOffset
        {
            get => _editor.CaretOffset;
            set => _editor.SetCaretOffset(value);
        }

        public override int SelectionStart
        {
            get => _editor.SelectionStart;
            set => _editor.SelectionStart = value;
        }

        public override int SelectionLength
        {
            get => _editor.SelectionLength;
            set => _editor.SelectionLength = value;
        }

        public override Task ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct)
        {
            ApplyEdits(Document, edits);
            _editor.IsModified = true;
            return Task.CompletedTask;
        }

        public CodeEditorDocumentAdapter(CodeEditorViewModel editor)
            : base(editor.Document, editor.FilePath, editor.LanguageId)
        {
            _editor = editor;

            IDisposable selectionSubscription = _editor.WhenAnyValue(x => x.SelectionStart, x => x.SelectionLength)
                .Subscribe(_ => RaiseSelectionChanged());
            RegisterSelectionSubscription(selectionSubscription);
        }
    }

    private sealed class TextDocumentAdapter : EditorDocumentAdapter
    {
        private readonly TextDocumentViewModel _editor;

        public override int CaretOffset
        {
            get => _editor.CaretOffset;
            set => _editor.SetCaretOffset(value);
        }

        public override int SelectionStart
        {
            get => _editor.SelectionStart;
            set => _editor.SelectionStart = value;
        }

        public override int SelectionLength
        {
            get => _editor.SelectionLength;
            set => _editor.SelectionLength = value;
        }

        public override Task ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct)
        {
            _editor.ApplyTextEdits(edits);
            return Task.CompletedTask;
        }

        public TextDocumentAdapter(TextDocumentViewModel editor)
            : base(editor.Document, editor.FilePath, editor.LanguageId)
        {
            _editor = editor;

            IDisposable selectionSubscription = _editor.WhenAnyValue(x => x.SelectionStart, x => x.SelectionLength)
                .Subscribe(_ => RaiseSelectionChanged());
            RegisterSelectionSubscription(selectionSubscription);
        }
    }
}
