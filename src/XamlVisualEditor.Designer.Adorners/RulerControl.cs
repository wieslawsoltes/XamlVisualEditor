using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace XamlVisualEditor.Designer.Adorners;

public enum RulerOrientation
{
    Horizontal,
    Vertical
}

/// <summary>
/// Simple ruler control that supports tick marks and guide creation.
/// </summary>
public sealed class RulerControl : Control
{
    private const double MinTickSpacing = 8.0;
    private const double GuideHitThreshold = 6.0;
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#252526"));
    private static readonly IBrush TickBrush = new SolidColorBrush(Color.Parse("#8B8B8B"));
    private static readonly IBrush LabelBrush = Brushes.White;
    private static readonly IBrush SelectionBrush = new SolidColorBrush(Color.FromArgb(60, 0, 120, 215));
    private static readonly IBrush GuideBrush = new SolidColorBrush(Color.Parse("#3FA9F5"));

    private int _dragGuideIndex = -1;
    private bool _isDragging;

    public static readonly StyledProperty<RulerOrientation> OrientationProperty =
        AvaloniaProperty.Register<RulerControl, RulerOrientation>(
            nameof(Orientation),
            RulerOrientation.Horizontal);

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<RulerControl, double>(nameof(Zoom), 1.0);

    public static readonly StyledProperty<double> OffsetProperty =
        AvaloniaProperty.Register<RulerControl, double>(nameof(Offset), 0.0);

    public static readonly StyledProperty<IReadOnlyList<double>> GuidesProperty =
        AvaloniaProperty.Register<RulerControl, IReadOnlyList<double>>(
            nameof(Guides),
            Array.Empty<double>());

    public static readonly StyledProperty<double?> SelectionStartProperty =
        AvaloniaProperty.Register<RulerControl, double?>(nameof(SelectionStart));

    public static readonly StyledProperty<double?> SelectionEndProperty =
        AvaloniaProperty.Register<RulerControl, double?>(nameof(SelectionEnd));

    public RulerOrientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double Offset
    {
        get => GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    public IReadOnlyList<double> Guides
    {
        get => GetValue(GuidesProperty);
        set => SetValue(GuidesProperty, value);
    }

    public double? SelectionStart
    {
        get => GetValue(SelectionStartProperty);
        set => SetValue(SelectionStartProperty, value);
    }

    public double? SelectionEnd
    {
        get => GetValue(SelectionEndProperty);
        set => SetValue(SelectionEndProperty, value);
    }

    public RulerControl()
    {
        ClipToBounds = true;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.DrawRectangle(BackgroundBrush, null, Bounds);

        double length = Orientation == RulerOrientation.Horizontal ? Bounds.Width : Bounds.Height;
        double thickness = Orientation == RulerOrientation.Horizontal ? Bounds.Height : Bounds.Width;
        double zoom = Math.Max(0.01, Zoom);

        double minorUnit = 10.0;
        while (minorUnit * zoom < MinTickSpacing)
        {
            minorUnit *= 2.0;
        }

        double majorUnit = minorUnit * 5.0;
        double labelUnit = majorUnit * 2.0;

        double startDoc = Math.Floor((Offset / zoom) / minorUnit) * minorUnit;
        double endDoc = (Offset + length) / zoom;

        DrawSelectionRange(context, thickness, zoom, length);

        for (double value = startDoc; value <= endDoc; value += minorUnit)
        {
            double pos = value * zoom - Offset;
            if (pos < 0 || pos > length)
            {
                continue;
            }

            bool isMajor = Math.Abs(value % majorUnit) < 0.001;
            bool isLabel = Math.Abs(value % labelUnit) < 0.001;
            double tickLength = isMajor ? thickness * 0.6 : thickness * 0.35;

            if (Orientation == RulerOrientation.Horizontal)
            {
                context.DrawLine(new Pen(TickBrush, 1), new Point(pos, thickness), new Point(pos, thickness - tickLength));
            }
            else
            {
                context.DrawLine(new Pen(TickBrush, 1), new Point(thickness, pos), new Point(thickness - tickLength, pos));
            }

            if (isLabel)
            {
                string label = value.ToString("0");
                FormattedText text = new(
                    label,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    10,
                    LabelBrush);

                if (Orientation == RulerOrientation.Horizontal)
                {
                    context.DrawText(text, new Point(pos + 2, 2));
                }
                else
                {
                    context.DrawText(text, new Point(2, pos + 2));
                }
            }
        }

        DrawGuideMarkers(context, thickness, zoom, length);
    }

    private void DrawSelectionRange(DrawingContext context, double thickness, double zoom, double length)
    {
        if (!SelectionStart.HasValue || !SelectionEnd.HasValue)
        {
            return;
        }

        double start = SelectionStart.Value * zoom - Offset;
        double end = SelectionEnd.Value * zoom - Offset;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        start = Math.Clamp(start, 0, length);
        end = Math.Clamp(end, 0, length);

        if (end <= start)
        {
            return;
        }

        if (Orientation == RulerOrientation.Horizontal)
        {
            context.DrawRectangle(SelectionBrush, null, new Rect(start, 0, end - start, thickness));
        }
        else
        {
            context.DrawRectangle(SelectionBrush, null, new Rect(0, start, thickness, end - start));
        }
    }

    private void DrawGuideMarkers(DrawingContext context, double thickness, double zoom, double length)
    {
        if (Guides is null || Guides.Count == 0)
        {
            return;
        }

        foreach (double guide in Guides)
        {
            double pos = guide * zoom - Offset;
            if (pos < 0 || pos > length)
            {
                continue;
            }

            if (Orientation == RulerOrientation.Horizontal)
            {
                context.DrawLine(new Pen(GuideBrush, 2), new Point(pos, thickness), new Point(pos, thickness - 8));
            }
            else
            {
                context.DrawLine(new Pen(GuideBrush, 2), new Point(thickness, pos), new Point(thickness - 8, pos));
            }
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            !e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            return;
        }

        IList<double>? guides = GetMutableGuides();
        if (guides is null)
        {
            return;
        }

        Point pos = e.GetPosition(this);
        double rulerPos = Orientation == RulerOrientation.Horizontal ? pos.X : pos.Y;
        double guideValue = (rulerPos + Offset) / Math.Max(0.01, Zoom);

        int nearest = FindNearestGuideIndex(guides, rulerPos);
        bool isRight = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;
        if (isRight)
        {
            if (nearest >= 0)
            {
                guides.RemoveAt(nearest);
                InvalidateVisual();
            }
            return;
        }

        if (nearest >= 0)
        {
            _dragGuideIndex = nearest;
            _isDragging = true;
        }
        else
        {
            guides.Add(guideValue);
            _dragGuideIndex = guides.Count - 1;
            _isDragging = true;
        }

        if (_dragGuideIndex >= 0)
        {
            guides[_dragGuideIndex] = guideValue;
        }

        e.Pointer.Capture(this);
        InvalidateVisual();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        IList<double>? guides = GetMutableGuides();
        if (guides is null || _dragGuideIndex < 0 || _dragGuideIndex >= guides.Count)
        {
            return;
        }

        Point pos = e.GetPosition(this);
        double rulerPos = Orientation == RulerOrientation.Horizontal ? pos.X : pos.Y;
        double guideValue = (rulerPos + Offset) / Math.Max(0.01, Zoom);
        guides[_dragGuideIndex] = guideValue;
        InvalidateVisual();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _dragGuideIndex = -1;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    private IList<double>? GetMutableGuides()
    {
        if (Guides is ObservableCollection<double> collection)
        {
            return collection;
        }

        if (Guides is IList<double> list)
        {
            return list;
        }

        return null;
    }

    private int FindNearestGuideIndex(IList<double> guides, double rulerPos)
    {
        if (guides.Count == 0)
        {
            return -1;
        }

        double zoom = Math.Max(0.01, Zoom);
        double bestDistance = GuideHitThreshold + 1;
        int bestIndex = -1;

        for (int i = 0; i < guides.Count; i++)
        {
            double pos = guides[i] * zoom - Offset;
            double distance = Math.Abs(pos - rulerPos);
            if (distance <= GuideHitThreshold && distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}
