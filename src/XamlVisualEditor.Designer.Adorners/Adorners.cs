using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Designer.Core;

namespace XamlVisualEditor.Designer.Adorners;

/// <summary>
/// Renders selection adorners (blue rectangles) around selected design items.
/// </summary>
public sealed class SelectionAdorner
{
    private static readonly IPen SelectionPen = new Pen(Brushes.DodgerBlue, 2);
    private static readonly IPen HoverPen = new Pen(Brushes.LightSkyBlue, 1, DashStyle.Dash);

    /// <summary>
    /// Renders selection adorners for the given items.
    /// </summary>
    public void Render(DrawingContext context, IReadOnlyList<IDesignItem> selectedItems, Control? surfaceRoot)
    {
        foreach (IDesignItem item in selectedItems)
        {
            if (item is DesignItem designItem && designItem.VisualElement is not null)
            {
                Rect bounds = surfaceRoot is not null
                    ? designItem.GetBoundsRelativeTo(surfaceRoot)
                    : designItem.Bounds;
                context.DrawRectangle(null, SelectionPen, bounds);
            }
        }
    }

    /// <summary>
    /// Renders a hover outline for an item under the cursor.
    /// </summary>
    public void RenderHover(DrawingContext context, IDesignItem? hoveredItem, Control? surfaceRoot)
    {
        if (hoveredItem is DesignItem designItem && designItem.VisualElement is not null)
        {
            Rect bounds = surfaceRoot is not null
                ? designItem.GetBoundsRelativeTo(surfaceRoot)
                : designItem.Bounds;
            context.DrawRectangle(null, HoverPen, bounds);
        }
    }
}

/// <summary>
/// Renders resize handles at the corners and edges of selected items.
/// </summary>
public sealed class ResizeHandleAdorner
{
    private const double HandleSize = 8;
    private static readonly IBrush HandleFill = Brushes.White;
    private static readonly IPen HandleStroke = new Pen(Brushes.DodgerBlue, 1);

    /// <summary>
    /// Gets the resize handle positions for a given bounding rectangle.
    /// </summary>
    public static ResizeHandle[] GetHandles(Rect bounds)
    {
        ResizeHandle[] handles = new ResizeHandle[8];
        handles[0] = new ResizeHandle(ResizeDirection.TopLeft, new Point(bounds.Left, bounds.Top));
        handles[1] = new ResizeHandle(ResizeDirection.Top, new Point(bounds.Center.X, bounds.Top));
        handles[2] = new ResizeHandle(ResizeDirection.TopRight, new Point(bounds.Right, bounds.Top));
        handles[3] = new ResizeHandle(ResizeDirection.Right, new Point(bounds.Right, bounds.Center.Y));
        handles[4] = new ResizeHandle(ResizeDirection.BottomRight, new Point(bounds.Right, bounds.Bottom));
        handles[5] = new ResizeHandle(ResizeDirection.Bottom, new Point(bounds.Center.X, bounds.Bottom));
        handles[6] = new ResizeHandle(ResizeDirection.BottomLeft, new Point(bounds.Left, bounds.Bottom));
        handles[7] = new ResizeHandle(ResizeDirection.Left, new Point(bounds.Left, bounds.Center.Y));
        return handles;
    }

    /// <summary>
    /// Renders resize handles.
    /// </summary>
    public void Render(DrawingContext context, Rect bounds)
    {
        IReadOnlyList<ResizeHandle> handles = GetHandles(bounds);

        foreach (ResizeHandle handle in handles)
        {
            Rect handleRect = new(
                handle.Position.X - HandleSize / 2,
                handle.Position.Y - HandleSize / 2,
                HandleSize,
                HandleSize);

            context.DrawRectangle(HandleFill, HandleStroke, handleRect);
        }
    }

    /// <summary>
    /// Hit-tests against resize handles.
    /// </summary>
    public ResizeHandle? HitTest(Rect bounds, Point point)
    {
        IReadOnlyList<ResizeHandle> handles = GetHandles(bounds);

        foreach (ResizeHandle handle in handles)
        {
            Rect handleRect = new(
                handle.Position.X - HandleSize / 2,
                handle.Position.Y - HandleSize / 2,
                HandleSize,
                HandleSize);

            if (handleRect.Contains(point))
            {
                return handle;
            }
        }

        return null;
    }
}

/// <summary>
/// Represents a resize handle at a specific position.
/// </summary>
public sealed class ResizeHandle
{
    public ResizeHandle(ResizeDirection direction, Point position)
    {
        Direction = direction;
        Position = position;
    }

    /// <summary>Gets the resize direction.</summary>
    public ResizeDirection Direction { get; }

    /// <summary>Gets the handle position.</summary>
    public Point Position { get; }
}

/// <summary>
/// Direction of a resize operation.
/// </summary>
public enum ResizeDirection
{
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left
}

/// <summary>
/// Renders snap lines for alignment during drag operations.
/// </summary>
public sealed class SnapLineAdorner
{
    private static readonly IPen SnapLinePen = new Pen(Brushes.Magenta, 1, DashStyle.Dash);

    /// <summary>
    /// Renders horizontal and vertical snap lines.
    /// </summary>
    public void Render(
        DrawingContext context,
        IReadOnlyList<double> horizontalLines,
        IReadOnlyList<double> verticalLines,
        Size surfaceSize)
    {
        foreach (double y in horizontalLines)
        {
            context.DrawLine(SnapLinePen, new Point(0, y), new Point(surfaceSize.Width, y));
        }

        foreach (double x in verticalLines)
        {
            context.DrawLine(SnapLinePen, new Point(x, 0), new Point(x, surfaceSize.Height));
        }
    }
}

/// <summary>
/// Renders margin and padding visualization around a design item.
/// </summary>
public sealed class MarginPaddingAdorner
{
    private static readonly IBrush MarginBrush = new SolidColorBrush(Color.FromArgb(50, 255, 165, 0));
    private static readonly IBrush PaddingBrush = new SolidColorBrush(Color.FromArgb(50, 0, 128, 0));

    /// <summary>
    /// Renders margin visualization.
    /// </summary>
    public void RenderMargin(DrawingContext context, Rect bounds, Thickness margin)
    {
        // Top margin
        if (margin.Top > 0)
        {
            context.DrawRectangle(MarginBrush, null,
                new Rect(bounds.Left, bounds.Top - margin.Top, bounds.Width, margin.Top));
        }

        // Bottom margin
        if (margin.Bottom > 0)
        {
            context.DrawRectangle(MarginBrush, null,
                new Rect(bounds.Left, bounds.Bottom, bounds.Width, margin.Bottom));
        }

        // Left margin
        if (margin.Left > 0)
        {
            context.DrawRectangle(MarginBrush, null,
                new Rect(bounds.Left - margin.Left, bounds.Top, margin.Left, bounds.Height));
        }

        // Right margin
        if (margin.Right > 0)
        {
            context.DrawRectangle(MarginBrush, null,
                new Rect(bounds.Right, bounds.Top, margin.Right, bounds.Height));
        }
    }

    /// <summary>
    /// Renders padding visualization.
    /// </summary>
    public void RenderPadding(DrawingContext context, Rect bounds, Thickness padding)
    {
        // Top padding
        if (padding.Top > 0)
        {
            context.DrawRectangle(PaddingBrush, null,
                new Rect(bounds.Left, bounds.Top, bounds.Width, padding.Top));
        }

        // Bottom padding
        if (padding.Bottom > 0)
        {
            context.DrawRectangle(PaddingBrush, null,
                new Rect(bounds.Left, bounds.Bottom - padding.Bottom, bounds.Width, padding.Bottom));
        }

        // Left padding
        if (padding.Left > 0)
        {
            context.DrawRectangle(PaddingBrush, null,
                new Rect(bounds.Left, bounds.Top, padding.Left, bounds.Height));
        }

        // Right padding
        if (padding.Right > 0)
        {
            context.DrawRectangle(PaddingBrush, null,
                new Rect(bounds.Right - padding.Right, bounds.Top, padding.Right, bounds.Height));
        }
    }
}

/// <summary>
/// Renders drop target indicators during drag-and-drop.
/// </summary>
public sealed class DropTargetAdorner
{
    private static readonly IPen DropIndicatorPen = new Pen(Brushes.Green, 3);
    private static readonly IBrush DropOverlayBrush = new SolidColorBrush(Color.FromArgb(30, 0, 128, 0));

    /// <summary>
    /// Renders a drop indicator at the specified position.
    /// </summary>
    public void Render(DrawingContext context, Rect targetBounds, DropPosition position)
    {
        switch (position)
        {
            case DropPosition.Inside:
                context.DrawRectangle(DropOverlayBrush, DropIndicatorPen, targetBounds);
                break;

            case DropPosition.Before:
                context.DrawLine(DropIndicatorPen,
                    new Point(targetBounds.Left, targetBounds.Top),
                    new Point(targetBounds.Right, targetBounds.Top));
                break;

            case DropPosition.After:
                context.DrawLine(DropIndicatorPen,
                    new Point(targetBounds.Left, targetBounds.Bottom),
                    new Point(targetBounds.Right, targetBounds.Bottom));
                break;
        }
    }
}

/// <summary>
/// Renders a semi-transparent ghost element during drag-and-drop operations.
/// </summary>
public sealed class DragGhostAdorner
{
    private static readonly IBrush GhostFill = new SolidColorBrush(Color.FromArgb(60, 70, 130, 180));
    private static readonly IPen GhostStroke = new Pen(Brushes.SteelBlue, 1, DashStyle.Dash);

    private Point _position;
    private Size _size;
    private string? _typeName;
    private bool _isActive;

    /// <summary>
    /// Starts showing a drag ghost at the given position.
    /// </summary>
    public void Start(string typeName, Size elementSize, Point initialPosition)
    {
        _typeName = typeName;
        _size = elementSize.Width > 0 && elementSize.Height > 0 ? elementSize : new Size(80, 30);
        _position = initialPosition;
        _isActive = true;
    }

    /// <summary>
    /// Updates the ghost position during drag.
    /// </summary>
    public void UpdatePosition(Point position)
    {
        _position = position;
    }

    /// <summary>
    /// Stops showing the drag ghost.
    /// </summary>
    public void Stop()
    {
        _isActive = false;
        _typeName = null;
    }

    /// <summary>
    /// Gets whether the ghost is currently active.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Renders the drag ghost.
    /// </summary>
    public void Render(DrawingContext context)
    {
        if (!_isActive)
        {
            return;
        }

        Rect ghostRect = new(_position.X - _size.Width / 2, _position.Y - _size.Height / 2, _size.Width, _size.Height);
        context.DrawRectangle(GhostFill, GhostStroke, ghostRect);

        if (_typeName is not null)
        {
            FormattedText text = new(
                _typeName,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                11,
                Brushes.White);

            context.DrawText(text, new Point(ghostRect.X + 4, ghostRect.Y + 4));
        }
    }
}

// ==============================================
// 8.2.5 — Presence Indicators on Designer Surface
// ==============================================

/// <summary>
/// Renders colored presence indicators on the designer surface
/// showing where remote collaborators are working (selected elements).
/// </summary>
public sealed class PresenceAdorner
{
    private readonly List<PresenceIndicator> _indicators = new();
    private readonly Dictionary<string, (IPen Pen, IBrush Brush)> _cachedResources = new();

    /// <summary>
    /// Updates the remote participant presence indicators.
    /// </summary>
    public void UpdatePresence(IEnumerable<PresenceIndicator> indicators)
    {
        _indicators.Clear();
        _indicators.AddRange(indicators);
    }

    /// <summary>
    /// Clears all presence indicators.
    /// </summary>
    public void Clear()
    {
        _indicators.Clear();
        _cachedResources.Clear();
    }

    /// <summary>
    /// Renders all presence indicators.
    /// </summary>
    public void Render(DrawingContext context)
    {
        foreach (PresenceIndicator indicator in _indicators)
        {
            if (indicator.Bounds.Width <= 0 || indicator.Bounds.Height <= 0)
            {
                continue;
            }

            // Cache Pen and Brush per color to avoid render-loop allocations
            if (!_cachedResources.TryGetValue(indicator.Color, out var resources))
            {
                Color color = ParseColor(indicator.Color);
                SolidColorBrush brush = new(color);
                IPen pen = new Pen(brush, 2, DashStyle.Dash);
                resources = (pen, brush);
                _cachedResources[indicator.Color] = resources;
            }

            // Draw dashed border around the selected element
            context.DrawRectangle(null, resources.Pen, indicator.Bounds);

            // Draw participant name label above the element
            FormattedText nameText = new(
                indicator.DisplayName,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                10,
                Brushes.White);

            double labelWidth = nameText.Width + 8;
            double labelHeight = nameText.Height + 4;
            Rect labelRect = new(
                indicator.Bounds.X,
                indicator.Bounds.Y - labelHeight - 2,
                labelWidth,
                labelHeight);

            // Draw label background
            context.DrawRectangle(resources.Brush, null, labelRect, 3, 3);
            context.DrawText(nameText, new Point(labelRect.X + 4, labelRect.Y + 2));
        }
    }

    private static Color ParseColor(string hex)
    {
        if (Color.TryParse(hex, out Color result))
        {
            return result;
        }
        return Colors.CornflowerBlue;
    }
}

/// <summary>
/// Describes a remote participant's presence on the designer surface.
/// </summary>
public sealed class PresenceIndicator
{
    /// <summary>Gets the participant identifier.</summary>
    public string ParticipantId { get; init; } = string.Empty;

    /// <summary>Gets the participant's display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets the participant's assigned color.</summary>
    public string Color { get; init; } = "#0078D4";

    /// <summary>Gets the bounds of the element the participant has selected.</summary>
    public Rect Bounds { get; init; }
}
