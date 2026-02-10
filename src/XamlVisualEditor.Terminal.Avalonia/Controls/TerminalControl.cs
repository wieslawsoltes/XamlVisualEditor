using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Terminal;

namespace XamlVisualEditor.Terminal.Avalonia.Controls;

public sealed class TerminalControl : Control
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

    public static readonly DirectProperty<TerminalControl, TerminalViewModel?> TerminalViewModelProperty =
        AvaloniaProperty.RegisterDirect<TerminalControl, TerminalViewModel?>(
            nameof(TerminalViewModel),
            o => o.TerminalViewModel,
            (o, v) => o.TerminalViewModel = v);

    private TerminalViewModel? _terminalViewModel;
    private TerminalRenderState? _renderState;
    private readonly DispatcherTimer _caretTimer;
    private bool _isFocused;
    private bool _isCaretVisible = true;
    private double _offsetY;

    public TerminalViewModel? TerminalViewModel
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
            InvalidateVisual();
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
        _caretTimer = new DispatcherTimer { Interval = CaretBlinkInterval };
        _caretTimer.Tick += (_, _) => ToggleCaret();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_terminalViewModel is null)
        {
            return;
        }

        if (_renderState is null)
        {
            UpdateRenderState();
        }

        if (_renderState is null)
        {
            return;
        }

        bool showCaret = _isFocused
            && (_terminalViewModel.Emulator.State.CursorBlink ? _isCaretVisible : true)
            && _terminalViewModel.ScrollOffset == 0
            && _terminalViewModel.Emulator.State.CursorVisible;
        context.Custom(new TerminalDrawOperation(Bounds, _terminalViewModel, _renderState, showCaret, _offsetY));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty ||
            change.Property == FontFamilyProperty ||
            change.Property == FontSizeProperty)
        {
            UpdateRenderState();
            InvalidateVisual();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _caretTimer.Stop();
        _renderState?.Dispose();
        _renderState = null;
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        _isFocused = true;
        _isCaretVisible = true;
        if (!_caretTimer.IsEnabled)
        {
            _caretTimer.Start();
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        _isFocused = false;
        _isCaretVisible = false;
        _caretTimer.Stop();
        InvalidateVisual();
    }

    private void UpdateRenderState()
    {
        if (_terminalViewModel is null)
        {
            return;
        }

        Typeface typeface = new(FontFamily, FontStyle.Normal, FontWeight.Normal);
        _renderState = TerminalRenderState.Create(typeface, FontSize);

        int cols = (int)Math.Floor(Bounds.Width / _renderState.CellSize.Width);
        int rows = (int)Math.Floor(Bounds.Height / _renderState.CellSize.Height);
        double usedHeight = rows > 0 ? rows * _renderState.CellSize.Height : 0;
        double remainderOffset = Math.Max(0, Bounds.Height - usedHeight);
        _offsetY = MathF.Round((float)remainderOffset);
        if (cols > 0 && rows > 0)
        {
            _terminalViewModel.SetMetrics(new TerminalMetrics(_renderState.CellSize.Width, _renderState.CellSize.Height, 0, _offsetY));
            _terminalViewModel.Resize(cols, rows);
        }

        if (_isFocused)
        {
            _isCaretVisible = true;
            if (!_caretTimer.IsEnabled)
            {
                _caretTimer.Start();
            }
        }
    }

    private void OnFrameInvalidated()
    {
        _isCaretVisible = true;
        if (_isFocused && !_caretTimer.IsEnabled)
        {
            _caretTimer.Start();
        }
        Dispatcher.UIThread.Post(InvalidateVisual);
    }

    private void ToggleCaret()
    {
        if (!_isFocused)
        {
            return;
        }

        _isCaretVisible = !_isCaretVisible;
        InvalidateVisual();
    }

    private sealed class TerminalDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly TerminalViewModel _viewModel;
        private readonly TerminalRenderState _state;

        private readonly bool _showCaret;

        private readonly double _offsetY;

        public TerminalDrawOperation(Rect bounds, TerminalViewModel viewModel, TerminalRenderState state, bool showCaret, double offsetY)
        {
            _bounds = bounds;
            _viewModel = viewModel;
            _state = state;
            _showCaret = showCaret;
            _offsetY = offsetY;
        }

        public Rect Bounds => _bounds;

        public void Dispose()
        {
        }

        public bool HitTest(Point p) => _bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            object? featureObj = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature));
            if (featureObj is not ISkiaSharpApiLeaseFeature feature)
            {
                return;
            }

            using ISkiaSharpApiLease lease = feature.Lease();
            SKCanvas canvas = lease.SkCanvas;
            TerminalRenderer.Render(canvas, _bounds, _viewModel, _state, _showCaret, _offsetY);
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return other is TerminalDrawOperation op
                && op._viewModel == _viewModel
                && op._bounds == _bounds
                && op._showCaret == _showCaret
                && Math.Abs(op._offsetY - _offsetY) < 0.5;
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
        public static void Render(SKCanvas canvas, Rect bounds, TerminalViewModel viewModel, TerminalRenderState state, bool showCaret, double offsetY)
        {
            TerminalEmulator emulator = viewModel.Emulator;
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
