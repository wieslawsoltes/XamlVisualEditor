using System.Text;
using System.Xml;
using System.Xml.Linq;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.Xaml.Parsing;

/// <summary>
/// XAML parsing service that parses XAML text into a mutable AST document.
/// Uses System.Xml.Linq for parsing with error recovery.
/// </summary>
public sealed class XamlParsingService : IXamlParsingService
{
    /// <inheritdoc />
    public ParseResult Parse(string xamlText, XamlParserOptions? options = null)
    {
        List<XamlDiagnostic> diagnostics = new();

        if (string.IsNullOrWhiteSpace(xamlText))
        {
            return new ParseResult
            {
                Document = null,
                Diagnostics = diagnostics
            };
        }

        try
        {
            XDocument xDoc = XDocument.Parse(xamlText, LoadOptions.SetLineInfo);

            if (xDoc.Root is null)
            {
                diagnostics.Add(new XamlDiagnostic
                {
                    Severity = DiagnosticSeverity.Error,
                    Message = "XAML document has no root element.",
                    Line = 1,
                    Column = 1,
                    Length = 0
                });

                return new ParseResult
                {
                    Document = null,
                    Diagnostics = diagnostics
                };
            }

            MutableAstDocument document = new();
            MutableAstObjectNode root = ConvertElement(xDoc.Root, document, diagnostics);
            document.Root = root;
            document.NodeMap.RegisterTree(root);

            // Extract namespace declarations from root
            foreach (XAttribute attr in xDoc.Root.Attributes())
            {
                if (attr.IsNamespaceDeclaration)
                {
                    string prefix = attr.Name.LocalName == "xmlns"
                        ? ""
                        : attr.Name.LocalName;
                    document.NamespaceAliases[prefix] = attr.Value;
                }
            }

            return new ParseResult
            {
                Document = document,
                Diagnostics = diagnostics
            };
        }
        catch (System.Xml.XmlException ex)
        {
            diagnostics.Add(new XamlDiagnostic
            {
                Severity = DiagnosticSeverity.Error,
                Message = ex.Message,
                Line = ex.LineNumber,
                Column = ex.LinePosition,
                Length = 1
            });

            // Attempt partial parse for tolerant mode
            if (options?.UseTolerantParser == true)
            {
                MutableAstDocument? partial = TryTolerantParse(xamlText, diagnostics);
                return new ParseResult
                {
                    Document = partial,
                    Diagnostics = diagnostics
                };
            }

            return new ParseResult
            {
                Document = null,
                Diagnostics = diagnostics
            };
        }
    }

    private static MutableAstObjectNode ConvertElement(
        XElement element,
        MutableAstDocument document,
        List<XamlDiagnostic> diagnostics)
    {
        RegisterNamespaceAliases(element, document);

        MutableAstObjectNode node = new()
        {
            TypeName = element.Name.LocalName,
            XmlNamespace = element.Name.NamespaceName
        };

        if (element is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
        {
            node.Line = lineInfo.LineNumber;
            node.Column = lineInfo.LinePosition;
        }

        // Convert attributes to properties
        foreach (XAttribute attr in element.Attributes())
        {
            if (attr.IsNamespaceDeclaration)
            {
                continue;
            }

            string propName = attr.Name.LocalName;
            if (!string.IsNullOrEmpty(attr.Name.NamespaceName))
            {
                // Attached property or directive — find prefix
                string? prefix = element.GetPrefixOfNamespace(attr.Name.Namespace);
                if (string.IsNullOrEmpty(prefix))
                {
                    prefix = FindPrefixForNamespace(document, attr.Name.NamespaceName);
                }

                if (!string.IsNullOrEmpty(prefix))
                {
                    propName = $"{prefix}:{attr.Name.LocalName}";
                }
            }

            MutableAstPropertyNode propNode = new()
            {
                PropertyName = propName,
                Value = new MutableAstTextNode { Text = attr.Value }
            };

            if (attr is IXmlLineInfo attrLineInfo && attrLineInfo.HasLineInfo())
            {
                propNode.Line = attrLineInfo.LineNumber;
                propNode.Column = attrLineInfo.LinePosition;
            }

            node.Properties.Add(propNode);
        }

        // Convert child elements
        foreach (XNode child in element.Nodes())
        {
            switch (child)
            {
                case XElement childElement:
                    // Check if this is a property element (TypeName.PropertyName)
                    if (childElement.Name.LocalName.Contains('.'))
                    {
                        ConvertPropertyElement(childElement, node, document, diagnostics);
                    }
                    else
                    {
                        MutableAstObjectNode childNode = ConvertElement(childElement, document, diagnostics);
                        node.Children.Add(childNode);
                    }
                    break;

                case XText textNode when !string.IsNullOrWhiteSpace(textNode.Value):
                    MutableAstTextNode textAstNode = new()
                    {
                        Text = textNode.Value.Trim()
                    };
                    if (textNode is IXmlLineInfo textLineInfo && textLineInfo.HasLineInfo())
                    {
                        textAstNode.Line = textLineInfo.LineNumber;
                        textAstNode.Column = textLineInfo.LinePosition;
                    }
                    node.Children.Add(textAstNode);
                    break;
            }
        }

        // Estimate EndLine from the last XNode or by counting newlines in element.ToString().
        // The last child node's line info gives us a lower bound, then add 1 for the closing tag.
        XNode? lastNode = element.LastNode;
        if (lastNode is IXmlLineInfo lastLineInfo && lastLineInfo.HasLineInfo())
        {
            // Closing tag is on the line after the last child (or same line for compact elements).
            // Use a rough heuristic: count newlines from last child to end.
            node.EndLine = lastLineInfo.LineNumber + 1;
        }
        else if (node.Line > 0)
        {
            // Self-closing or no children with line info; count newlines in the element text.
            int newlines = 0;
            string text = element.ToString();
            foreach (char c in text)
            {
                if (c == '\n') newlines++;
            }
            node.EndLine = node.Line + newlines;
        }

        return node;
    }

    private static void ConvertPropertyElement(
        XElement element,
        MutableAstObjectNode parentNode,
        MutableAstDocument document,
        List<XamlDiagnostic> diagnostics)
    {
        string propName = element.Name.LocalName;

        MutableAstPropertyNode propNode = new()
        {
            PropertyName = propName
        };

        if (element is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
        {
            propNode.Line = lineInfo.LineNumber;
            propNode.Column = lineInfo.LinePosition;
        }

        // If single child element, use as value; otherwise create container
        List<XElement> childElements = element.Elements().ToList();
        if (childElements.Count == 1)
        {
            propNode.Value = ConvertElement(childElements[0], document, diagnostics);
        }
        else if (childElements.Count > 1)
        {
            // Multiple children — create container node
            MutableAstObjectNode container = new()
            {
                TypeName = "__PropertyElementChildren__",
                XmlNamespace = element.Name.NamespaceName
            };
            foreach (XElement child in childElements)
            {
                container.Children.Add(ConvertElement(child, document, diagnostics));
            }
            propNode.Value = container;
        }
        else
        {
            // Text content
            string text = element.Value.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                propNode.Value = new MutableAstTextNode { Text = text };
            }
        }

        parentNode.Properties.Add(propNode);
    }

    private static MutableAstDocument? TryTolerantParse(
        string xamlText,
        List<XamlDiagnostic> diagnostics)
    {
        // Tolerant parsing: attempt to recover from common XML errors
        try
        {
            string trimmed = xamlText.TrimStart();
            if (!trimmed.StartsWith('<'))
            {
                return null;
            }

            // Strategy 1: Try to close unclosed tags
            string repaired = TryRepairUnclosedTags(xamlText);
            try
            {
                XDocument repairedDoc = XDocument.Parse(repaired, LoadOptions.SetLineInfo);
                if (repairedDoc.Root is not null)
                {
                    diagnostics.Add(new XamlDiagnostic
                    {
                        Severity = DiagnosticSeverity.Warning,
                        Message = "Document was repaired automatically (unclosed tags).",
                        Line = 1,
                        Column = 1,
                        Length = 0
                    });

                    MutableAstDocument document = new();
                    MutableAstObjectNode root = ConvertElement(repairedDoc.Root, document, diagnostics);
                    document.Root = root;
                    document.NodeMap.RegisterTree(root);
                    ExtractNamespaces(repairedDoc.Root, document);
                    return document;
                }
            }
            catch (XmlException)
            {
                // Repair attempt failed, try truncation
            }

            // Strategy 2: Truncate at the error point — find the last valid closing tag
            string? truncated = TryTruncateToValid(xamlText);
            if (truncated is not null)
            {
                try
                {
                    XDocument truncatedDoc = XDocument.Parse(truncated, LoadOptions.SetLineInfo);
                    if (truncatedDoc.Root is not null)
                    {
                        diagnostics.Add(new XamlDiagnostic
                        {
                            Severity = DiagnosticSeverity.Warning,
                            Message = "Partial document parsed (truncated at error).",
                            Line = 1,
                            Column = 1,
                            Length = 0
                        });

                        MutableAstDocument document = new();
                        MutableAstObjectNode root = ConvertElement(truncatedDoc.Root, document, diagnostics);
                        document.Root = root;
                        document.NodeMap.RegisterTree(root);
                        ExtractNamespaces(truncatedDoc.Root, document);
                        return document;
                    }
                }
                catch (XmlException)
                {
                    // Truncation also failed
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Tolerant XAML parse failed unexpectedly: {ex}");
            return null;
        }
    }

    private static void ExtractNamespaces(XElement root, MutableAstDocument document)
    {
        foreach (XAttribute attr in root.Attributes())
        {
            if (attr.IsNamespaceDeclaration)
            {
                string prefix = attr.Name.LocalName == "xmlns" ? "" : attr.Name.LocalName;
                document.NamespaceAliases[prefix] = attr.Value;
            }
        }
    }

    /// <summary>
    /// Attempts to repair XAML by closing unclosed tags using a simple stack-based approach.
    /// </summary>
    private static string TryRepairUnclosedTags(string xamlText)
    {
        Stack<string> openTags = new();
        int i = 0;

        while (i < xamlText.Length)
        {
            if (xamlText[i] == '<')
            {
                if (i + 1 < xamlText.Length && xamlText[i + 1] == '/')
                {
                    // Closing tag
                    int nameStart = i + 2;
                    int nameEnd = xamlText.IndexOf('>', nameStart);
                    if (nameEnd > nameStart)
                    {
                        string closingName = xamlText[nameStart..nameEnd].Trim();
                        if (openTags.Count > 0 && openTags.Peek() == closingName)
                        {
                            openTags.Pop();
                        }
                        i = nameEnd + 1;
                        continue;
                    }
                }
                else if (i + 1 < xamlText.Length && xamlText[i + 1] != '!' && xamlText[i + 1] != '?')
                {
                    // Opening tag — extract name
                    int nameStart = i + 1;
                    int nameEnd = nameStart;
                    while (nameEnd < xamlText.Length &&
                           xamlText[nameEnd] != ' ' &&
                           xamlText[nameEnd] != '>' &&
                           xamlText[nameEnd] != '/' &&
                           xamlText[nameEnd] != '\r' &&
                           xamlText[nameEnd] != '\n')
                    {
                        nameEnd++;
                    }

                    if (nameEnd > nameStart)
                    {
                        string tagName = xamlText[nameStart..nameEnd];

                        // Find the end of this tag
                        int closeGt = xamlText.IndexOf('>', nameEnd);
                        if (closeGt > 0)
                        {
                            bool selfClosing = xamlText[closeGt - 1] == '/';
                            if (!selfClosing)
                            {
                                openTags.Push(tagName);
                            }
                            i = closeGt + 1;
                            continue;
                        }
                    }
                }
            }

            i++;
        }

        // Close remaining open tags in reverse order
        if (openTags.Count == 0)
        {
            return xamlText;
        }

        StringBuilder sb = new(xamlText);
        while (openTags.Count > 0)
        {
            string tag = openTags.Pop();
            sb.AppendLine();
            sb.Append($"</{tag}>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Tries to truncate the XAML at a point that yields valid XML.
    /// Works backwards from the end to find a valid document.
    /// </summary>
    private static string? TryTruncateToValid(string xamlText)
    {
        // Find the last complete closing tag
        int lastClose = xamlText.LastIndexOf("</", StringComparison.Ordinal);
        while (lastClose > 0)
        {
            int end = xamlText.IndexOf('>', lastClose);
            if (end > lastClose)
            {
                string candidate = xamlText[..(end + 1)];
                try
                {
                    XDocument.Parse(candidate, LoadOptions.None);
                    return candidate;
                }
                catch (XmlException)
                {
                    // Try earlier
                }
            }

            lastClose = xamlText.LastIndexOf("</", lastClose - 1, StringComparison.Ordinal);
        }

        return null;
    }

    private static void RegisterNamespaceAliases(XElement element, MutableAstDocument document)
    {
        foreach (XAttribute attr in element.Attributes())
        {
            if (!attr.IsNamespaceDeclaration)
            {
                continue;
            }

            string prefix = attr.Name.LocalName == "xmlns"
                ? ""
                : attr.Name.LocalName;

            if (!document.NamespaceAliases.ContainsKey(prefix))
            {
                document.NamespaceAliases[prefix] = attr.Value;
            }
        }
    }

    private static string? FindPrefixForNamespace(MutableAstDocument document, string namespaceUri)
    {
        foreach (KeyValuePair<string, string> alias in document.NamespaceAliases)
        {
            if (string.Equals(alias.Value, namespaceUri, StringComparison.Ordinal))
            {
                return alias.Key;
            }
        }

        return null;
    }
}
