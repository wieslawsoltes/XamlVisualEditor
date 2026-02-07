using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Designer.Core;

namespace XamlVisualEditor.Designer.Adorners;

/// <summary>
/// A transparent overlay control that renders adorners (selection, resize handles,
/// snap lines, margin/padding, drop targets) on top of the design surface.
/// </summary>
public sealed class DesignAdornerLayer : Control
{
    private readonly SelectionAdorner _selectionAdorner = new();
    private readonly ResizeHandleAdorner _resizeHandleAdorner = new();
    private readonly SnapLineAdorner _snapLineAdorner = new();
    private readonly MarginPaddingAdorner _marginPaddingAdorner = new();
    private readonly DropTargetAdorner _dropTargetAdorner = new();

    private IReadOnlyList<IDesignItem> _selectedItems = Array.Empty<IDesignItem>();
    private IDesignItem? _hoveredItem;
    private IReadOnlyList<double> _snapHorizontalLines = Array.Empty<double>();
    private IReadOnlyList<double> _snapVerticalLines = Array.Empty<double>();
    private DropPosition? _dropPosition;
    private Rect? _dropTargetBounds;
    private bool _showMarginPadding;

    /// <summary>
    /// Gets or sets whether to show margin/padding adorners on the selected item.
    /// </summary>
    public bool ShowMarginPadding
    {
        get => _showMarginPadding;
        set
        {
            _showMarginPadding = value;
            InvalidateVisual();
        }
    }

    public DesignAdornerLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    /// <summary>
    /// Updates the selected items to render selection adorners for.
    /// </summary>
    public void UpdateSelection(IReadOnlyList<IDesignItem> selectedItems)
    {
        _selectedItems = selectedItems ?? Array.Empty<IDesignItem>();
        InvalidateVisual();
    }

    /// <summary>
    /// Updates the hovered item for hover outline rendering.
    /// </summary>
    public void UpdateHover(IDesignItem? hoveredItem)
    {
        _hoveredItem = hoveredItem;
        InvalidateVisual();
    }

    /// <summary>
    /// Updates snap lines to display during drag operations.
    /// </summary>
    public void UpdateSnapLines(IReadOnlyList<double> horizontalLines, IReadOnlyList<double> verticalLines)
    {
        _snapHorizontalLines = horizontalLines ?? Array.Empty<double>();
        _snapVerticalLines = verticalLines ?? Array.Empty<double>();
        InvalidateVisual();
    }

    /// <summary>
    /// Updates the drop target indicator.
    /// </summary>
    public void UpdateDropTarget(Rect? targetBounds, DropPosition? position)
    {
        _dropTargetBounds = targetBounds;
        _dropPosition = position;
        InvalidateVisual();
    }

    /// <summary>
    /// Clears all adorner state.
    /// </summary>
    public void ClearAll()
    {
        _selectedItems = Array.Empty<IDesignItem>();
        _hoveredItem = null;
        _snapHorizontalLines = Array.Empty<double>();
        _snapVerticalLines = Array.Empty<double>();
        _dropTargetBounds = null;
        _dropPosition = null;
        InvalidateVisual();
    }

    /// <summary>
    /// Hit-tests resize handles for the primary selection.
    /// </summary>
    public ResizeHandle? HitTestResizeHandles(Point point)
    {
        if (_selectedItems.Count == 0)
        {
            return null;
        }

        IDesignItem primary = _selectedItems[0];
        if (primary is DesignItem designItem)
        {
            return _resizeHandleAdorner.HitTest(designItem.Bounds, point);
        }

        return null;
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // 1. Hover outline
        _selectionAdorner.RenderHover(context, _hoveredItem);

        // 2. Selection rectangles
        _selectionAdorner.Render(context, _selectedItems);

        // 3. Resize handles on primary selection
        if (_selectedItems.Count > 0 && _selectedItems[0] is DesignItem primary)
        {
            _resizeHandleAdorner.Render(context, primary.Bounds);

            // 4. Margin/padding on primary selection
            if (_showMarginPadding)
            {
                Rect bounds = primary.Bounds;
                Thickness margin = TryParseThickness(primary.AstNode.GetPropertyValue("Margin"));
                Thickness padding = TryParseThickness(primary.AstNode.GetPropertyValue("Padding"));

                _marginPaddingAdorner.RenderMargin(context, bounds, margin);
                _marginPaddingAdorner.RenderPadding(context, bounds, padding);
            }
        }

        // 5. Snap lines
        if (_snapHorizontalLines.Count > 0 || _snapVerticalLines.Count > 0)
        {
            _snapLineAdorner.Render(context, _snapHorizontalLines, _snapVerticalLines, Bounds.Size);
        }

        // 6. Drop target indicator
        if (_dropTargetBounds.HasValue && _dropPosition.HasValue)
        {
            _dropTargetAdorner.Render(context, _dropTargetBounds.Value, _dropPosition.Value);
        }
    }

    private static Thickness TryParseThickness(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return default;
        }

        string[] parts = value.Split(',');
        return parts.Length switch
        {
            1 when double.TryParse(parts[0].Trim(), out double uniform) => new Thickness(uniform),
            2 when double.TryParse(parts[0].Trim(), out double h)
                && double.TryParse(parts[1].Trim(), out double v) => new Thickness(h, v, h, v),
            4 when double.TryParse(parts[0].Trim(), out double l)
                && double.TryParse(parts[1].Trim(), out double t)
                && double.TryParse(parts[2].Trim(), out double r)
                && double.TryParse(parts[3].Trim(), out double b) => new Thickness(l, t, r, b),
            _ => default
        };
    }
}
