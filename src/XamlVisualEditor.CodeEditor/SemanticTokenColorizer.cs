using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using XamlVisualEditor.Core;

namespace XamlVisualEditor.CodeEditor;

/// <summary>
/// Renders semantic token colors in the editor.
/// </summary>
public sealed class SemanticTokenColorizer : DocumentColorizingTransformer
{
    private readonly Dictionary<string, IBrush> _brushes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["namespace"] = Brushes.SteelBlue,
        ["type"] = Brushes.DodgerBlue,
        ["class"] = Brushes.DodgerBlue,
        ["enum"] = Brushes.MediumSeaGreen,
        ["interface"] = Brushes.LightSeaGreen,
        ["struct"] = Brushes.SteelBlue,
        ["typeParameter"] = Brushes.SteelBlue,
        ["parameter"] = Brushes.SandyBrown,
        ["variable"] = Brushes.LightSeaGreen,
        ["property"] = Brushes.Orange,
        ["enumMember"] = Brushes.MediumSeaGreen,
        ["event"] = Brushes.DarkKhaki,
        ["function"] = Brushes.MediumPurple,
        ["method"] = Brushes.MediumPurple,
        ["macro"] = Brushes.DarkOrange,
        ["keyword"] = Brushes.MediumVioletRed,
        ["modifier"] = Brushes.MediumVioletRed,
        ["comment"] = Brushes.Gray,
        ["string"] = Brushes.SeaGreen,
        ["number"] = Brushes.OrangeRed,
        ["regexp"] = Brushes.OrangeRed,
        ["operator"] = Brushes.DimGray
    };

    private IReadOnlyList<LanguageSemanticToken> _tokens = Array.Empty<LanguageSemanticToken>();

    public void UpdateTokens(IReadOnlyList<LanguageSemanticToken> tokens)
    {
        _tokens = tokens ?? Array.Empty<LanguageSemanticToken>();
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (_tokens.Count == 0)
        {
            return;
        }

        int lineNumber = line.LineNumber;
        int lineStart = line.Offset;
        int lineEnd = line.EndOffset;

        foreach (LanguageSemanticToken token in _tokens)
        {
            if (lineNumber < token.Range.Start.Line || lineNumber > token.Range.End.Line)
            {
                continue;
            }

            int startColumn = lineNumber == token.Range.Start.Line ? token.Range.Start.Column : 1;
            int endColumn = lineNumber == token.Range.End.Line ? token.Range.End.Column : line.Length + 1;

            int start = lineStart + Math.Max(0, startColumn - 1);
            int end = lineStart + Math.Max(0, endColumn - 1);
            end = Math.Min(end, lineEnd);

            if (start >= end || start < lineStart || end > lineEnd)
            {
                continue;
            }

            if (!_brushes.TryGetValue(token.Type, out IBrush? brush))
            {
                continue;
            }

            ChangeLinePart(start, end, element =>
            {
                element.TextRunProperties.SetForegroundBrush(brush);
            });
        }
    }
}
