using System;

namespace XamlVisualEditor.Designer.Core;

/// <summary>
/// Computes the design artboard size for a document root. The artboard clips its
/// content, so a document without an explicit size must grow with what it renders.
/// </summary>
public static class DesignCanvasSizing
{
    /// <summary>Default artboard width for documents that declare nothing.</summary>
    public const double DefaultWidth = 800;

    /// <summary>Default artboard height for documents that declare nothing.</summary>
    public const double DefaultHeight = 600;

    /// <summary>
    /// Extra artboard space beyond the document size, so border strokes on the
    /// document edge stay visible instead of ending on the clip boundary.
    /// </summary>
    public const double ArtboardPadding = 10;

    /// <summary>
    /// Resolves the artboard size: an explicit size (DesignWidth/DesignHeight or
    /// Width/Height) wins unchanged; otherwise the declared minimum (or the default)
    /// grows to the measured content size when one is available.
    /// </summary>
    public static (double Width, double Height) Compute(
        double? explicitWidth,
        double? explicitHeight,
        double? minWidth,
        double? minHeight,
        double? measuredWidth,
        double? measuredHeight)
    {
        double width = explicitWidth ?? minWidth ?? DefaultWidth;
        if (!explicitWidth.HasValue && measuredWidth is { } contentWidth && double.IsFinite(contentWidth))
        {
            width = Math.Max(width, contentWidth);
        }

        double height = explicitHeight ?? minHeight ?? DefaultHeight;
        if (!explicitHeight.HasValue && measuredHeight is { } contentHeight && double.IsFinite(contentHeight))
        {
            height = Math.Max(height, contentHeight);
        }

        return (width, height);
    }
}
