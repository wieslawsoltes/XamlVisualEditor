using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using XamlVisualEditor.Collaboration.UI;

namespace XamlVisualEditor.CodeEditor;

// ==============================================
// 8.2.3 — Colored Cursors per Participant
// ==============================================

/// <summary>
/// Renders colored caret indicators for remote collaboration participants
/// in the code editor.
/// </summary>
public sealed class CollaborationCaretRenderer : DocumentColorizingTransformer
{
    private readonly List<RemoteCaretInfo> _carets = new();
    private readonly Dictionary<Color, ISolidColorBrush> _brushCache = new();

    /// <summary>
    /// Updates the remote participant caret positions.
    /// </summary>
    public void UpdateCarets(IEnumerable<ParticipantViewModel> participants)
    {
        _carets.Clear();

        foreach (ParticipantViewModel p in participants)
        {
            if (p.IsLocal)
            {
                continue;
            }

            _carets.Add(new RemoteCaretInfo
            {
                ParticipantId = p.Id,
                DisplayName = p.DisplayName,
                Line = p.CaretLine,
                Column = p.CaretColumn,
                Color = ParseColor(p.Color)
            });
        }
    }

    /// <inheritdoc/>
    protected override void ColorizeLine(DocumentLine line)
    {
        int lineNumber = line.LineNumber;
        int lineStart = line.Offset;

        foreach (RemoteCaretInfo caret in _carets)
        {
            if (caret.Line != lineNumber)
            {
                continue;
            }

            // Highlight the character at the caret position with a colored background
            int caretOffset = lineStart + Math.Max(0, caret.Column - 1);
            int endOffset = Math.Min(caretOffset + 1, line.EndOffset);

            if (caretOffset >= lineStart && endOffset <= line.EndOffset && caretOffset < endOffset)
            {
                ChangeLinePart(caretOffset, endOffset, element =>
                {
                    element.TextRunProperties.SetBackgroundBrush(GetOrCreateBrush(caret.Color, 0.5));
                });
            }
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

    private ISolidColorBrush GetOrCreateBrush(Color color, double opacity)
    {
        Color key = Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B);
        if (!_brushCache.TryGetValue(key, out ISolidColorBrush? brush))
        {
            brush = new SolidColorBrush(color) { Opacity = opacity };
            _brushCache[key] = brush;
        }
        return brush;
    }

    private sealed class RemoteCaretInfo
    {
        public string ParticipantId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int Line { get; init; }
        public int Column { get; init; }
        public Color Color { get; init; }
    }
}

// ==============================================
// 8.2.4 — Selection Highlighting per Participant
// ==============================================

/// <summary>
/// Renders colored selection highlights for remote collaboration participants
/// in the code editor.
/// </summary>
public sealed class CollaborationSelectionRenderer : DocumentColorizingTransformer
{
    private readonly List<RemoteSelectionInfo> _selections = new();
    private readonly Dictionary<Color, ISolidColorBrush> _brushCache = new();

    /// <summary>
    /// Updates the remote participant selections.
    /// </summary>
    public void UpdateSelections(IEnumerable<RemoteSelectionData> selections)
    {
        _selections.Clear();

        foreach (RemoteSelectionData sel in selections)
        {
            _selections.Add(new RemoteSelectionInfo
            {
                ParticipantId = sel.ParticipantId,
                StartLine = sel.StartLine,
                StartColumn = sel.StartColumn,
                EndLine = sel.EndLine,
                EndColumn = sel.EndColumn,
                Color = ParseColor(sel.Color)
            });
        }
    }

    /// <inheritdoc/>
    protected override void ColorizeLine(DocumentLine line)
    {
        int lineNumber = line.LineNumber;
        int lineStart = line.Offset;
        int lineEnd = line.EndOffset;

        foreach (RemoteSelectionInfo sel in _selections)
        {
            // Check if this line is within the selection range
            if (lineNumber < sel.StartLine || lineNumber > sel.EndLine)
            {
                continue;
            }

            int start = lineStart;
            int end = lineEnd;

            if (lineNumber == sel.StartLine)
            {
                start = lineStart + Math.Max(0, sel.StartColumn - 1);
            }

            if (lineNumber == sel.EndLine)
            {
                end = lineStart + Math.Max(0, sel.EndColumn - 1);
            }

            start = Math.Max(start, lineStart);
            end = Math.Min(end, lineEnd);

            if (start < end)
            {
                ChangeLinePart(start, end, element =>
                {
                    element.TextRunProperties.SetBackgroundBrush(GetOrCreateBrush(sel.Color, 0.25));
                });
            }
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

    private ISolidColorBrush GetOrCreateBrush(Color color, double opacity)
    {
        Color key = Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B);
        if (!_brushCache.TryGetValue(key, out ISolidColorBrush? brush))
        {
            brush = new SolidColorBrush(color) { Opacity = opacity };
            _brushCache[key] = brush;
        }
        return brush;
    }

    private sealed class RemoteSelectionInfo
    {
        public string ParticipantId { get; init; } = string.Empty;
        public int StartLine { get; init; }
        public int StartColumn { get; init; }
        public int EndLine { get; init; }
        public int EndColumn { get; init; }
        public Color Color { get; init; }
    }
}

/// <summary>
/// Data describing a remote participant's text selection.
/// </summary>
public sealed class RemoteSelectionData
{
    public string ParticipantId { get; init; } = string.Empty;
    public int StartLine { get; init; }
    public int StartColumn { get; init; }
    public int EndLine { get; init; }
    public int EndColumn { get; init; }
    public string Color { get; init; } = "#0078D4";
}
