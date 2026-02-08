using System;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using XamlVisualEditor.Core;

namespace XamlVisualEditor.CodeEditor;

/// <summary>
/// Renders squiggly underlines for language diagnostics.
/// </summary>
public sealed class LanguageDiagnosticColorizer : DocumentColorizingTransformer
{
    private IReadOnlyList<LanguageDiagnostic> _diagnostics = Array.Empty<LanguageDiagnostic>();

    private static readonly TextDecorationCollection s_errorDecoration = CreateUnderline(Brushes.Red);
    private static readonly TextDecorationCollection s_warningDecoration = CreateUnderline(Brushes.Orange);
    private static readonly TextDecorationCollection s_infoDecoration = CreateUnderline(Brushes.CornflowerBlue);

    private static TextDecorationCollection CreateUnderline(IBrush brush)
    {
        TextDecorationCollection col = new()
        {
            new TextDecoration
            {
                Location = TextDecorationLocation.Underline,
                Stroke = brush,
                StrokeThicknessUnit = TextDecorationUnit.Pixel,
                StrokeThickness = 2
            }
        };
        return col;
    }

    public void UpdateDiagnostics(IReadOnlyList<LanguageDiagnostic> diagnostics)
    {
        _diagnostics = diagnostics ?? Array.Empty<LanguageDiagnostic>();
    }

    public LanguageDiagnostic? GetDiagnosticAt(int line, int column)
    {
        foreach (LanguageDiagnostic diag in _diagnostics)
        {
            if (line < diag.Range.Start.Line || line > diag.Range.End.Line)
            {
                continue;
            }

            int startColumn = line == diag.Range.Start.Line ? diag.Range.Start.Column : 1;
            int endColumn = line == diag.Range.End.Line ? diag.Range.End.Column : int.MaxValue;

            if (column >= startColumn && column <= endColumn)
            {
                return diag;
            }
        }

        return null;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        int lineNumber = line.LineNumber;
        int lineStart = line.Offset;
        int lineEnd = line.EndOffset;

        foreach (LanguageDiagnostic diag in _diagnostics)
        {
            if (lineNumber < diag.Range.Start.Line || lineNumber > diag.Range.End.Line)
            {
                continue;
            }

            int startColumn = lineNumber == diag.Range.Start.Line ? diag.Range.Start.Column : 1;
            int endColumn = lineNumber == diag.Range.End.Line ? diag.Range.End.Column : line.Length + 1;

            int start = lineStart + Math.Max(0, startColumn - 1);
            int end = lineStart + Math.Max(0, endColumn - 1);
            end = Math.Min(end, lineEnd);

            if (start >= end || start < lineStart || end > lineEnd)
            {
                continue;
            }

            ChangeLinePart(start, end, element =>
            {
                TextDecorationCollection decoration = diag.Severity switch
                {
                    DiagnosticSeverity.Error => s_errorDecoration,
                    DiagnosticSeverity.Warning => s_warningDecoration,
                    _ => s_infoDecoration
                };

                element.TextRunProperties.SetTextDecorations(decoration);
            });
        }
    }
}
