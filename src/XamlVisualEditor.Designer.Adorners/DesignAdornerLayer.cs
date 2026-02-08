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
    private static readonly IPen SelectionBoxPen = new Pen(Brushes.DodgerBlue, 1, DashStyle.Dash);
    private static readonly IBrush SelectionBoxFill = new SolidColorBrush(Color.FromArgb(30, 30, 144, 255));
    private readonly SelectionAdorner _selectionAdorner = new();
    private readonly ResizeHandleAdorner _resizeHandleAdorner = new();
    private readonly SnapLineAdorner _snapLineAdorner = new();
    private readonly GuideLineAdorner _guideLineAdorner = new();
    private readonly SpacingGuideAdorner _spacingGuideAdorner = new();
    private readonly MarginPaddingAdorner _marginPaddingAdorner = new();
    private readonly DropTargetAdorner _dropTargetAdorner = new();

    private IReadOnlyList<IDesignItem> _selectedItems = Array.Empty<IDesignItem>();
    private IDesignItem? _hoveredItem;
    private IReadOnlyList<double> _snapHorizontalLines = Array.Empty<double>();
    private IReadOnlyList<double> _snapVerticalLines = Array.Empty<double>();
    private IReadOnlyList<double> _guideHorizontalLines = Array.Empty<double>();
    private IReadOnlyList<double> _guideVerticalLines = Array.Empty<double>();
    private IReadOnlyList<SpacingGuide> _spacingGuides = Array.Empty<SpacingGuide>();
    private DropPosition? _dropPosition;
    private Rect? _dropTargetBounds;
    private bool _showMarginPadding;
    private bool _showAlignmentGuides = true;
    private bool _showGuides = true;
    private bool _showSpacingGuides = true;
    private Control? _surfaceRoot;
    private Rect? _selectionBox;

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

    /// <summary>
    /// Gets or sets whether alignment guides (snap lines) are visible.
    /// </summary>
    public bool ShowAlignmentGuides
    {
        get => _showAlignmentGuides;
        set
        {
            _showAlignmentGuides = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Gets or sets whether persistent guides are visible.
    /// </summary>
    public bool ShowGuides
    {
        get => _showGuides;
        set
        {
            _showGuides = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Gets or sets whether spacing guides are visible.
    /// </summary>
    public bool ShowSpacingGuides
    {
        get => _showSpacingGuides;
        set
        {
            _showSpacingGuides = value;
            InvalidateVisual();
        }
    }

    public DesignAdornerLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    /// <summary>
    /// Sets the surface root control used for coordinate translation.
    /// </summary>
    public void SetSurfaceRoot(Control? surfaceRoot)
    {
        _surfaceRoot = surfaceRoot;
        InvalidateVisual();
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
    /// Updates guide lines (ruler guides).
    /// </summary>
    public void UpdateGuideLines(IReadOnlyList<double> horizontalLines, IReadOnlyList<double> verticalLines)
    {
        _guideHorizontalLines = horizontalLines ?? Array.Empty<double>();
        _guideVerticalLines = verticalLines ?? Array.Empty<double>();
        InvalidateVisual();
    }

    /// <summary>
    /// Updates spacing guides.
    /// </summary>
    public void UpdateSpacingGuides(IReadOnlyList<SpacingGuide> guides)
    {
        _spacingGuides = guides ?? Array.Empty<SpacingGuide>();
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
    /// Updates the marquee selection rectangle.
    /// </summary>
    public void UpdateSelectionBox(Rect? selectionBox)
    {
        _selectionBox = selectionBox;
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
        _guideHorizontalLines = Array.Empty<double>();
        _guideVerticalLines = Array.Empty<double>();
        _spacingGuides = Array.Empty<SpacingGuide>();
        _dropTargetBounds = null;
        _dropPosition = null;
        _selectionBox = null;
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
            Rect bounds = _surfaceRoot is not null
                ? designItem.GetBoundsRelativeTo(_surfaceRoot)
                : designItem.Bounds;
            return _resizeHandleAdorner.HitTest(bounds, point);
        }

        return null;
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // 1. Hover outline
        _selectionAdorner.RenderHover(context, _hoveredItem, _surfaceRoot);

        // 2. Selection rectangles
        _selectionAdorner.Render(context, _selectedItems, _surfaceRoot);

        // 3. Resize handles on primary selection
        if (_selectedItems.Count > 0 && _selectedItems[0] is DesignItem primary)
        {
            Rect bounds = _surfaceRoot is not null
                ? primary.GetBoundsRelativeTo(_surfaceRoot)
                : primary.Bounds;
            _resizeHandleAdorner.Render(context, bounds);

            // 4. Margin/padding on primary selection
            if (_showMarginPadding)
            {
                Thickness margin = TryParseThickness(primary.AstNode.GetPropertyValue("Margin"));
                Thickness padding = TryParseThickness(primary.AstNode.GetPropertyValue("Padding"));

                _marginPaddingAdorner.RenderMargin(context, bounds, margin);
                _marginPaddingAdorner.RenderPadding(context, bounds, padding);
            }
        }

        // 5. Alignment snap lines
        if (_showAlignmentGuides && (_snapHorizontalLines.Count > 0 || _snapVerticalLines.Count > 0))
        {
            _snapLineAdorner.Render(context, _snapHorizontalLines, _snapVerticalLines, Bounds.Size);
        }

        // 6. Spacing guides
        if (_showSpacingGuides && _spacingGuides.Count > 0)
        {
            _spacingGuideAdorner.Render(context, _spacingGuides);
        }

        // 7. Persistent guides
        if (_showGuides && (_guideHorizontalLines.Count > 0 || _guideVerticalLines.Count > 0))
        {
            _guideLineAdorner.Render(context, _guideHorizontalLines, _guideVerticalLines, Bounds.Size);
        }

        // 8. Drop target indicator
        if (_dropTargetBounds.HasValue && _dropPosition.HasValue)
        {
            _dropTargetAdorner.Render(context, _dropTargetBounds.Value, _dropPosition.Value);
        }

        // 9. Marquee selection box
        if (_selectionBox.HasValue)
        {
            Rect box = _selectionBox.Value;
            context.DrawRectangle(SelectionBoxFill, SelectionBoxPen, box);
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
