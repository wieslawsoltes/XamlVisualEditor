using System.Collections.Generic;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace XamlVisualEditor.CodeEditor;

/// <summary>
/// Folding strategy for XAML/XML documents.
/// Creates foldable regions for XML elements.
/// </summary>
public sealed class XmlFoldingStrategy
{
    /// <summary>
    /// Create folds for the given document.
    /// </summary>
    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document)
    {
        List<NewFolding> foldings = new();
        Stack<(int Offset, string Name)> stack = new();

        string text = document.Text;
        int i = 0;

        while (i < text.Length)
        {
            if (text[i] == '<')
            {
                if (i + 1 < text.Length && text[i + 1] == '/')
                {
                    // Closing tag
                    int nameStart = i + 2;
                    int nameEnd = text.IndexOf('>', nameStart);
                    if (nameEnd > nameStart)
                    {
                        string closingTag = text.Substring(nameStart, nameEnd - nameStart).Trim();
                        int closeTagEnd = nameEnd + 1;

                        if (stack.Count > 0 && stack.Peek().Name == closingTag)
                        {
                            var (openOffset, name) = stack.Pop();
                            if (closeTagEnd - openOffset > 1)
                            {
                                foldings.Add(new NewFolding(openOffset, closeTagEnd)
                                {
                                    Name = $"<{name}> ..."
                                });
                            }
                        }

                        i = closeTagEnd;
                        continue;
                    }
                }
                else if (i + 1 < text.Length && text[i + 1] == '!')
                {
                    // Comment or CDATA
                    if (i + 3 < text.Length && text[i + 2] == '-' && text[i + 3] == '-')
                    {
                        int commentEnd = text.IndexOf("-->", i + 4);
                        if (commentEnd >= 0)
                        {
                            int endPos = commentEnd + 3;
                            foldings.Add(new NewFolding(i, endPos) { Name = "<!-- ... -->" });
                            i = endPos;
                            continue;
                        }
                    }

                    i++;
                    continue;
                }
                else if (i + 1 < text.Length && text[i + 1] == '?')
                {
                    // Processing instruction
                    i++;
                    continue;
                }
                else
                {
                    // Opening tag
                    int tagStart = i;
                    int nameStart = i + 1;
                    int nameEnd = nameStart;

                    while (nameEnd < text.Length && !char.IsWhiteSpace(text[nameEnd])
                           && text[nameEnd] != '>' && text[nameEnd] != '/')
                    {
                        nameEnd++;
                    }

                    string tagName = text.Substring(nameStart, nameEnd - nameStart);

                    // Find the end of the opening tag
                    int tagEnd = text.IndexOf('>', nameEnd);
                    if (tagEnd >= 0)
                    {
                        bool selfClosing = tagEnd > 0 && text[tagEnd - 1] == '/';

                        if (!selfClosing && !string.IsNullOrWhiteSpace(tagName))
                        {
                            stack.Push((tagStart, tagName));
                        }

                        i = tagEnd + 1;
                        continue;
                    }
                }
            }

            i++;
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }
}
