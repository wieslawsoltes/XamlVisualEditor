using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using System.Reactive.Disposables;
using Serilog;
using System.Reactive.Linq;
using TextMateSharp.Grammars;
using XamlVisualEditor.CodeEditor;
using XmlFoldingStrategy = XamlVisualEditor.CodeEditor.XmlFoldingStrategy;
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
    private EventHandler? _caretPositionChangedHandler;
    private CompositeDisposable? _vmSubscriptions;
    private bool _textMateInstalled;
    private bool _suppressCaretUpdate;
    private FoldingManager? _foldingManager;
    private readonly XmlFoldingStrategy _foldingStrategy = new();
    private IDisposable? _foldingSubscription;

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
            if (_caretPositionChangedHandler is not null)
            {
                _textEditor.TextArea.Caret.PositionChanged -= _caretPositionChangedHandler;
            }
        }

        _foldingSubscription?.Dispose();
        _foldingSubscription = null;
        _foldingManager = null;

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
        _caretPositionChangedHandler = (_, _) =>
        {
            if (_suppressCaretUpdate)
            {
                return;
            }

            TextEditor? editor = _textEditor;
            TextArea? textArea = editor?.TextArea;
            Caret? caret = textArea?.Caret;
            if (caret is null)
            {
                return;
            }

            if (DataContext is CodeEditorViewModel vm)
            {
                vm.CaretOffset = caret.Offset;
                vm.CurrentLine = caret.Line;
                vm.CurrentColumn = caret.Column;
            }
        };
        _textEditor.TextArea.Caret.PositionChanged += _caretPositionChangedHandler;

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

        if (_textEditor.TextArea.LeftMargins.FirstOrDefault(m => m is FoldingMargin) is null)
        {
            _textEditor.TextArea.LeftMargins.Insert(0, new FoldingMargin());
        }

        _foldingManager ??= FoldingManager.Install(_textEditor.TextArea);

        _foldingSubscription?.Dispose();
        _foldingSubscription = Observable.FromEventPattern<EventHandler, EventArgs>(
                h => vm.Document.TextChanged += h,
                h => vm.Document.TextChanged -= h)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateFoldings());
        _vmSubscriptions.Add(_foldingSubscription);
        UpdateFoldings();

        // Sync editor properties from the ViewModel
        _textEditor.FontSize = vm.FontSize;
        _textEditor.ShowLineNumbers = vm.ShowLineNumbers;
        _textEditor.WordWrap = vm.WordWrap;

        // Subscribe to ViewModel property changes
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
                try
                {
                    _textEditor.TextArea.Caret.BringCaretToView();
                }
                catch (ArgumentException)
                {
                }
                _suppressCaretUpdate = false;
            });
        _vmSubscriptions.Add(caretSubscription);

        // Add diagnostic colorizer for error squiggles
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

    private void UpdateFoldings()
    {
        if (_textEditor?.Document is null || _foldingManager is null)
        {
            return;
        }

        TextDocument document = _textEditor.Document;
        int length = document.TextLength;
        if (length == 0)
        {
            try
            {
                _foldingManager.UpdateFoldings(Array.Empty<NewFolding>(), -1);
            }
            catch (ArgumentException)
            {
            }
            return;
        }
        IEnumerable<NewFolding> foldings = _foldingStrategy.CreateNewFoldings(document)
            .Select(folding =>
            {
                int start = Math.Clamp(folding.StartOffset, 0, length);
                int end = Math.Clamp(folding.EndOffset, 0, length);
                return new NewFolding(start, end) { Name = folding.Name };
            })
            .Where(folding => folding.EndOffset > folding.StartOffset)
            .OrderBy(folding => folding.StartOffset)
            .ToList();

        int currentLength = document.TextLength;
        List<NewFolding> safeFoldings = foldings
            .Where(folding => folding.StartOffset >= 0 && folding.EndOffset <= currentLength)
            .ToList();

        try
        {
            _foldingManager.UpdateFoldings(safeFoldings, -1);
        }
        catch (ArgumentException)
        {
        }
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
            ShowCompletionWindow(vm, CompletionTrigger.CharacterTyped, trigger);
        }

        // Show insight window for markup extensions after '{'
        if (trigger == '{')
        {
            ShowInsightWindow(vm);
        }
    }

    private void ShowCompletionWindow(CodeEditorViewModel vm, CompletionTrigger trigger, char? triggerCharacter)
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
            DocumentText = text,
            Offset = offset,
            Trigger = trigger,
            TriggerCharacter = triggerCharacter,
            LanguageId = "xml"
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
            Log.Logger.Warning("Editor position lookup failed: {Message}", ex.Message);
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
