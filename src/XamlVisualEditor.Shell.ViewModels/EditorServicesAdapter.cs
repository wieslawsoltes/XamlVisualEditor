using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
        List<IEditorDocument> results = new(_mainViewModel.Documents.Count);
        foreach (IEditorDocumentViewModel doc in _mainViewModel.Documents)
        {
            results.Add(GetAdapter(doc));
        }

        return results;
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

        public event EventHandler<EditorDocumentChangedEventArgs>? Changed;

        public Task<string> GetTextAsync(CancellationToken ct)
        {
            return Task.FromResult(Document.Text);
        }

        public abstract Task ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct);

        public void Dispose()
        {
            _textChangedSubscription.Dispose();
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

        public CodeEditorDocumentAdapter(CodeEditorViewModel editor)
            : base(editor.Document, editor.FilePath, editor.LanguageId)
        {
            _editor = editor;
        }

        public override int CaretOffset
        {
            get => _editor.CaretOffset;
            set => _editor.SetCaretOffset(value);
        }

        public override Task ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct)
        {
            ApplyEdits(Document, edits);
            _editor.IsModified = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TextDocumentAdapter : EditorDocumentAdapter
    {
        private readonly TextDocumentViewModel _editor;

        public TextDocumentAdapter(TextDocumentViewModel editor)
            : base(editor.Document, editor.FilePath, editor.LanguageId)
        {
            _editor = editor;
        }

        public override int CaretOffset
        {
            get => _editor.CaretOffset;
            set => _editor.SetCaretOffset(value);
        }

        public override Task ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct)
        {
            _editor.ApplyTextEdits(edits);
            return Task.CompletedTask;
        }
    }
}
