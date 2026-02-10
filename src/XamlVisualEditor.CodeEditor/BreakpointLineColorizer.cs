using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace XamlVisualEditor.CodeEditor;

public sealed class BreakpointLineColorizer : DocumentColorizingTransformer
{
    private HashSet<int> _lines = new();

    public IBrush BackgroundBrush { get; set; } = new SolidColorBrush(Color.Parse("#2A1F1F"));

    public void UpdateLines(IEnumerable<int> lines)
    {
        _lines = lines is HashSet<int> set
            ? new HashSet<int>(set)
            : new HashSet<int>(lines);
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (_lines.Count == 0)
        {
            return;
        }

        if (!_lines.Contains(line.LineNumber))
        {
            return;
        }

        ChangeLinePart(line.Offset, line.EndOffset, element =>
        {
            element.TextRunProperties.SetBackgroundBrush(BackgroundBrush);
        });
    }
}
