using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using XamlVisualEditor.Terminal;

namespace XamlVisualEditor.Terminal.Avalonia.Controls;

public sealed class TerminalControl : Control, ILogicalScrollable
{
    private static readonly TimeSpan CaretBlinkInterval = TimeSpan.FromMilliseconds(600);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<TerminalControl, FontFamily>(
            nameof(FontFamily),
            new FontFamily("Cascadia Mono"));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<TerminalControl, double>(
            nameof(FontSize),
            13);

    public static readonly DirectProperty<TerminalControl, ITerminalViewModel?> TerminalViewModelProperty =
        AvaloniaProperty.RegisterDirect<TerminalControl, ITerminalViewModel?>(
            nameof(TerminalViewModel),
            o => o.TerminalViewModel,
            (o, v) => o.TerminalViewModel = v);

    private ITerminalViewModel? _terminalViewModel;
    private TerminalRenderState? _renderState;
    private CompositionCustomVisual? _compositionVisual;
    private bool _isFocused;
    private double _offsetY;
    private Size _lastLayoutSize;
    private int _lastColumns;
    private int _lastRows;
    private double _lastCellWidth;
    private double _lastCellHeight;
    private double _lastMetricsOffsetY;
    private Size _extent;
    private Size _viewport;
    private Vector _logicalOffset;
    private bool _canHorizontallyScroll;
    private bool _canVerticallyScroll = true;
    private EventHandler? _scrollInvalidated;
    private Size _scrollSize = new(1, 1);
    private Size _pageScrollSize = new(1, 1);

    public ITerminalViewModel? TerminalViewModel
    {
        get => _terminalViewModel;
        set
        {
            if (_terminalViewModel == value)
            {
                return;
            }

            if (_terminalViewModel is not null)
            {
                _terminalViewModel.FrameInvalidated -= OnFrameInvalidated;
            }

            _terminalViewModel = value;
            if (_terminalViewModel is not null)
            {
                _terminalViewModel.FrameInvalidated += OnFrameInvalidated;
            }

            UpdateRenderState();
            InvalidateScrollable();
            RequestRender();
        }
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public TerminalControl()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    Size IScrollable.Extent => _extent;

    Vector IScrollable.Offset
    {
        get => _logicalOffset;
        set => SetLogicalOffset(value);
    }

    Size IScrollable.Viewport => _viewport;

    bool ILogicalScrollable.CanHorizontallyScroll
    {
        get => _canHorizontallyScroll;
        set
        {
            if (_canHorizontallyScroll == value)
            {
                return;
            }

            _canHorizontallyScroll = value;
            InvalidateScrollable();
        }
    }

    bool ILogicalScrollable.CanVerticallyScroll
    {
        get => _canVerticallyScroll;
        set
        {
            if (_canVerticallyScroll == value)
            {
                return;
            }

            _canVerticallyScroll = value;
            InvalidateScrollable();
        }
    }

    bool ILogicalScrollable.IsLogicalScrollEnabled => true;

    event EventHandler? ILogicalScrollable.ScrollInvalidated
    {
        add => _scrollInvalidated += value;
        remove => _scrollInvalidated -= value;
    }

    Size ILogicalScrollable.ScrollSize => _scrollSize;

    Size ILogicalScrollable.PageScrollSize => _pageScrollSize;

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect)
    {
        return false;
    }

    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from)
    {
        return null;
    }

    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e)
    {
        _scrollInvalidated?.Invoke(this, e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.Transparent, Bounds);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty ||
            change.Property == FontFamilyProperty ||
            change.Property == FontSizeProperty)
        {
            _lastLayoutSize = Bounds.Size;
            bool recreateRenderState = change.Property == FontFamilyProperty || change.Property == FontSizeProperty;
            UpdateRenderState(recreateRenderState);
            RequestRender();
        }
    }

    protected override void OnLoaded(RoutedEventArgs routedEventArgs)
    {
        base.OnLoaded(routedEventArgs);
        _lastLayoutSize = Bounds.Size;
        LayoutUpdated += OnLayoutUpdated;
        EnsureCompositionVisual();
        UpdateRenderState();
        RequestRender();
    }

    protected override void OnUnloaded(RoutedEventArgs routedEventArgs)
    {
        base.OnUnloaded(routedEventArgs);
        LayoutUpdated -= OnLayoutUpdated;
        ReleaseCompositionVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutUpdated -= OnLayoutUpdated;
        ReleaseRenderStates();
        ReleaseCompositionVisual();
        InvalidateScrollable();
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        _isFocused = true;
        RequestRender();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        _isFocused = false;
        RequestRender();
    }

    private void UpdateRenderState(bool recreateRenderState = false)
    {
        if (_terminalViewModel is null)
        {
            _renderState?.Dispose();
            _renderState = null;
            _offsetY = 0;
            _lastColumns = 0;
            _lastRows = 0;
            _lastCellWidth = 0;
            _lastCellHeight = 0;
            _lastMetricsOffsetY = 0;
            return;
        }

        if (_renderState is null || recreateRenderState)
        {
            Typeface typeface = new(FontFamily, FontStyle.Normal, FontWeight.Normal);
            _renderState?.Dispose();
            _renderState = TerminalRenderState.Create(typeface, FontSize);
        }

        int cols = (int)Math.Floor(Bounds.Width / _renderState.CellSize.Width);
        int rows = (int)Math.Floor(Bounds.Height / _renderState.CellSize.Height);
        double usedHeight = rows > 0 ? rows * _renderState.CellSize.Height : 0;
        double remainderOffset = Math.Max(0, Bounds.Height - usedHeight);
        _offsetY = MathF.Round((float)remainderOffset);
        if (cols > 0 && rows > 0)
        {
            if (NeedsMetricsUpdate(_renderState.CellSize.Width, _renderState.CellSize.Height, _offsetY))
            {
                _terminalViewModel.SetMetrics(new TerminalMetrics(_renderState.CellSize.Width, _renderState.CellSize.Height, 0, _offsetY));
                _lastCellWidth = _renderState.CellSize.Width;
                _lastCellHeight = _renderState.CellSize.Height;
                _lastMetricsOffsetY = _offsetY;
            }

            if (cols != _lastColumns || rows != _lastRows)
            {
                _terminalViewModel.Resize(cols, rows);
                _lastColumns = cols;
                _lastRows = rows;
            }
        }
        else
        {
            _lastColumns = 0;
            _lastRows = 0;
        }

        InvalidateScrollable();
    }

    private void ReleaseRenderStates()
    {
        _renderState?.Dispose();
        _renderState = null;
    }

    private void OnFrameInvalidated()
    {
        Dispatcher.UIThread.Post(() =>
        {
            InvalidateScrollable();
            RequestRender();
        });
    }

    private void SetLogicalOffset(Vector value)
    {
        Vector coerced = CoerceOffset(value);
        if (AreClose(_logicalOffset, coerced))
        {
            return;
        }

        _logicalOffset = coerced;
        ApplyLogicalOffsetToViewModel();
        ((ILogicalScrollable)this).RaiseScrollInvalidated(EventArgs.Empty);
        RequestRender();
    }

    private void ApplyLogicalOffsetToViewModel()
    {
        if (_terminalViewModel is null)
        {
            return;
        }

        double maxY = Math.Max(_extent.Height - _viewport.Height, 0);
        int targetOffset = (int)Math.Round(maxY - _logicalOffset.Y);
        _terminalViewModel.SetScrollOffset(targetOffset);
    }

    private void InvalidateScrollable()
    {
        if (_terminalViewModel is null)
        {
            _extent = default;
            _viewport = default;
            _logicalOffset = default;
            _scrollSize = new Size(1, 1);
            _pageScrollSize = new Size(1, 1);
            ((ILogicalScrollable)this).RaiseScrollInvalidated(EventArgs.Empty);
            return;
        }

        int columns = 1;
        int rows = 1;
        int scrollback = 0;

        _terminalViewModel.Emulator.Read((buffer, _) =>
        {
            columns = Math.Max(1, buffer.Columns);
            rows = Math.Max(1, buffer.Rows);
            scrollback = Math.Max(0, buffer.ScrollbackCount);
        });

        _viewport = new Size(columns, rows);
        _extent = new Size(columns, rows + scrollback);
        _scrollSize = new Size(1, 1);
        _pageScrollSize = _viewport;

        double maxY = Math.Max(_extent.Height - _viewport.Height, 0);
        int clampedOffset = Math.Clamp(_terminalViewModel.ScrollOffset, 0, (int)Math.Round(maxY));
        _logicalOffset = CoerceOffset(new Vector(0, maxY - clampedOffset));

        ((ILogicalScrollable)this).RaiseScrollInvalidated(EventArgs.Empty);
    }

    private Vector CoerceOffset(Vector value)
    {
        double maxX = _canHorizontallyScroll ? Math.Max(_extent.Width - _viewport.Width, 0) : 0;
        double maxY = _canVerticallyScroll ? Math.Max(_extent.Height - _viewport.Height, 0) : 0;
        return new Vector(Clamp(value.X, 0, maxX), Clamp(value.Y, 0, maxY));
    }

    private static bool AreClose(Vector left, Vector right)
    {
        return Math.Abs(left.X - right.X) < 0.001 && Math.Abs(left.Y - right.Y) < 0.001;
    }

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) < 0.001;
    }

    private bool NeedsMetricsUpdate(double cellWidth, double cellHeight, double offsetY)
    {
        return !AreClose(_lastCellWidth, cellWidth)
            || !AreClose(_lastCellHeight, cellHeight)
            || !AreClose(_lastMetricsOffsetY, offsetY);
    }

    private static double Clamp(double value, double min, double max)
    {
        return value < min ? min : value > max ? max : value;
    }

    private bool IsCaretVisible()
    {
        if (_terminalViewModel is null)
        {
            return false;
        }

        return _isFocused
            && _terminalViewModel.ScrollOffset == 0
            && _terminalViewModel.Emulator.State.CursorVisible;
    }

    private bool IsCaretBlinkEnabled()
    {
        return _terminalViewModel?.Emulator.State.CursorBlink ?? false;
    }

    private void RequestRender()
    {
        EnsureCompositionVisual();
        UpdateCompositionVisual();
    }

    private void EnsureCompositionVisual()
    {
        if (_compositionVisual is not null)
        {
            return;
        }

        CompositionVisual? elementVisual = ElementComposition.GetElementVisual(this);
        Compositor? compositor = elementVisual?.Compositor;
        if (compositor is null)
        {
            return;
        }

        _compositionVisual = compositor.CreateCustomVisual(new TerminalCompositionCustomVisualHandler(() => _terminalViewModel));
        ElementComposition.SetElementChildVisual(this, _compositionVisual);
        UpdateCompositionVisual();
    }

    private void ReleaseCompositionVisual()
    {
        if (_compositionVisual is null)
        {
            return;
        }

        _compositionVisual.SendHandlerMessage(new TerminalVisualPayload(
            TerminalVisualCommand.Dispose,
            0,
            false,
            false,
            string.Empty,
            0));
        _compositionVisual = null;
    }

    private void UpdateCompositionVisual()
    {
        if (_compositionVisual is null)
        {
            return;
        }

        _compositionVisual.Size = new System.Numerics.Vector2((float)Math.Max(0, Bounds.Width), (float)Math.Max(0, Bounds.Height));
        _compositionVisual.SendHandlerMessage(new TerminalVisualPayload(
            TerminalVisualCommand.Update,
            _offsetY,
            IsCaretVisible(),
            IsCaretBlinkEnabled(),
            FontFamily.Name,
            FontSize));
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_lastLayoutSize == Bounds.Size)
        {
            return;
        }

        _lastLayoutSize = Bounds.Size;
        UpdateRenderState();
        RequestRender();
    }

    private enum TerminalVisualCommand
    {
        Update,
        Dispose
    }

    private readonly record struct TerminalVisualPayload(
        TerminalVisualCommand Command,
        double OffsetY,
        bool CaretVisible,
        bool CaretBlink,
        string FontFamilyName,
        double FontSize);

    private sealed class TerminalCompositionCustomVisualHandler : CompositionCustomVisualHandler
    {
        private readonly Func<ITerminalViewModel?> _getViewModel;
        private TerminalRenderState? _renderState;
        private readonly List<TerminalRenderState> _retiredRenderStates = new();
        private double _offsetY;
        private bool _caretVisible;
        private bool _caretBlink;
        private bool _caretBlinkState = true;
        private TimeSpan? _lastBlinkToggle;
        private bool _running;
        private string _fontFamilyName = string.Empty;
        private double _fontSize;

        public TerminalCompositionCustomVisualHandler(Func<ITerminalViewModel?> getViewModel)
        {
            _getViewModel = getViewModel;
        }

        public override void OnMessage(object message)
        {
            if (message is not TerminalVisualPayload payload)
            {
                return;
            }

            switch (payload.Command)
            {
                case TerminalVisualCommand.Update:
                    bool resetBlinkState = _caretVisible != payload.CaretVisible || _caretBlink != payload.CaretBlink;
                    _offsetY = payload.OffsetY;
                    _caretVisible = payload.CaretVisible;
                    _caretBlink = payload.CaretBlink;
                    UpdateRenderState(payload.FontFamilyName, payload.FontSize);
                    UpdateAnimationState(resetBlinkState);
                    Invalidate();
                    RegisterForNextAnimationFrameUpdate();
                    break;

                case TerminalVisualCommand.Dispose:
                    _running = false;
                    _caretVisible = false;
                    _caretBlink = false;
                    _caretBlinkState = true;
                    _lastBlinkToggle = null;
                    _offsetY = 0;
                    DisposeRenderState();
                    DisposeRetiredRenderStates();
                    _fontFamilyName = string.Empty;
                    _fontSize = 0;
                    break;
            }
        }

        public override void OnAnimationFrameUpdate()
        {
            if (!_running)
            {
                return;
            }

            TimeSpan now = CompositionNow;
            if (_lastBlinkToggle is null)
            {
                _lastBlinkToggle = now;
            }
            else
            {
                TimeSpan elapsed = now - _lastBlinkToggle.Value;
                if (elapsed >= CaretBlinkInterval)
                {
                    long tickSteps = elapsed.Ticks / CaretBlinkInterval.Ticks;
                    if ((tickSteps & 1L) == 1L)
                    {
                        _caretBlinkState = !_caretBlinkState;
                    }

                    _lastBlinkToggle = _lastBlinkToggle.Value + TimeSpan.FromTicks(tickSteps * CaretBlinkInterval.Ticks);
                    Invalidate();
                }
            }

            RegisterForNextAnimationFrameUpdate();
        }

        public override void OnRender(ImmediateDrawingContext context)
        {
            if (_retiredRenderStates.Count > 0)
            {
                DisposeRetiredRenderStates();
            }

            ITerminalViewModel? viewModel = _getViewModel();
            if (viewModel is null || _renderState is null)
            {
                return;
            }

            object? featureObj = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature));
            if (featureObj is not ISkiaSharpApiLeaseFeature leaseFeature)
            {
                return;
            }

            using ISkiaSharpApiLease lease = leaseFeature.Lease();
            SKCanvas canvas = lease.SkCanvas;
            Rect bounds = new(GetRenderBounds().Size);
            bool showCaret = _caretVisible && (!_caretBlink || _caretBlinkState);
            TerminalRenderer.Render(canvas, bounds, viewModel, _renderState, showCaret, _offsetY);
        }

        private void UpdateRenderState(string fontFamilyName, double fontSize)
        {
            if (_renderState is not null
                && string.Equals(_fontFamilyName, fontFamilyName, StringComparison.Ordinal)
                && Math.Abs(_fontSize - fontSize) < 0.01)
            {
                return;
            }

            if (_renderState is not null)
            {
                _retiredRenderStates.Add(_renderState);
            }

            Typeface typeface = new(new FontFamily(fontFamilyName), FontStyle.Normal, FontWeight.Normal);
            _renderState = TerminalRenderState.Create(typeface, fontSize);
            _fontFamilyName = fontFamilyName;
            _fontSize = fontSize;
        }

        private void UpdateAnimationState(bool resetBlinkState)
        {
            bool shouldAnimate = _caretVisible && _caretBlink;
            if (!shouldAnimate)
            {
                _running = false;
                _caretBlinkState = true;
                _lastBlinkToggle = null;
                return;
            }

            if (!_running || resetBlinkState)
            {
                _running = true;
                _caretBlinkState = true;
                _lastBlinkToggle = null;
            }
        }

        private void DisposeRenderState()
        {
            _renderState?.Dispose();
            _renderState = null;
        }

        private void DisposeRetiredRenderStates()
        {
            for (int i = 0; i < _retiredRenderStates.Count; i++)
            {
                _retiredRenderStates[i].Dispose();
            }

            _retiredRenderStates.Clear();
        }
    }

    private sealed class TerminalRenderState : IDisposable
    {
        public SKTypeface Typeface { get; }
        public TerminalCellSize CellSize { get; }
        public SKPaint TextPaint { get; }
        public SKPaint BackgroundPaint { get; }
        public SKPaint SelectionPaint { get; }
        public SKPaint CursorPaint { get; }
        public SKFont BaseFont { get; }
        public GlyphCache Glyphs { get; }

        private TerminalRenderState(
            SKTypeface typeface,
            TerminalCellSize cellSize,
            SKPaint textPaint,
            SKPaint backgroundPaint,
            SKPaint selectionPaint,
            SKPaint cursorPaint,
            SKFont baseFont,
            GlyphCache glyphs)
        {
            Typeface = typeface;
            CellSize = cellSize;
            TextPaint = textPaint;
            BackgroundPaint = backgroundPaint;
            SelectionPaint = selectionPaint;
            CursorPaint = cursorPaint;
            BaseFont = baseFont;
            Glyphs = glyphs;
        }

        public static TerminalRenderState Create(Typeface typeface, double fontSize)
        {
            SKTypeface skTypeface = ResolveSkiaTypeface(typeface);
            SKFont font = new(skTypeface, (float)fontSize);
            SKPaint textPaint = new()
            {
                Typeface = skTypeface,
                TextSize = (float)fontSize,
                IsAntialias = true
            };

            SKFontMetrics metrics = textPaint.FontMetrics;
            float rawCellHeight = metrics.Descent - metrics.Ascent + metrics.Leading;
            float rawCellWidth = GetMonospaceWidth(font, textPaint, (float)fontSize);
            float cellHeight = MathF.Max(1f, MathF.Round(rawCellHeight));
            float cellWidth = MathF.Max(1f, MathF.Round(rawCellWidth));
            float baseline = MathF.Round(-metrics.Ascent);
            TerminalCellSize cellSize = new(cellWidth, cellHeight, baseline);

            SKPaint backgroundPaint = new()
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill
            };

            SKPaint selectionPaint = new()
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill
            };

            SKPaint cursorPaint = new()
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill
            };

            GlyphCache glyphs = new(font, SKFontManager.Default);

            return new TerminalRenderState(skTypeface, cellSize, textPaint, backgroundPaint, selectionPaint, cursorPaint, font, glyphs);
        }

        private static float GetMonospaceWidth(SKFont font, SKPaint paint, float fontSize)
        {
            Span<ushort> glyphs = stackalloc ushort[1];
            font.GetGlyphs("M", glyphs);
            if (glyphs[0] != 0)
            {
                Span<float> widths = stackalloc float[1];
                Span<SKRect> bounds = stackalloc SKRect[1];
                font.GetGlyphWidths(glyphs, widths, bounds, paint);
                if (widths[0] > 0)
                {
                    return widths[0];
                }
            }

            float measured = paint.MeasureText("M");
            if (measured > 0)
            {
                return measured;
            }

            return Math.Max(1f, fontSize * 0.6f);
        }

        private static SKTypeface ResolveSkiaTypeface(Typeface typeface)
        {
            SKFontStyleWeight weight = SKFontStyleWeight.Normal;
            SKFontStyleSlant slant = SKFontStyleSlant.Upright;
            string familyList = typeface.FontFamily.Name;

            foreach (string family in familyList.Split(','))
            {
                string candidate = family.Trim();
                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }

                SKTypeface matched = SKTypeface.FromFamilyName(candidate, weight, SKFontStyleWidth.Normal, slant);
                if (string.Equals(candidate, "monospace", StringComparison.OrdinalIgnoreCase))
                {
                    return matched;
                }

                if (!string.IsNullOrEmpty(matched.FamilyName)
                    && string.Equals(matched.FamilyName, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return matched;
                }
            }

            return SKTypeface.FromFamilyName(familyList, weight, SKFontStyleWidth.Normal, slant);
        }

        public void Dispose()
        {
            TextPaint.Dispose();
            BackgroundPaint.Dispose();
            SelectionPaint.Dispose();
            CursorPaint.Dispose();
            BaseFont.Dispose();
            Glyphs.Dispose();
            Typeface.Dispose();
        }
    }

    private sealed class GlyphCache : IDisposable
    {
        private readonly SKFont _baseFont;
        private readonly SKFontManager _fontManager;
        private readonly Dictionary<int, GlyphInfo> _glyphs = new();
        private readonly Dictionary<SKTypeface, SKFont> _fonts = new();
        private readonly SKPaint _measurePaint = new();

        public GlyphCache(SKFont baseFont, SKFontManager fontManager)
        {
            _baseFont = baseFont;
            _fontManager = fontManager;
            _fonts[baseFont.Typeface] = baseFont;
        }

        public GlyphInfo Get(Rune rune)
        {
            int key = rune.Value;
            if (_glyphs.TryGetValue(key, out GlyphInfo cached))
            {
                return cached;
            }

            GlyphInfo info = ResolveGlyph(rune, _baseFont);
            _glyphs[key] = info;
            return info;
        }

        private GlyphInfo ResolveGlyph(Rune rune, SKFont baseFont)
        {
            Span<ushort> glyphs = stackalloc ushort[1];
            string text = rune.ToString();
            baseFont.GetGlyphs(text, glyphs);
            if (glyphs[0] != 0)
            {
                float advance = MeasureAdvance(baseFont, glyphs);
                return new GlyphInfo(baseFont, advance);
            }

            SKTypeface fallback = _fontManager.MatchCharacter(
                baseFont.Typeface.FamilyName,
                baseFont.Typeface.FontStyle,
                null,
                rune.Value) ?? baseFont.Typeface;

            SKFont font = GetFont(fallback, baseFont.Size);
            font.GetGlyphs(text, glyphs);
            float fallbackAdvance = glyphs[0] == 0 ? baseFont.Size * 0.6f : MeasureAdvance(font, glyphs);
            return new GlyphInfo(font, fallbackAdvance);
        }

        private float MeasureAdvance(SKFont font, ReadOnlySpan<ushort> glyphs)
        {
            Span<float> widths = stackalloc float[1];
            Span<SKRect> bounds = stackalloc SKRect[1];
            font.GetGlyphWidths(glyphs, widths, bounds, _measurePaint);
            return widths[0];
        }

        private SKFont GetFont(SKTypeface typeface, float size)
        {
            if (_fonts.TryGetValue(typeface, out SKFont? cached))
            {
                return cached;
            }

            SKFont font = new(typeface, size);
            _fonts[typeface] = font;
            return font;
        }

        public void Dispose()
        {
            foreach (KeyValuePair<SKTypeface, SKFont> entry in _fonts)
            {
                if (!ReferenceEquals(entry.Value, _baseFont))
                {
                    entry.Value.Dispose();
                }
            }

            _fonts.Clear();
            _glyphs.Clear();
            _measurePaint.Dispose();
        }
    }

    private readonly struct GlyphInfo
    {
        public SKFont Font { get; }
        public float Advance { get; }

        public GlyphInfo(SKFont font, float advance)
        {
            Font = font;
            Advance = advance;
        }
    }

    private readonly struct TerminalCellSize
    {
        public float Width { get; }
        public float Height { get; }
        public float Baseline { get; }

        public TerminalCellSize(float width, float height, float baseline)
        {
            Width = width;
            Height = height;
            Baseline = baseline;
        }
    }

    private static class TerminalRenderer
    {
        public static void Render(SKCanvas canvas, Rect bounds, ITerminalViewModel viewModel, TerminalRenderState state, bool showCaret, double offsetY)
        {
            ITerminalEmulator emulator = viewModel.Emulator;
            TerminalTheme theme = viewModel.Theme;

            float cellWidth = state.CellSize.Width;
            float cellHeight = state.CellSize.Height;
            float baseline = state.CellSize.Baseline;

            SKPaint textPaint = state.TextPaint;
            SKPaint backgroundPaint = state.BackgroundPaint;
            SKPaint selectionPaint = state.SelectionPaint;
            SKPaint cursorPaint = state.CursorPaint;
            GlyphCache glyphs = state.Glyphs;

            TerminalSelection selection = viewModel.Selection;
            TerminalSelection normalized = selection.IsActive ? selection.Normalize() : selection;

            canvas.Save();
            canvas.Translate((float)bounds.X, (float)(bounds.Y + offsetY));

            emulator.Read((buffer, tstate) =>
            {
                IReadOnlyList<TerminalLine> scrollback = buffer.Scrollback;
                int totalLines = scrollback.Count + buffer.Rows;
                int startIndex = Math.Max(0, scrollback.Count - viewModel.ScrollOffset);
                if (startIndex + buffer.Rows > totalLines)
                {
                    startIndex = Math.Max(0, totalLines - buffer.Rows);
                }

                for (int row = 0; row < buffer.Rows; row++)
                {
                    int globalIndex = startIndex + row;
                    TerminalLine line;
                    if (globalIndex < scrollback.Count)
                    {
                        line = scrollback[globalIndex];
                    }
                    else if (globalIndex - scrollback.Count < buffer.Rows)
                    {
                        line = buffer.GetLine(globalIndex - scrollback.Count);
                    }
                    else
                    {
                        line = new TerminalLine(buffer.Columns, TerminalAttributes.Default);
                    }
                    for (int col = 0; col < buffer.Columns; col++)
                    {
                        TerminalCell cell = line.Cells[col];
                        bool isSelected = selection.IsActive && IsSelectedCell(normalized, globalIndex, col);

                        TerminalAttributes attrs = cell.Attributes;
                        TerminalRgb fg = TerminalPalette.Resolve(attrs.Foreground, theme);
                        TerminalRgb bg = TerminalPalette.ResolveBackground(attrs.Background, theme);

                        if (attrs.Inverse)
                        {
                            TerminalRgb temp = fg;
                            fg = bg;
                            bg = temp;
                        }

                        if (isSelected)
                        {
                            selectionPaint.Color = new SKColor(
                                theme.SelectionBackground.R,
                                theme.SelectionBackground.G,
                                theme.SelectionBackground.B);
                            canvas.DrawRect(MathF.Round(col * cellWidth), MathF.Round(row * cellHeight), cellWidth, cellHeight, selectionPaint);
                        }
                        else
                        {
                            backgroundPaint.Color = new SKColor(bg.R, bg.G, bg.B);
                            canvas.DrawRect(MathF.Round(col * cellWidth), MathF.Round(row * cellHeight), cellWidth, cellHeight, backgroundPaint);
                        }

                        if (cell.Width == 0)
                        {
                            DrawCombiningGlyph(canvas, textPaint, glyphs, cell, col, row, cellWidth, cellHeight, baseline, theme, isSelected);
                            continue;
                        }

                        TerminalRgb textColor = isSelected
                            ? theme.SelectionForeground
                            : fg;
                        textPaint.Color = new SKColor(textColor.R, textColor.G, textColor.B);
                        textPaint.FakeBoldText = attrs.Bold;

                        GlyphInfo glyph = glyphs.Get(cell.Rune);
                        float x = MathF.Round(col * cellWidth);
                        float y = MathF.Round(row * cellHeight + baseline);
                        float cellSpan = Math.Max(1, (int)cell.Width) * cellWidth;
                        canvas.Save();
                        canvas.ClipRect(new SKRect(x, y - baseline, x + cellSpan, y - baseline + cellHeight));
                        canvas.DrawText(cell.Rune.ToString(), x, y, glyph.Font, textPaint);
                        canvas.Restore();
                    }
                }

                if (showCaret && tstate.CursorRow >= 0 && tstate.CursorRow < buffer.Rows)
                {
                    int cursorCol = Math.Clamp(tstate.CursorColumn, 0, buffer.Columns - 1);
                    float x = MathF.Round(cursorCol * cellWidth);
                    float y = MathF.Round(tstate.CursorRow * cellHeight);
                    int globalCursorRow = startIndex + tstate.CursorRow;
                    bool cursorSelected = selection.IsActive && IsSelectedCell(normalized, globalCursorRow, cursorCol);
                    TerminalRgb cursorColor = cursorSelected ? theme.SelectionForeground : theme.Foreground;
                    cursorPaint.Color = new SKColor(cursorColor.R, cursorColor.G, cursorColor.B, 0x90);
                    DrawCursor(canvas, cursorPaint, tstate.CursorShape, x, y, cellWidth, cellHeight);
                }
            });

            canvas.Restore();
        }

        private static void DrawCombiningGlyph(
            SKCanvas canvas,
            SKPaint textPaint,
            GlyphCache glyphs,
            TerminalCell cell,
            int col,
            int row,
            float cellWidth,
            float cellHeight,
            float baseline,
            TerminalTheme theme,
            bool isSelected)
        {
            TerminalAttributes attrs = cell.Attributes;
            TerminalRgb fg = TerminalPalette.Resolve(attrs.Foreground, theme);
            TerminalRgb bg = TerminalPalette.ResolveBackground(attrs.Background, theme);
            if (attrs.Inverse)
            {
                TerminalRgb temp = fg;
                fg = bg;
                bg = temp;
            }

            TerminalRgb textColor = isSelected ? theme.SelectionForeground : fg;
            textPaint.Color = new SKColor(textColor.R, textColor.G, textColor.B);
            textPaint.FakeBoldText = attrs.Bold;

            GlyphInfo glyph = glyphs.Get(cell.Rune);
            int targetCol = Math.Max(0, col - 1);
            float x = MathF.Round(targetCol * cellWidth);
            float y = MathF.Round(row * cellHeight + baseline);
            canvas.Save();
            canvas.ClipRect(new SKRect(x, y - baseline, x + cellWidth, y - baseline + cellHeight));
            canvas.DrawText(cell.Rune.ToString(), x, y, glyph.Font, textPaint);
            canvas.Restore();
        }

        private static void DrawCursor(
            SKCanvas canvas,
            SKPaint cursorPaint,
            TerminalCursorShape shape,
            float x,
            float y,
            float cellWidth,
            float cellHeight)
        {
            switch (shape)
            {
                case TerminalCursorShape.Underline:
                    float underlineHeight = Math.Max(1f, cellHeight * 0.12f);
                    canvas.DrawRect(x, y + cellHeight - underlineHeight, cellWidth, underlineHeight, cursorPaint);
                    break;
                case TerminalCursorShape.Bar:
                    float barWidth = Math.Max(1f, cellWidth * 0.12f);
                    canvas.DrawRect(x, y, barWidth, cellHeight, cursorPaint);
                    break;
                default:
                    canvas.DrawRect(x, y, cellWidth, cellHeight, cursorPaint);
                    break;
            }
        }

        private static float GetGlyphOffset(GlyphInfo glyph, SKFont baseFont, float cellWidth, int cellSpan)
        {
            return 0f;
        }

        private static bool IsSelectedCell(TerminalSelection selection, int row, int col)
        {
            if (!selection.IsActive)
            {
                return false;
            }

            if (row < selection.StartRow || row > selection.EndRow)
            {
                return false;
            }

            if (selection.StartRow == selection.EndRow)
            {
                return col >= selection.StartColumn && col <= selection.EndColumn;
            }

            if (row == selection.StartRow)
            {
                return col >= selection.StartColumn;
            }

            if (row == selection.EndRow)
            {
                return col <= selection.EndColumn;
            }

            return true;
        }
    }
}
