using System.Linq;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Xaml.Intellisense;
using XamlVisualEditor.Xaml.Parsing;

namespace XamlVisualEditor.Xaml.Language;

/// <summary>
/// XAML language service built on the existing intellisense providers and parser.
/// </summary>
public sealed class XamlLanguageService : ILanguageIntellisenseService
{
    private readonly CompletionProviderRegistry _completionRegistry;
    private readonly IXamlParsingService _parser;
    private readonly ITypeMetadataService _metadataService;

    public XamlLanguageService(
        CompletionProviderRegistry completionRegistry,
        IXamlParsingService parser,
        ITypeMetadataService metadataService)
    {
        _completionRegistry = completionRegistry;
        _parser = parser;
        _metadataService = metadataService;
    }

    public string LanguageId => "xml";

    public bool CanHandle(string filePath, string? languageId)
    {
        if (string.Equals(languageId, "xml", StringComparison.OrdinalIgnoreCase))
        {
            return filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase);
        }

        return filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase);
    }

    public Task InitializeWorkspaceAsync(string workspacePath, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task ClearWorkspaceAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken ct = default)
    {
        CompletionContext enriched = new()
        {
            Offset = context.Offset,
            TextBefore = context.TextBefore,
            DocumentText = context.DocumentText,
            FilePath = context.FilePath,
            LanguageId = context.LanguageId,
            Trigger = context.Trigger,
            TriggerCharacter = context.TriggerCharacter,
            Metadata = _metadataService
        };

        IReadOnlyList<CompletionItem> items = _completionRegistry.GetCompletions(enriched);
        return Task.FromResult(items);
    }

    public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default)
    {
        ParseResult result = _parser.Parse(context.Text, new XamlParserOptions { UseTolerantParser = true });
        List<LanguageDiagnostic> diagnostics = new(result.Diagnostics.Count);

        foreach (XamlDiagnostic diagnostic in result.Diagnostics)
        {
            LanguageTextPosition start = new(diagnostic.Line, diagnostic.Column);
            LanguageTextPosition end = new(diagnostic.Line, diagnostic.Column + Math.Max(1, diagnostic.Length));

            diagnostics.Add(new LanguageDiagnostic
            {
                FilePath = context.FilePath,
                Message = diagnostic.Message,
                Severity = diagnostic.Severity,
                Range = new LanguageTextRange(start, end),
                Source = "XAML"
            });
        }

        return Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(diagnostics);
    }

    public Task<LanguageHover?> GetHoverAsync(LanguagePositionContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context.Text))
        {
            return Task.FromResult<LanguageHover?>(null);
        }

        string token = ExtractTokenAt(context.Text, context.Offset);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult<LanguageHover?>(null);
        }

        TypeMetadata? type = _metadataService.GetType("https://github.com/avaloniaui", token);
        if (type is not null)
        {
            return Task.FromResult<LanguageHover?>(new LanguageHover
            {
                Contents = type.FullName
            });
        }

        return Task.FromResult<LanguageHover?>(null);
    }

    public Task<IReadOnlyList<LanguageLocation>> FindDefinitionsAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<LanguageLocation>>(Array.Empty<LanguageLocation>());
    }

    public Task<IReadOnlyList<LanguageLocation>> FindReferencesAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<LanguageLocation>>(Array.Empty<LanguageLocation>());
    }

    public Task<LanguageRenameInfo?> PrepareRenameAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context.Text))
        {
            return Task.FromResult<LanguageRenameInfo?>(null);
        }

        if (!TryGetRenameTarget(context.Text, context.Offset, out RenameTarget target))
        {
            return Task.FromResult<LanguageRenameInfo?>(null);
        }

        LanguageTextPosition start = OffsetToPosition(context.Text, target.ValueStart);
        LanguageTextPosition end = OffsetToPosition(context.Text, target.ValueEnd);
        return Task.FromResult<LanguageRenameInfo?>(new LanguageRenameInfo
        {
            Name = target.Value,
            Range = new LanguageTextRange(start, end)
        });
    }

    public Task<LanguageWorkspaceEdit?> RenameSymbolAsync(
        LanguageRenameContext context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context.Text) || string.IsNullOrWhiteSpace(context.NewName))
        {
            return Task.FromResult<LanguageWorkspaceEdit?>(null);
        }

        if (!TryGetRenameTarget(context.Text, context.Offset, out RenameTarget target))
        {
            return Task.FromResult<LanguageWorkspaceEdit?>(null);
        }

        List<TextEdit> edits = new();
        HashSet<(int Offset, int Length)> seen = new();

        switch (target.Kind)
        {
            case RenameTargetKind.ElementName:
                AddAttributeEdits(context.Text, "x:Name", target.Value, context.NewName, edits, seen);
                AddAttributeEdits(context.Text, "Name", target.Value, context.NewName, edits, seen);
                AddAttributeEdits(context.Text, "ElementName", target.Value, context.NewName, edits, seen);
                AddAttributeEdits(context.Text, "TargetName", target.Value, context.NewName, edits, seen);
                break;
            case RenameTargetKind.ResourceKey:
                AddAttributeEdits(context.Text, "x:Key", target.Value, context.NewName, edits, seen);
                AddAttributeEdits(context.Text, "Key", target.Value, context.NewName, edits, seen);
                AddMarkupExtensionEdits(context.Text, "StaticResource", target.Value, context.NewName, edits, seen);
                AddMarkupExtensionEdits(context.Text, "DynamicResource", target.Value, context.NewName, edits, seen);
                break;
            case RenameTargetKind.EventHandler:
                AddAttributeEdits(context.Text, target.AttributeName, target.Value, context.NewName, edits, seen);
                break;
            default:
                break;
        }

        if (edits.Count == 0)
        {
            return Task.FromResult<LanguageWorkspaceEdit?>(null);
        }

        LanguageDocumentEdit docEdit = new()
        {
            FilePath = context.FilePath,
            Edits = edits
        };

        return Task.FromResult<LanguageWorkspaceEdit?>(new LanguageWorkspaceEdit
        {
            DocumentEdits = new[] { docEdit }
        });
    }

    public Task<LanguageSignatureHelp?> GetSignatureHelpAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        return Task.FromResult<LanguageSignatureHelp?>(null);
    }

    private static string ExtractTokenAt(string text, int offset)
    {
        if (offset < 0 || offset > text.Length)
        {
            return string.Empty;
        }

        int start = offset;
        while (start > 0 && IsTokenChar(text[start - 1]))
        {
            start--;
        }

        int end = offset;
        while (end < text.Length && IsTokenChar(text[end]))
        {
            end++;
        }

        return end > start ? text.Substring(start, end - start) : string.Empty;
    }

    private static bool IsTokenChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '.' || c == ':' || c == '_';
    }

    private static bool TryGetRenameTarget(string text, int offset, out RenameTarget target)
    {
        target = default;
        if (!TryGetAttributeValueSpan(text, offset, out string attributeName, out string value, out int start, out int end))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (attributeName.Equals("x:Name", StringComparison.Ordinal) ||
            attributeName.Equals("Name", StringComparison.Ordinal))
        {
            target = new RenameTarget(attributeName, value, start, end, RenameTargetKind.ElementName);
            return true;
        }

        if (attributeName.Equals("x:Key", StringComparison.Ordinal) ||
            attributeName.Equals("Key", StringComparison.Ordinal))
        {
            target = new RenameTarget(attributeName, value, start, end, RenameTargetKind.ResourceKey);
            return true;
        }

        if (IsEventAttributeName(attributeName) && IsIdentifier(value))
        {
            target = new RenameTarget(attributeName, value, start, end, RenameTargetKind.EventHandler);
            return true;
        }

        return false;
    }

    private static bool TryGetAttributeValueSpan(
        string text,
        int offset,
        out string attributeName,
        out string value,
        out int valueStart,
        out int valueEnd)
    {
        attributeName = string.Empty;
        value = string.Empty;
        valueStart = 0;
        valueEnd = 0;

        if (offset < 0 || offset > text.Length)
        {
            return false;
        }

        int quoteStart = FindQuoteStart(text, offset, out char quote);
        if (quoteStart < 0)
        {
            return false;
        }

        int quoteEnd = text.IndexOf(quote, quoteStart + 1);
        if (quoteEnd <= quoteStart)
        {
            return false;
        }

        if (offset < quoteStart || offset > quoteEnd)
        {
            return false;
        }

        if (!TryGetAttributeNameBefore(text, quoteStart, out attributeName))
        {
            return false;
        }

        valueStart = quoteStart + 1;
        valueEnd = quoteEnd;
        value = valueEnd > valueStart ? text.Substring(valueStart, valueEnd - valueStart) : string.Empty;
        return true;
    }

    private static int FindQuoteStart(string text, int offset, out char quote)
    {
        quote = '\0';
        for (int i = Math.Min(offset, text.Length - 1); i >= 0; i--)
        {
            char c = text[i];
            if (c == '"' || c == '\'')
            {
                quote = c;
                return i;
            }
        }

        return -1;
    }

    private static bool TryGetAttributeNameBefore(string text, int quoteStart, out string attributeName)
    {
        attributeName = string.Empty;
        int index = quoteStart - 1;
        while (index >= 0 && char.IsWhiteSpace(text[index]))
        {
            index--;
        }

        if (index < 0 || text[index] != '=')
        {
            return false;
        }

        index--;
        while (index >= 0 && char.IsWhiteSpace(text[index]))
        {
            index--;
        }

        int end = index;
        while (index >= 0 && IsAttributeNameChar(text[index]))
        {
            index--;
        }

        int start = index + 1;
        if (end < start)
        {
            return false;
        }

        attributeName = text.Substring(start, end - start + 1);
        return !string.IsNullOrWhiteSpace(attributeName);
    }

    private static bool IsAttributeNameChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == ':' || c == '.' || c == '_';
    }

    private static bool IsIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!(char.IsLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        return value.All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    private static bool IsEventAttributeName(string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return false;
        }

        if (attributeName.Contains(':', StringComparison.Ordinal) || attributeName.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        if (!char.IsUpper(attributeName[0]))
        {
            return false;
        }

        return !IsDirectiveAttribute(attributeName);
    }

    private static bool IsDirectiveAttribute(string attributeName)
    {
        return attributeName is "Name" or "Key" or "Class" or "DataContext" or "Styles";
    }

    private static void AddAttributeEdits(
        string text,
        string attributeName,
        string oldValue,
        string newValue,
        List<TextEdit> edits,
        HashSet<(int Offset, int Length)> seen)
    {
        AddAttributeEditsForQuote(text, attributeName, oldValue, newValue, '"', edits, seen);
        AddAttributeEditsForQuote(text, attributeName, oldValue, newValue, '\'', edits, seen);
    }

    private static void AddAttributeEditsForQuote(
        string text,
        string attributeName,
        string oldValue,
        string newValue,
        char quote,
        List<TextEdit> edits,
        HashSet<(int Offset, int Length)> seen)
    {
        string marker = attributeName + "=" + quote;
        int index = 0;
        while (index < text.Length)
        {
            int attrIndex = text.IndexOf(marker, index, StringComparison.Ordinal);
            if (attrIndex < 0)
            {
                break;
            }

            int start = attrIndex + marker.Length;
            int end = text.IndexOf(quote, start);
            if (end <= start)
            {
                break;
            }

            string value = text.Substring(start, end - start);
            if (string.Equals(value, oldValue, StringComparison.Ordinal))
            {
                AddEdit(edits, seen, start, end - start, newValue);
            }

            index = end + 1;
        }
    }

    private static void AddMarkupExtensionEdits(
        string text,
        string extensionName,
        string oldValue,
        string newValue,
        List<TextEdit> edits,
        HashSet<(int Offset, int Length)> seen)
    {
        string marker = "{" + extensionName;
        int index = 0;
        while (index < text.Length)
        {
            int startIndex = text.IndexOf(marker, index, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                break;
            }

            int contentStart = startIndex + marker.Length;
            int endBrace = text.IndexOf('}', contentStart);
            if (endBrace < 0)
            {
                break;
            }

            string rawContent = text.Substring(contentStart, endBrace - contentStart);
            int leadingWhitespace = rawContent.Length - rawContent.TrimStart().Length;
            string content = rawContent.TrimStart();
            if (TryGetMarkupExtensionToken(content, oldValue, out int tokenOffset, out int tokenLength))
            {
                int absoluteOffset = contentStart + leadingWhitespace + tokenOffset;
                AddEdit(edits, seen, absoluteOffset, tokenLength, newValue);
            }

            index = endBrace + 1;
        }
    }

    private static bool TryGetMarkupExtensionToken(string content, string oldValue, out int offset, out int length)
    {
        offset = 0;
        length = 0;

        if (content.StartsWith(oldValue, StringComparison.Ordinal))
        {
            offset = 0;
            length = oldValue.Length;
            return true;
        }

        string[] keys = new[] { "ResourceKey=", "Key=" };
        foreach (string key in keys)
        {
            int keyIndex = content.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                continue;
            }

            int valueStart = keyIndex + key.Length;
            int valueEnd = FindTokenEnd(content, valueStart);
            if (valueEnd <= valueStart)
            {
                continue;
            }

            string value = content.Substring(valueStart, valueEnd - valueStart);
            if (string.Equals(value, oldValue, StringComparison.Ordinal))
            {
                offset = valueStart;
                length = value.Length;
                return true;
            }
        }

        return false;
    }

    private static int FindTokenEnd(string text, int start)
    {
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c) || c == ',' || c == '}' || c == '\'' || c == '"')
            {
                return i;
            }
        }

        return text.Length;
    }

    private static void AddEdit(
        List<TextEdit> edits,
        HashSet<(int Offset, int Length)> seen,
        int offset,
        int length,
        string newValue)
    {
        if (offset < 0 || length < 0)
        {
            return;
        }

        if (seen.Add((offset, length)))
        {
            edits.Add(new TextEdit
            {
                Offset = offset,
                Length = length,
                NewText = newValue
            });
        }
    }

    private static LanguageTextPosition OffsetToPosition(string text, int offset)
    {
        int line = 1;
        int column = 1;
        int max = Math.Clamp(offset, 0, text.Length);
        for (int i = 0; i < max; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return new LanguageTextPosition(line, column);
    }

    private readonly record struct RenameTarget(
        string AttributeName,
        string Value,
        int ValueStart,
        int ValueEnd,
        RenameTargetKind Kind);

    private enum RenameTargetKind
    {
        None,
        ElementName,
        ResourceKey,
        EventHandler
    }
}
