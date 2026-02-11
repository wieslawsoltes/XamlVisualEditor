using System;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace XamlVisualEditor.CodeEditor;

/// <summary>
/// Highlights the range associated with hover information.
/// </summary>
public sealed class HoverRangeColorizer : DocumentColorizingTransformer
{
    private static readonly IBrush s_hoverBrush = new SolidColorBrush(Colors.CornflowerBlue)
    {
        Opacity = 0.2
    };

    private int? _startOffset;
    private int? _endOffset;

    public void UpdateRange(int startOffset, int endOffset)
    {
        if (startOffset < 0 || endOffset < 0)
        {
            Clear();
            return;
        }

        int start = Math.Min(startOffset, endOffset);
        int end = Math.Max(startOffset, endOffset);
        if (start == end)
        {
            end = start + 1;
        }

        _startOffset = start;
        _endOffset = end;
    }

    public void Clear()
    {
        _startOffset = null;
        _endOffset = null;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (_startOffset is null || _endOffset is null)
        {
            return;
        }

        int lineStart = line.Offset;
        int lineEnd = line.EndOffset;
        int start = Math.Clamp(_startOffset.Value, lineStart, lineEnd);
        int end = Math.Clamp(_endOffset.Value, lineStart, lineEnd);

        if (start >= end)
        {
            return;
        }

        ChangeLinePart(start, end, element =>
        {
            element.TextRunProperties.SetBackgroundBrush(s_hoverBrush);
        });
    }
}
