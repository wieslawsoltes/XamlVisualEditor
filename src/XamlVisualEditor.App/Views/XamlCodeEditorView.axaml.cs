using System;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using System.Reactive.Disposables;
using TextMateSharp.Grammars;
using XamlVisualEditor.CodeEditor;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Designer.DragDrop;
using XamlVisualEditor.Xaml.Intellisense;

namespace XamlVisualEditor.App.Views;

/// <summary>
/// Code-behind for the XAML code editor view.
/// Sets up TextMate syntax highlighting for XML/XAML and wires CompletionWindow / InsightWindow.
/// The Document is assigned programmatically rather than via compiled binding
/// to ensure AvaloniaEdit's TextEditor properly initializes with the shared TextDocument.
/// </summary>
public sealed partial class XamlCodeEditorView : UserControl
{
    private TextMate.Installation? _textMateInstallation;
    private CompletionWindow? _completionWindow;
    private OverloadInsightWindow? _insightWindow;
    private TextEditor? _textEditor;
    private CompositeDisposable? _vmSubscriptions;
    private bool _textMateInstalled;
    private bool _suppressCaretUpdate;

    public XamlCodeEditorView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        DetachedFromLogicalTree += OnDetachedFromLogicalTree;
    }

    private void OnDetachedFromLogicalTree(object? sender, Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        // Dispose TextMate native resources
        _textMateInstallation?.Dispose();
        _textMateInstallation = null;
        _textMateInstalled = false;

        _completionWindow?.Close();
        _completionWindow = null;
        _insightWindow?.Close();
        _insightWindow = null;

        // Unsubscribe event handlers
        if (_textEditor is not null)
        {
            _textEditor.TextArea.TextEntered -= OnTextEntered;
            _textEditor.TextArea.TextEntering -= OnTextEntering;
        }

        _vmSubscriptions?.Dispose();
        _vmSubscriptions = null;

        _textEditor = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // When the DataContext changes, re-bind the document
        if (_textEditor is not null && DataContext is CodeEditorViewModel vm)
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

        // Install TextMate with XML grammar for XAML highlighting (once)
        if (!_textMateInstalled)
        {
            RegistryOptions registryOptions = new(ThemeName.DarkPlus);
            _textMateInstallation = _textEditor.InstallTextMate(registryOptions);
            _textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId("xml"));
            _textMateInstalled = true;
        }

        // Wire up caret position tracking
        _textEditor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            if (_suppressCaretUpdate)
            {
                return;
            }

            if (DataContext is CodeEditorViewModel vm)
            {
                vm.CaretOffset = _textEditor.TextArea.Caret.Offset;
                vm.CurrentLine = _textEditor.TextArea.Caret.Line;
                vm.CurrentColumn = _textEditor.TextArea.Caret.Column;
            }
        };

        // Wire text input for auto-completion triggers
        _textEditor.TextArea.TextEntered += OnTextEntered;
        _textEditor.TextArea.TextEntering += OnTextEntering;

        _textEditor.AddHandler(DragDrop.DragOverEvent,
            OnDragOver,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
            true);
        _textEditor.AddHandler(DragDrop.DropEvent,
            OnDrop,
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
            true);

        // Bind the ViewModel if already set
        if (DataContext is CodeEditorViewModel codeVm)
        {
            BindViewModel(codeVm);
        }
    }

    /// <summary>
    /// Sets the shared TextDocument on the TextEditor and subscribes
    /// to ViewModel property changes for font size, word wrap, etc.
    /// </summary>
    private void BindViewModel(CodeEditorViewModel vm)
    {
        if (_textEditor is null)
        {
            return;
        }

        _vmSubscriptions?.Dispose();
        _vmSubscriptions = new CompositeDisposable();

        // Set the shared TextDocument directly — this is the critical line
        _textEditor.Document = vm.Document;

        // Sync editor properties from the ViewModel
        _textEditor.FontSize = vm.FontSize;
        _textEditor.ShowLineNumbers = vm.ShowLineNumbers;
        _textEditor.WordWrap = vm.WordWrap;

        // Subscribe to ViewModel property changes
        vm.WhenAnyValue(x => x.FontSize)
            .Subscribe(size => _textEditor.FontSize = size)
            .DisposeWith(_vmSubscriptions);

        vm.WhenAnyValue(x => x.ShowLineNumbers)
            .Subscribe(show => _textEditor.ShowLineNumbers = show)
            .DisposeWith(_vmSubscriptions);

        vm.WhenAnyValue(x => x.WordWrap)
            .Subscribe(wrap => _textEditor.WordWrap = wrap)
            .DisposeWith(_vmSubscriptions);

        vm.WhenAnyValue(x => x.CaretOffset)
            .Subscribe(offset =>
            {
                _suppressCaretUpdate = true;
                _textEditor.TextArea.Caret.Offset = Math.Clamp(offset, 0, _textEditor.Document.TextLength);
                _textEditor.TextArea.Caret.BringCaretToView();
                _suppressCaretUpdate = false;
            })
            .DisposeWith(_vmSubscriptions);

        // Add diagnostic colorizer for error squiggles
        if (!_textEditor.TextArea.TextView.LineTransformers.Contains(vm.DiagnosticColorizer))
        {
            _textEditor.TextArea.TextView.LineTransformers.Add(vm.DiagnosticColorizer);
        }

        // Wire tooltip for diagnostics on hover
        _textEditor.TextArea.TextView.PointerMoved += (_, args) =>
        {
            Avalonia.Point position = args.GetPosition(_textEditor.TextArea.TextView);

            // Get the text position from the mouse
            int? offset = GetOffsetFromPoint(_textEditor, position);
            if (offset is null || offset < 0 || _textEditor.Document is null || offset >= _textEditor.Document.TextLength)
            {
                ToolTip.SetTip(_textEditor, null);
                return;
            }

            DocumentLine line = _textEditor.Document.GetLineByOffset(offset.Value);
            int col = offset.Value - line.Offset + 1;

            XamlDiagnostic? diag = vm.DiagnosticColorizer.GetDiagnosticAt(line.LineNumber, col);
            if (diag is not null)
            {
                string severity = diag.Severity.ToString();
                ToolTip.SetTip(_textEditor, $"[{severity}] {diag.Message}");
            }
            else
            {
                ToolTip.SetTip(_textEditor, null);
            }
        };
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        // When the completion window is open and the user types something,
        // let the window handle insertion (e.g., completing with Enter/Tab)
        if (_completionWindow is not null && e.Text is { Length: > 0 })
        {
            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '.' && e.Text[0] != ':' && e.Text[0] != '_')
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        IDataTransfer data = e.DataTransfer;
        if (!data.Contains(DesignerDataFormats.ToolboxItem))
        {
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (_textEditor is null || _textEditor.Document is null)
        {
            return;
        }

        IDataTransfer data = e.DataTransfer;
        if (!data.Contains(DesignerDataFormats.ToolboxItem))
        {
            return;
        }

        string? typeName = data.TryGetValue(DesignerDataFormats.ToolboxItem);
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return;
        }

        int offset = _textEditor.TextArea.Caret.Offset;
        int insertedLength = InsertToolboxSnippet(_textEditor.Document, offset, typeName.Trim());
        _textEditor.TextArea.Caret.Offset = Math.Clamp(offset + insertedLength, 0, _textEditor.Document.TextLength);
        _textEditor.Focus();
        e.Handled = true;
    }

    private static int InsertToolboxSnippet(TextDocument document, int offset, string typeName)
    {
        int clampedOffset = Math.Clamp(offset, 0, document.TextLength);
        DocumentLine line = document.GetLineByOffset(clampedOffset);
        string lineText = document.GetText(line);

        string indent = GetLineIndent(lineText);
        bool needsLeadingNewLine = clampedOffset > line.Offset && clampedOffset < line.EndOffset;
        bool needsTrailingNewLine = clampedOffset < document.TextLength &&
                                   !IsNewLineAt(document, clampedOffset);

        string snippet = BuildSnippet(typeName, indent, needsLeadingNewLine, needsTrailingNewLine);

        document.Insert(clampedOffset, snippet);
        return snippet.Length;
    }

    private static string BuildSnippet(string typeName, string indent, bool leadingNewLine, bool trailingNewLine)
    {
        string newline = Environment.NewLine;
        string snippet = $"{indent}<{typeName} />";

        if (leadingNewLine)
        {
            snippet = newline + snippet;
        }

        if (trailingNewLine)
        {
            snippet += newline;
        }

        return snippet;
    }

    private static string GetLineIndent(string lineText)
    {
        int count = 0;
        while (count < lineText.Length && char.IsWhiteSpace(lineText[count]))
        {
            if (lineText[count] == '\r' || lineText[count] == '\n')
            {
                break;
            }
            count++;
        }

        return count == 0 ? string.Empty : lineText.Substring(0, count);
    }

    private static bool IsNewLineAt(TextDocument document, int offset)
    {
        if (offset < 0 || offset >= document.TextLength)
        {
            return false;
        }

        char current = document.GetCharAt(offset);
        return current == '\n' || current == '\r';
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (DataContext is not CodeEditorViewModel vm || _textEditor is null)
        {
            return;
        }

        // Determine trigger character
        char trigger = e.Text is { Length: > 0 } ? e.Text[0] : '\0';

        // Trigger completion on '<', ' ' (inside tag), '=' or '"' (attribute values), ':'
        if (trigger is '<' or ' ' or '=' or '"' or ':' or '/')
        {
            ShowCompletionWindow(vm, CompletionTrigger.CharacterTyped);
        }

        // Show insight window for markup extensions after '{'
        if (trigger == '{')
        {
            ShowInsightWindow(vm);
        }
    }

    private void ShowCompletionWindow(CodeEditorViewModel vm, CompletionTrigger trigger)
    {
        if (_textEditor is null)
        {
            return;
        }

        // Close existing window
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
            Offset = offset,
            Trigger = trigger
        };

        IReadOnlyList<CompletionItem> items = vm.GetCompletions(context);

        if (items.Count == 0)
        {
            return;
        }

        _completionWindow = new CompletionWindow(_textEditor.TextArea);
        IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;

        foreach (CompletionItem item in items)
        {
            data.Add(new AvaloniaCompletionData(item));
        }

        _completionWindow.Show();
        _completionWindow.Closed += (_, _) => _completionWindow = null;
    }

    private void ShowInsightWindow(CodeEditorViewModel vm)
    {
        if (_textEditor is null)
        {
            return;
        }

        _insightWindow?.Close();
        _insightWindow = null;

        // Provide a basic markup extension hint
        string text = _textEditor.Document.Text;
        int offset = _textEditor.CaretOffset;

        if (offset < 2)
        {
            return;
        }

        _insightWindow = new OverloadInsightWindow(_textEditor.TextArea);
        _insightWindow.Provider = new MarkupExtensionInsightProvider();
        _insightWindow.Show();
        _insightWindow.Closed += (_, _) => _insightWindow = null;
    }

    private static int? GetOffsetFromPoint(TextEditor editor, Avalonia.Point point)
    {
        try
        {
            AvaloniaEdit.Rendering.TextView textView = editor.TextArea.TextView;
            Avalonia.Point tvPos = point;
            AvaloniaEdit.Rendering.VisualLine? visualLine = textView.GetVisualLineFromVisualTop(tvPos.Y + textView.ScrollOffset.Y);
            if (visualLine is null)
            {
                return null;
            }

            int vc = visualLine.GetVisualColumn(tvPos);
            return visualLine.GetRelativeOffset(vc) + visualLine.FirstDocumentLine.Offset;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Editor position lookup failed: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Bridges the internal CompletionItem to AvaloniaEdit's ICompletionData.
/// </summary>
internal sealed class AvaloniaCompletionData : ICompletionData
{
    private readonly CompletionItem _item;

    public AvaloniaCompletionData(CompletionItem item)
    {
        _item = item;
    }

    public string Text => _item.InsertText ?? _item.DisplayText;

    public object Content => _item.DisplayText;

    public object? Description => _item.Description;

    public double Priority => _item.Priority;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }

    // AvaloniaEdit ICompletionData requires an Image property (can be null)
    public Avalonia.Media.IImage? Image => null;
}

/// <summary>
/// Basic insight provider for markup extension parameter hints.
/// </summary>
internal sealed class MarkupExtensionInsightProvider : IOverloadProvider
{
    private static readonly string[] s_hints =
    [
        "{Binding Path=, ElementName=, Mode=, Converter=}",
        "{StaticResource ResourceKey}",
        "{DynamicResource ResourceKey}",
        "{TemplateBinding Property}",
        "{x:Static Member}",
        "{x:Type TypeName}"
    ];

    private int _selectedIndex;

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

    public int Count => s_hints.Length;

    public string CurrentIndexText => $"{SelectedIndex + 1} of {Count}";

    public object CurrentHeader => s_hints[SelectedIndex];

    public object CurrentContent => "Markup extension parameter hints";

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
