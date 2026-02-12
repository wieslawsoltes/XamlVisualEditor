using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Avalonia.VisualTree;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Views;

public sealed class BreakpointMarginBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<BreakpointMarginBehavior, Control, bool>("IsEnabled");

    public static readonly AttachedProperty<BreakpointsViewModel?> BreakpointsProperty =
        AvaloniaProperty.RegisterAttached<BreakpointMarginBehavior, Control, BreakpointsViewModel?>("Breakpoints");

    public static readonly AttachedProperty<string?> FilePathProperty =
        AvaloniaProperty.RegisterAttached<BreakpointMarginBehavior, Control, string?>("FilePath");

    public static readonly AttachedProperty<int?> ExecutionLineProperty =
        AvaloniaProperty.RegisterAttached<BreakpointMarginBehavior, Control, int?>("ExecutionLine");

    private static readonly AttachedProperty<BreakpointMargin?> MarginProperty =
        AvaloniaProperty.RegisterAttached<BreakpointMarginBehavior, Control, BreakpointMargin?>("Margin");

    private static readonly AttachedProperty<bool> PendingAttachProperty =
        AvaloniaProperty.RegisterAttached<BreakpointMarginBehavior, Control, bool>("PendingAttach");

    static BreakpointMarginBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnPropertyChanged);
        BreakpointsProperty.Changed.AddClassHandler<Control>(OnPropertyChanged);
        FilePathProperty.Changed.AddClassHandler<Control>(OnPropertyChanged);
        ExecutionLineProperty.Changed.AddClassHandler<Control>(OnPropertyChanged);
    }

    public static bool GetIsEnabled(Control control)
    {
        return control.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(Control control, bool value)
    {
        control.SetValue(IsEnabledProperty, value);
    }

    public static BreakpointsViewModel? GetBreakpoints(Control control)
    {
        return control.GetValue(BreakpointsProperty);
    }

    public static void SetBreakpoints(Control control, BreakpointsViewModel? value)
    {
        control.SetValue(BreakpointsProperty, value);
    }

    public static string? GetFilePath(Control control)
    {
        return control.GetValue(FilePathProperty);
    }

    public static void SetFilePath(Control control, string? value)
    {
        control.SetValue(FilePathProperty, value);
    }

    public static int? GetExecutionLine(Control control)
    {
        return control.GetValue(ExecutionLineProperty);
    }

    public static void SetExecutionLine(Control control, int? value)
    {
        control.SetValue(ExecutionLineProperty, value);
    }

    private static void OnPropertyChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        UpdateMargin(control);
    }

    private static void UpdateMargin(Control control)
    {
        TextEditor? editor = ResolveEditor(control);
        if (editor is null)
        {
            if (!control.GetValue(PendingAttachProperty))
            {
                control.AttachedToVisualTree += OnAttachedToVisualTree;
                control.SetValue(PendingAttachProperty, true);
            }
            return;
        }

        if (control.GetValue(PendingAttachProperty))
        {
            control.AttachedToVisualTree -= OnAttachedToVisualTree;
            control.SetValue(PendingAttachProperty, false);
        }

        bool enabled = GetIsEnabled(control);
        BreakpointsViewModel? breakpoints = GetBreakpoints(control);
        string? filePath = GetFilePath(control);
        int? executionLine = GetExecutionLine(control);

        if (!enabled || breakpoints is null || string.IsNullOrWhiteSpace(filePath))
        {
            DetachMargin(editor);
            return;
        }

        BreakpointMargin? margin = editor.GetValue(MarginProperty);
        if (margin is null)
        {
            margin = new BreakpointMargin();
            editor.TextArea.LeftMargins.Insert(0, margin);
            editor.SetValue(MarginProperty, margin);
        }

        margin.AttachEditor(editor);

        margin.UpdateSource(breakpoints, filePath, executionLine);
    }

    private static void DetachMargin(TextEditor editor)
    {
        BreakpointMargin? margin = editor.GetValue(MarginProperty);
        if (margin is null)
        {
            return;
        }

        editor.TextArea.LeftMargins.Remove(margin);
        margin.DetachEditor();
        margin.Dispose();
        editor.SetValue(MarginProperty, null);
    }

    private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
        {
            UpdateMargin(control);
        }
    }

    private static TextEditor? ResolveEditor(Control control)
    {
        if (control is TextEditor editor)
        {
            return editor;
        }

        return control.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault();
    }

    private sealed class BreakpointMargin : Control, ITextViewConnect, IDisposable
    {
        private static readonly IBrush EnabledBrush = new SolidColorBrush(Color.Parse("#E51400"));
        private static readonly IBrush DisabledBrush = new SolidColorBrush(Color.Parse("#7A7A7A"));
        private static readonly IBrush UnverifiedBrush = new SolidColorBrush(Color.Parse("#F0A500"));
        private static readonly IBrush ExecutionBrush = new SolidColorBrush(Color.Parse("#00C853"));
        private BreakpointsViewModel? _breakpoints;
        private string? _filePath;
        private int? _executionLine;
        private readonly Dictionary<BreakpointEntryViewModel, INotifyPropertyChanged> _tracked = new();
        private TextView? _textView;
        private TextEditor? _editor;
        private EventHandler<PointerPressedEventArgs>? _textAreaPointerHandler;

        public BreakpointMargin()
        {
            IsHitTestVisible = true;
            Focusable = false;
            Width = 18;
            MinWidth = 18;
        }

        public void UpdateSource(BreakpointsViewModel breakpoints, string filePath, int? executionLine)
        {
            if (ReferenceEquals(_breakpoints, breakpoints)
                && string.Equals(_filePath, filePath, StringComparison.OrdinalIgnoreCase)
                && _executionLine == executionLine)
            {
                return;
            }

            DetachBreakpoints();
            _breakpoints = breakpoints;
            _filePath = filePath;
            _executionLine = executionLine;
            AttachBreakpoints();
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double height = double.IsInfinity(availableSize.Height)
                ? _textView?.Bounds.Height ?? 0
                : availableSize.Height;
            return new Size(18, height);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (_textView is null || _breakpoints is null || string.IsNullOrWhiteSpace(_filePath))
            {
                return;
            }

            if (!_textView.VisualLinesValid)
            {
                return;
            }

            Dictionary<int, BreakpointEntryViewModel> lookup = BuildLineLookup();
            foreach (VisualLine line in _textView.VisualLines)
            {
                int lineNumber = line.FirstDocumentLine.LineNumber;
                if (!lookup.TryGetValue(lineNumber, out BreakpointEntryViewModel? entry))
                {
                    continue;
                }

                double y = line.VisualTop - _textView.ScrollOffset.Y + (line.Height / 2);
                double x = 9;
                double radius = 5;

                IBrush brush = entry.IsEnabled
                    ? (entry.IsVerified ? EnabledBrush : UnverifiedBrush)
                    : DisabledBrush;

                context.DrawEllipse(brush, null, new Point(x, y), radius, radius);

                if (_executionLine == lineNumber)
                {
                    Point p1 = new(2, y);
                    Point p2 = new(10, y - 6);
                    Point p3 = new(10, y + 6);
                    context.DrawGeometry(ExecutionBrush, null, CreateTriangleGeometry(p1, p2, p3));
                }
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (_editor is null)
            {
                return;
            }
            if (_textView is null || _breakpoints is null || string.IsNullOrWhiteSpace(_filePath))
            {
                return;
            }
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            Point position = e.GetPosition(this);
            VisualLine? line = _textView.GetVisualLineFromVisualTop(position.Y + _textView.ScrollOffset.Y);
            if (line is null)
            {
                return;
            }

            int lineNumber = line.FirstDocumentLine.LineNumber;
            _breakpoints.ToggleBreakpoint(_filePath, lineNumber);
            e.Handled = true;
        }

        public void AttachEditor(TextEditor editor)
        {
            if (ReferenceEquals(_editor, editor))
            {
                return;
            }

            DetachEditor();
            _editor = editor;
            _textAreaPointerHandler = OnTextAreaPointerPressed;
            _editor.TextArea.AddHandler(
                InputElement.PointerPressedEvent,
                _textAreaPointerHandler,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
        }

        public void DetachEditor()
        {
            if (_editor is not null && _textAreaPointerHandler is not null)
            {
                _editor.TextArea.RemoveHandler(InputElement.PointerPressedEvent, _textAreaPointerHandler);
            }

            _editor = null;
            _textAreaPointerHandler = null;
        }

        private void OnTextAreaPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_textView is null || _breakpoints is null || _editor is null || string.IsNullOrWhiteSpace(_filePath))
            {
                return;
            }
            if (e.Handled && ReferenceEquals(e.Source, this))
            {
                return;
            }
            if (!e.GetCurrentPoint(_editor.TextArea).Properties.IsLeftButtonPressed)
            {
                return;
            }

            Point position = e.GetPosition(_editor.TextArea);
            double gutterWidth = GetGutterWidth(_editor.TextArea.LeftMargins);
            if (position.X > gutterWidth)
            {
                return;
            }

            Point viewPosition = e.GetPosition(_textView);
            VisualLine? line = _textView.GetVisualLineFromVisualTop(viewPosition.Y + _textView.ScrollOffset.Y);
            if (line is null)
            {
                return;
            }

            int lineNumber = line.FirstDocumentLine.LineNumber;
            _breakpoints.ToggleBreakpoint(_filePath, lineNumber);
            e.Handled = true;
        }

        private static double GetGutterWidth(System.Collections.IEnumerable margins)
        {
            double width = 0;
            foreach (object? margin in margins)
            {
                if (margin is not Control control)
                {
                    continue;
                }

                if (control.Bounds.Width > 0)
                {
                    width += control.Bounds.Width;
                }
                else if (control.DesiredSize.Width > 0)
                {
                    width += control.DesiredSize.Width;
                }
            }

            return width;
        }

        private Dictionary<int, BreakpointEntryViewModel> BuildLineLookup()
        {
            Dictionary<int, BreakpointEntryViewModel> lookup = new();
            if (_breakpoints is null || string.IsNullOrWhiteSpace(_filePath))
            {
                return lookup;
            }

            foreach (BreakpointEntryViewModel entry in _breakpoints.Items)
            {
                if (!string.Equals(entry.FilePath, _filePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                lookup[entry.Line] = entry;
            }

            return lookup;
        }

        private void AttachBreakpoints()
        {
            if (_breakpoints is null)
            {
                return;
            }

            _breakpoints.Items.CollectionChanged += OnCollectionChanged;
            foreach (BreakpointEntryViewModel entry in _breakpoints.Items)
            {
                Subscribe(entry);
            }
        }

        private void DetachBreakpoints()
        {
            if (_breakpoints is null)
            {
                return;
            }

            _breakpoints.Items.CollectionChanged -= OnCollectionChanged;
            foreach (BreakpointEntryViewModel entry in _breakpoints.Items.ToList())
            {
                Unsubscribe(entry);
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
            {
                foreach (BreakpointEntryViewModel entry in e.OldItems)
                {
                    Unsubscribe(entry);
                }
            }

            if (e.NewItems is not null)
            {
                foreach (BreakpointEntryViewModel entry in e.NewItems)
                {
                    Subscribe(entry);
                }
            }

            InvalidateVisual();
        }

        private void Subscribe(BreakpointEntryViewModel entry)
        {
            if (entry is not INotifyPropertyChanged notifying)
            {
                return;
            }

            notifying.PropertyChanged += OnEntryPropertyChanged;
            _tracked[entry] = notifying;
        }

        private void Unsubscribe(BreakpointEntryViewModel entry)
        {
            if (_tracked.Remove(entry, out INotifyPropertyChanged? notifying))
            {
                notifying.PropertyChanged -= OnEntryPropertyChanged;
            }
        }

        private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            InvalidateVisual();
        }

        public void Dispose()
        {
            DetachBreakpoints();
            _tracked.Clear();
            DetachEditor();
        }

        public void AddToTextView(TextView textView)
        {
            _textView = textView;
            _textView.VisualLinesChanged += OnTextViewVisualChanged;
            _textView.ScrollOffsetChanged += OnTextViewVisualChanged;
            InvalidateVisual();
        }

        public void RemoveFromTextView(TextView textView)
        {
            textView.VisualLinesChanged -= OnTextViewVisualChanged;
            textView.ScrollOffsetChanged -= OnTextViewVisualChanged;
            if (ReferenceEquals(_textView, textView))
            {
                _textView = null;
            }
        }

        private void OnTextViewVisualChanged(object? sender, EventArgs e)
        {
            InvalidateVisual();
        }

        private static StreamGeometry CreateTriangleGeometry(Point p1, Point p2, Point p3)
        {
            StreamGeometry geometry = new();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(p1, true);
                ctx.LineTo(p2);
                ctx.LineTo(p3);
                ctx.EndFigure(true);
            }

            return geometry;
        }
    }
}
