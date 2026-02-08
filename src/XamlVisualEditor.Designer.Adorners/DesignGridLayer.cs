using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XamlVisualEditor.Designer.Adorners;

/// <summary>
/// Renders a simple design-time grid behind the surface content.
/// </summary>
public sealed class DesignGridLayer : Control
{
    private static readonly IPen MinorPen = new Pen(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), 1);
    private static readonly IPen MajorPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<DesignGridLayer, bool>(nameof(ShowGrid));

    public static readonly StyledProperty<double> GridSizeProperty =
        AvaloniaProperty.Register<DesignGridLayer, double>(nameof(GridSize), 8.0);

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public double GridSize
    {
        get => GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    public DesignGridLayer()
    {
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!ShowGrid || GridSize <= 1)
        {
            return;
        }

        double width = Bounds.Width;
        double height = Bounds.Height;
        double majorStep = GridSize * 8;

        for (double x = 0; x <= width; x += GridSize)
        {
            IPen pen = Math.Abs(x % majorStep) < 0.1 ? MajorPen : MinorPen;
            context.DrawLine(pen, new Point(x, 0), new Point(x, height));
        }

        for (double y = 0; y <= height; y += GridSize)
        {
            IPen pen = Math.Abs(y % majorStep) < 0.1 ? MajorPen : MinorPen;
            context.DrawLine(pen, new Point(0, y), new Point(width, y));
        }
    }
}
