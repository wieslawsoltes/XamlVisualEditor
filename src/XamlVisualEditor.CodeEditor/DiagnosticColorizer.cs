using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using XamlVisualEditor.Core;

namespace XamlVisualEditor.CodeEditor;

/// <summary>
/// A document colorizing transformer that renders squiggly underlines for XAML diagnostics.
/// Errors are shown in red, warnings in yellow/orange.
/// </summary>
public sealed class DiagnosticColorizer : DocumentColorizingTransformer
{
    private IReadOnlyList<XamlDiagnostic> _diagnostics = Array.Empty<XamlDiagnostic>();

    // Cached TextDecorationCollections per severity to avoid per-render allocations
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

    /// <summary>
    /// Updates the diagnostics to display.
    /// </summary>
    public void UpdateDiagnostics(IReadOnlyList<XamlDiagnostic> diagnostics)
    {
        _diagnostics = diagnostics ?? Array.Empty<XamlDiagnostic>();
    }

    /// <summary>
    /// Gets the diagnostic at a given line and column, if any.
    /// </summary>
    public XamlDiagnostic? GetDiagnosticAt(int line, int column)
    {
        foreach (XamlDiagnostic diag in _diagnostics)
        {
            if (diag.Line != line)
            {
                continue;
            }

            int diagStart = Math.Max(1, diag.Column);
            int diagEnd = diag.Length > 0 ? diagStart + diag.Length : int.MaxValue;

            if (column >= diagStart && column < diagEnd)
            {
                return diag;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    protected override void ColorizeLine(DocumentLine line)
    {
        int lineNumber = line.LineNumber;
        int lineStart = line.Offset;
        int lineEnd = line.EndOffset;

        foreach (XamlDiagnostic diag in _diagnostics)
        {
            if (diag.Line != lineNumber)
            {
                continue;
            }

            // Calculate underline range
            int start = lineStart + Math.Max(0, diag.Column - 1);
            int end = diag.Length > 0
                ? Math.Min(lineEnd, start + diag.Length)
                : lineEnd; // Underline to end of line if no length

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
