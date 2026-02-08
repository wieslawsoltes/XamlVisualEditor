using System;
using System.Reactive.Disposables;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using TextMateSharp.Grammars;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

/// <summary>
/// Code-behind for the generic text file view with TextMate syntax highlighting.
/// </summary>
public sealed partial class TextFileView : UserControl
{
    private TextMate.Installation? _textMateInstallation;
    private TextEditor? _textEditor;
    private CompositeDisposable? _vmSubscriptions;
    private bool _suppressCaretUpdate;

    public TextFileView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        DetachedFromLogicalTree += OnDetachedFromLogicalTree;
    }

    private void OnDetachedFromLogicalTree(object? sender, Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        _textMateInstallation?.Dispose();
        _textMateInstallation = null;

        if (_textEditor is not null)
        {
            _textEditor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        }

        _vmSubscriptions?.Dispose();
        _vmSubscriptions = null;
        _textEditor = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_textEditor is not null && DataContext is TextDocumentViewModel vm)
        {
            BindViewModel(vm);
        }
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        _textEditor = this.FindControl<TextEditor>("TextEditor");
        if (_textEditor is null)
        {
            return;
        }

        RegistryOptions registryOptions = new(ThemeName.DarkPlus);
        _textMateInstallation = _textEditor.InstallTextMate(registryOptions);

        _textEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;

        if (DataContext is TextDocumentViewModel vm)
        {
            BindViewModel(vm);
            ApplyGrammar(vm, registryOptions);
        }
    }

    private void BindViewModel(TextDocumentViewModel vm)
    {
        if (_textEditor is null)
        {
            return;
        }

        _vmSubscriptions?.Dispose();
        _vmSubscriptions = new CompositeDisposable();

        _textEditor.Document = vm.Document;
        _textEditor.FontSize = vm.FontSize;
        _textEditor.ShowLineNumbers = vm.ShowLineNumbers;
        _textEditor.WordWrap = vm.WordWrap;

        IDisposable fontSizeSubscription = vm.WhenAnyValue(x => x.FontSize)
            .Subscribe(size => _textEditor.FontSize = size);
        _vmSubscriptions.Add(fontSizeSubscription);

        IDisposable lineNumbersSubscription = vm.WhenAnyValue(x => x.ShowLineNumbers)
            .Subscribe(show => _textEditor.ShowLineNumbers = show);
        _vmSubscriptions.Add(lineNumbersSubscription);

        IDisposable wordWrapSubscription = vm.WhenAnyValue(x => x.WordWrap)
            .Subscribe(wrap => _textEditor.WordWrap = wrap);
        _vmSubscriptions.Add(wordWrapSubscription);

        IDisposable caretSubscription = vm.WhenAnyValue(x => x.CaretOffset)
            .Subscribe(offset =>
            {
                _suppressCaretUpdate = true;
                _textEditor.TextArea.Caret.Offset = Math.Clamp(offset, 0, _textEditor.Document.TextLength);
                _textEditor.TextArea.Caret.BringCaretToView();
                _suppressCaretUpdate = false;
            });
        _vmSubscriptions.Add(caretSubscription);
    }

    private void ApplyGrammar(TextDocumentViewModel vm, RegistryOptions registryOptions)
    {
        if (_textMateInstallation is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(vm.LanguageId))
        {
            string? scope = registryOptions.GetScopeByLanguageId(vm.LanguageId);
            if (!string.IsNullOrWhiteSpace(scope))
            {
                _textMateInstallation.SetGrammar(scope);
            }
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_suppressCaretUpdate)
        {
            return;
        }

        if (_textEditor?.TextArea.Caret is not Caret caret)
        {
            return;
        }

        if (DataContext is TextDocumentViewModel vm)
        {
            vm.CaretOffset = caret.Offset;
            vm.CurrentLine = caret.Line;
            vm.CurrentColumn = caret.Column;
        }
    }
}
