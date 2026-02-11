using System;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;
using Serilog;
using ReactiveUI;
using TextMateSharp.Grammars;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Shell.ViewModels;
using System.Text.RegularExpressions;
using XamlVisualEditor.CodeEditor;

namespace XamlVisualEditor.App.Views;

/// <summary>
/// Code-behind for the generic text file view with TextMate syntax highlighting.
/// </summary>
public sealed partial class TextFileView : UserControl
{
    private TextMate.Installation? _textMateInstallation;
    private CompletionWindow? _completionWindow;
    private OverloadInsightWindow? _insightWindow;
    private TextEditor? _textEditor;
    private CompositeDisposable? _vmSubscriptions;
    private bool _suppressCaretUpdate;
    private CancellationTokenSource? _hoverCts;
    private readonly HoverRangeColorizer _hoverRangeColorizer = new();

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

        _hoverCts?.Cancel();
        _hoverCts?.Dispose();
        _hoverCts = null;

        _completionWindow?.Close();
        _completionWindow = null;
        _insightWindow?.Close();
        _insightWindow = null;

        if (_textEditor is not null)
        {
            _textEditor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
            _textEditor.TextArea.TextEntered -= OnTextEntered;
            _textEditor.TextArea.TextEntering -= OnTextEntering;
            _textEditor.TextArea.TextView.PointerMoved -= OnPointerMoved;
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
        _textEditor.TextArea.TextEntered += OnTextEntered;
        _textEditor.TextArea.TextEntering += OnTextEntering;
        _textEditor.TextArea.TextView.PointerMoved += OnPointerMoved;

        if (!_textEditor.TextArea.TextView.LineTransformers.Contains(_hoverRangeColorizer))
        {
            _textEditor.TextArea.TextView.LineTransformers.Add(_hoverRangeColorizer);
        }

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

        if (!_textEditor.TextArea.TextView.LineTransformers.Contains(vm.SemanticTokenColorizer))
        {
            _textEditor.TextArea.TextView.LineTransformers.Add(vm.SemanticTokenColorizer);
        }

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

        if (!_textEditor.TextArea.TextView.LineTransformers.Contains(vm.DiagnosticColorizer))
        {
            _textEditor.TextArea.TextView.LineTransformers.Add(vm.DiagnosticColorizer);
        }

        if (!_textEditor.TextArea.TextView.LineTransformers.Contains(vm.ExecutionLineColorizer))
        {
            _textEditor.TextArea.TextView.LineTransformers.Add(vm.ExecutionLineColorizer);
        }

        if (!_textEditor.TextArea.TextView.LineTransformers.Contains(vm.BreakpointLineColorizer))
        {
            _textEditor.TextArea.TextView.LineTransformers.Add(vm.BreakpointLineColorizer);
        }

        IDisposable executionLineSubscription = vm.WhenAnyValue(x => x.ExecutionLine)
            .Subscribe(line =>
            {
                vm.ExecutionLineColorizer.LineNumber = line;
                _textEditor.TextArea.TextView.InvalidateVisual();
            });
        _vmSubscriptions.Add(executionLineSubscription);

        IDisposable breakpointHighlightSubscription = vm.WhenAnyValue(x => x.BreakpointHighlightVersion)
            .Subscribe(_ => _textEditor.TextArea.TextView.InvalidateVisual());
        _vmSubscriptions.Add(breakpointHighlightSubscription);

        IDisposable semanticTokenSubscription = vm.WhenAnyValue(x => x.SemanticTokenVersion)
            .Subscribe(_ => _textEditor.TextArea.TextView.InvalidateVisual());
        _vmSubscriptions.Add(semanticTokenSubscription);
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

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (_completionWindow is not null && e.Text is { Length: > 0 })
        {
            if (TryHandleCommitCharacter(e, e.Text[0]))
            {
                return;
            }

            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '.' && e.Text[0] != '_' && e.Text[0] != ':')
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    private async void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (DataContext is not TextDocumentViewModel vm || _textEditor is null)
        {
            return;
        }

        char trigger = e.Text is { Length: > 0 } ? e.Text[0] : '\0';
        if (trigger is '.' or '(' or '<' or ':' or '"' or '=')
        {
            await ShowCompletionWindowAsync(vm, CompletionTrigger.CharacterTyped, trigger);
        }

        if (trigger == '(')
        {
            await ShowSignatureHelpAsync(vm);
        }
    }

    private async Task ShowCompletionWindowAsync(
        TextDocumentViewModel vm,
        CompletionTrigger trigger,
        char triggerCharacter)
    {
        if (_textEditor is null)
        {
            return;
        }

        _completionWindow?.Close();
        _completionWindow = null;

        string text = _textEditor.Document.Text;
        int offset = _textEditor.CaretOffset;
        if (offset < 0 || offset > text.Length)
        {
            return;
        }

        CompletionContext context = new()
        {
            TextBefore = text[..offset],
            DocumentText = text,
            FilePath = vm.FilePath,
            LanguageId = vm.LanguageId,
            Offset = offset,
            Trigger = trigger,
            TriggerCharacter = triggerCharacter
        };

        IReadOnlyList<CompletionItem> items = await vm.GetCompletionsAsync(context);
        if (items.Count == 0)
        {
            return;
        }

        _completionWindow = new CompletionWindow(_textEditor.TextArea);
        IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;
        foreach (CompletionItem item in items)
        {
            data.Add(new TextFileCompletionData(item));
        }

        _completionWindow.Show();
        _completionWindow.Closed += (_, _) => _completionWindow = null;
    }

    private async Task ShowSignatureHelpAsync(TextDocumentViewModel vm)
    {
        if (_textEditor is null)
        {
            return;
        }

        _insightWindow?.Close();
        _insightWindow = null;

        LanguageSignatureHelp? help = await vm.GetSignatureHelpAsync(_textEditor.CaretOffset);
        if (help is null || help.Signatures.Count == 0)
        {
            return;
        }

        _insightWindow = new OverloadInsightWindow(_textEditor.TextArea)
        {
            Provider = new LanguageSignatureHelpProvider(help)
        };
        _insightWindow.Show();
        _insightWindow.Closed += (_, _) => _insightWindow = null;
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_textEditor is null || DataContext is not TextDocumentViewModel vm)
        {
            return;
        }

        int? offset = GetOffsetFromPoint(_textEditor, e.GetPosition(_textEditor.TextArea.TextView));
        if (offset is null || offset < 0 || offset >= _textEditor.Document.TextLength)
        {
            ToolTip.SetTip(_textEditor, null);
            ClearHoverHighlight();
            return;
        }

        DocumentLine line = _textEditor.Document.GetLineByOffset(offset.Value);
        int col = offset.Value - line.Offset + 1;

        LanguageDiagnostic? diag = vm.DiagnosticColorizer.GetDiagnosticAt(line.LineNumber, col);
        if (diag is not null)
        {
            ToolTip.SetTip(_textEditor, $"[{diag.Severity}] {diag.Message}");
            ClearHoverHighlight();
            return;
        }

        _hoverCts?.Cancel();
        _hoverCts?.Dispose();
        _hoverCts = new CancellationTokenSource();
        CancellationToken token = _hoverCts.Token;

        LanguageHover? hover = await vm.GetHoverAsync(offset.Value, token);
        if (token.IsCancellationRequested)
        {
            return;
        }

        string? formatted = FormatHoverText(hover?.Contents);
        ToolTip.SetTip(_textEditor, formatted);
        UpdateHoverHighlight(vm, hover);
    }

    private void UpdateHoverHighlight(TextDocumentViewModel vm, LanguageHover? hover)
    {
        if (_textEditor?.Document is null || hover?.Range is null)
        {
            ClearHoverHighlight();
            return;
        }

        LanguageTextRange range = hover.Range.Value;
        int startOffset = vm.GetOffsetForLineColumn(range.Start.Line, range.Start.Column);
        int endOffset = vm.GetOffsetForLineColumn(range.End.Line, range.End.Column);
        int length = _textEditor.Document.TextLength;
        startOffset = Math.Clamp(startOffset, 0, length);
        endOffset = Math.Clamp(endOffset, 0, length);

        _hoverRangeColorizer.UpdateRange(startOffset, endOffset);
        _textEditor.TextArea.TextView.InvalidateVisual();
    }

    private void ClearHoverHighlight()
    {
        if (_textEditor is null)
        {
            return;
        }

        _hoverRangeColorizer.Clear();
        _textEditor.TextArea.TextView.InvalidateVisual();
    }

    private bool TryHandleCommitCharacter(TextInputEventArgs e, char character)
    {
        if (_completionWindow?.CompletionList.SelectedItem is not TextFileCompletionData data)
        {
            return false;
        }

        if (data.CommitCharacters.Count == 0 || !data.CommitCharacters.Contains(character))
        {
            return false;
        }

        _completionWindow.CompletionList.RequestInsertion(e);

        return true;
    }

    private static string? FormatHoverText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string formatted = text;

        // Strip common markdown markers.
        formatted = formatted.Replace("```", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

        // Convert markdown links to plain text.
        formatted = Regex.Replace(formatted, "\\[([^\\]]+)\\]\\([^\\)]+\\)", "$1");

        return formatted.Trim();
    }

    private static int? GetOffsetFromPoint(TextEditor editor, Avalonia.Point point)
    {
        try
        {
            AvaloniaEdit.Rendering.TextView textView = editor.TextArea.TextView;
            Avalonia.Point tvPos = point;
            AvaloniaEdit.Rendering.VisualLine? visualLine = textView.GetVisualLineFromVisualTop(
                tvPos.Y + textView.ScrollOffset.Y);
            if (visualLine is null)
            {
                return null;
            }

            int vc = visualLine.GetVisualColumn(tvPos);
            return visualLine.GetRelativeOffset(vc) + visualLine.FirstDocumentLine.Offset;
        }
        catch (Exception ex)
        {
                Log.Logger.Warning("Editor position lookup failed: {Message}", ex.Message);
            return null;
        }
    }
}

internal sealed class TextFileCompletionData : ICompletionData
    , ICommitCharactersProvider
{
    private readonly CompletionItem _item;

    public TextFileCompletionData(CompletionItem item)
    {
        _item = item;
    }

    public string Text => _item.InsertText ?? _item.DisplayText;

    public object Content => _item.DisplayText;

    public object? Description => _item.Description;

    public double Priority => _item.Priority;

    public IReadOnlyList<char> CommitCharacters => _item.CommitCharacters ?? Array.Empty<char>();

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        if (_item.TextEdit is not null)
        {
            int offset = Math.Clamp(_item.TextEdit.Offset, 0, textArea.Document.TextLength);
            int length = Math.Clamp(_item.TextEdit.Length, 0, textArea.Document.TextLength - offset);
            textArea.Document.Replace(offset, length, _item.TextEdit.NewText);
            return;
        }

        textArea.Document.Replace(completionSegment, Text);
    }

    public Avalonia.Media.IImage? Image => null;
}

internal sealed class LanguageSignatureHelpProvider : IOverloadProvider
{
    private readonly LanguageSignatureHelp _help;
    private int _selectedIndex;

    public LanguageSignatureHelpProvider(LanguageSignatureHelp help)
    {
        _help = help;
        _selectedIndex = Math.Clamp(help.ActiveSignature, 0, help.Signatures.Count - 1);
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            _selectedIndex = value;
            OnPropertyChanged(nameof(SelectedIndex));
            OnPropertyChanged(nameof(CurrentHeader));
            OnPropertyChanged(nameof(CurrentContent));
            OnPropertyChanged(nameof(CurrentIndexText));
        }
    }

    public int Count => _help.Signatures.Count;

    public string CurrentIndexText => $"{SelectedIndex + 1} of {Count}";

    public object CurrentHeader => _help.Signatures[SelectedIndex].Label;

    public object CurrentContent => _help.Signatures[SelectedIndex].Documentation ?? string.Empty;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}
