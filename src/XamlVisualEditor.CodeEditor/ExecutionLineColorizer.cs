using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace XamlVisualEditor.CodeEditor;

public sealed class ExecutionLineColorizer : DocumentColorizingTransformer
{
    public int? LineNumber { get; set; }

    public IBrush BackgroundBrush { get; set; } = new SolidColorBrush(Color.Parse("#1F3A5F"));

    protected override void ColorizeLine(DocumentLine line)
    {
        if (LineNumber is null || LineNumber <= 0)
        {
            return;
        }

        if (line.LineNumber != LineNumber)
        {
            return;
        }

        ChangeLinePart(line.Offset, line.EndOffset, element =>
        {
            element.TextRunProperties.SetBackgroundBrush(BackgroundBrush);
        });
    }
}