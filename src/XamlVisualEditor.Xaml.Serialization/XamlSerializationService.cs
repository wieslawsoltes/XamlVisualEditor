using System.Text;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.Xaml.Serialization;

/// <summary>
/// Serializes a mutable AST document back to formatted XAML text.
/// </summary>
public sealed class XamlSerializationService : IXamlSerializationService
{
    /// <inheritdoc />
    public string Serialize(IXamlDocumentModel document, SerializationOptions? options = null)
    {
        if (document is not MutableAstDocument mutableDoc || mutableDoc.Root is null)
        {
            return string.Empty;
        }

        options ??= new SerializationOptions();
        StringBuilder sb = new(4096);
        SerializeNode(mutableDoc.Root, mutableDoc.NamespaceAliases, sb, 0, options, isRoot: true);
        return sb.ToString();
    }

    /// <inheritdoc />
    public IReadOnlyList<TextEdit> ComputeMinimalEdits(
        IXamlDocumentModel document,
        string currentText,
        IReadOnlyList<AstChange> changes)
    {
        // For now, serialize the full document and compute a single replacement edit.
        // This will be refined to produce truly minimal edits based on change records.
        string newText = Serialize(document);

        if (currentText == newText)
        {
            return Array.Empty<TextEdit>();
        }

        // Find common prefix and suffix to minimize the edit
        int commonPrefix = 0;
        int maxPrefix = Math.Min(currentText.Length, newText.Length);
        while (commonPrefix < maxPrefix && currentText[commonPrefix] == newText[commonPrefix])
        {
            commonPrefix++;
        }

        int commonSuffix = 0;
        int maxSuffix = Math.Min(currentText.Length - commonPrefix, newText.Length - commonPrefix);
        while (commonSuffix < maxSuffix &&
               currentText[currentText.Length - 1 - commonSuffix] == newText[newText.Length - 1 - commonSuffix])
        {
            commonSuffix++;
        }

        int oldLength = currentText.Length - commonPrefix - commonSuffix;
        string replacement = newText.Substring(commonPrefix, newText.Length - commonPrefix - commonSuffix);

        return new[]
        {
            new TextEdit
            {
                Offset = commonPrefix,
                Length = oldLength,
                NewText = replacement
            }
        };
    }

    private void SerializeNode(
        MutableAstNode node,
        Dictionary<string, string> namespaces,
        StringBuilder sb,
        int depth,
        SerializationOptions options,
        bool isRoot = false)
    {
        switch (node)
        {
            case MutableAstObjectNode objectNode:
                SerializeObjectNode(objectNode, namespaces, sb, depth, options, isRoot);
                break;
            case MutableAstTextNode textNode:
                sb.Append(EscapeXmlText(textNode.Text));
                break;
        }
    }

    private void SerializeObjectNode(
        MutableAstObjectNode node,
        Dictionary<string, string> namespaces,
        StringBuilder sb,
        int depth,
        SerializationOptions options,
        bool isRoot)
    {
        string indent = GetIndent(depth, options);
        string prefix = GetPrefix(node.XmlNamespace, namespaces);
        string tagName = string.IsNullOrEmpty(prefix) ? node.TypeName : $"{prefix}:{node.TypeName}";

        sb.Append(indent);
        sb.Append('<');
        sb.Append(tagName);

        // Write namespace declarations on root element
        if (isRoot)
        {
            foreach (KeyValuePair<string, string> ns in namespaces)
            {
                sb.AppendLine();
                sb.Append(GetIndent(depth + 1, options));
                if (string.IsNullOrEmpty(ns.Key))
                {
                    sb.Append($"xmlns=\"{EscapeXmlAttribute(ns.Value)}\"");
                }
                else
                {
                    sb.Append($"xmlns:{ns.Key}=\"{EscapeXmlAttribute(ns.Value)}\"");
                }
            }
        }

        // Write attribute properties
        List<MutableAstPropertyNode> attributeProps = new();
        List<MutableAstPropertyNode> elementProps = new();

        foreach (MutableAstPropertyNode prop in node.Properties)
        {
            if (IsAttributeProperty(prop))
            {
                attributeProps.Add(prop);
            }
            else
            {
                elementProps.Add(prop);
            }
        }

        // Apply attribute ordering strategy
        if (options.AttributeOrdering == AttributeOrdering.Alphabetical)
        {
            attributeProps.Sort((a, b) => string.Compare(a.PropertyName, b.PropertyName, StringComparison.Ordinal));
        }
        else if (options.AttributeOrdering == AttributeOrdering.ByCategory)
        {
            attributeProps.Sort((a, b) =>
            {
                int catA = GetAttributeCategory(a.PropertyName);
                int catB = GetAttributeCategory(b.PropertyName);
                int cmp = catA.CompareTo(catB);
                return cmp != 0 ? cmp : string.Compare(a.PropertyName, b.PropertyName, StringComparison.Ordinal);
            });
        }
        // AttributeOrdering.Preserve: no sorting

        foreach (MutableAstPropertyNode prop in attributeProps)
        {
            string value = prop.Value is MutableAstTextNode textNode ? textNode.Text : "";
            sb.AppendLine();
            sb.Append(GetIndent(depth + 1, options));
            sb.Append($"{prop.PropertyName}=\"{EscapeXmlAttribute(value)}\"");
        }

        bool hasContent = node.Children.Count > 0 || elementProps.Count > 0;

        if (!hasContent)
        {
            sb.Append(" />");
            sb.AppendLine();
        }
        else
        {
            sb.Append('>');
            sb.AppendLine();

            // Write property element children
            foreach (MutableAstPropertyNode prop in elementProps)
            {
                SerializePropertyElement(prop, tagName, namespaces, sb, depth + 1, options);
            }

            // Write child elements
            foreach (MutableAstNode child in node.Children)
            {
                SerializeNode(child, namespaces, sb, depth + 1, options);
            }

            sb.Append(indent);
            sb.Append($"</{tagName}>");
            sb.AppendLine();
        }
    }

    private void SerializePropertyElement(
        MutableAstPropertyNode prop,
        string parentTagName,
        Dictionary<string, string> namespaces,
        StringBuilder sb,
        int depth,
        SerializationOptions options)
    {
        string indent = GetIndent(depth, options);
        string elementName = prop.PropertyName.Contains('.')
            ? prop.PropertyName
            : $"{parentTagName}.{prop.PropertyName}";

        sb.Append(indent);
        sb.Append($"<{elementName}>");
        sb.AppendLine();

        if (prop.Value is not null)
        {
            SerializeNode(prop.Value, namespaces, sb, depth + 1, options);
        }

        sb.Append(indent);
        sb.Append($"</{elementName}>");
        sb.AppendLine();
    }

    private static bool IsAttributeProperty(MutableAstPropertyNode prop)
    {
        // A property is serialized as an attribute if its value is a simple text node
        return prop.Value is MutableAstTextNode;
    }

    private static string GetPrefix(string xmlNamespace, Dictionary<string, string> namespaces)
    {
        foreach (KeyValuePair<string, string> kvp in namespaces)
        {
            if (kvp.Value == xmlNamespace)
            {
                return kvp.Key;
            }
        }
        return string.Empty;
    }

    private static string GetIndent(int depth, SerializationOptions options)
    {
        return string.Concat(Enumerable.Repeat(options.IndentString, depth));
    }

    private static string EscapeXmlText(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static string EscapeXmlAttribute(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    /// <summary>
    /// Returns a sort category for a property name when using ByCategory ordering.
    /// Lower numbers appear first.
    /// </summary>
    private static int GetAttributeCategory(string propertyName)
    {
        // 0: Identity (x:Name, x:Key, x:Class)
        if (propertyName.StartsWith("x:", StringComparison.Ordinal) ||
            propertyName.Equals("Name", StringComparison.Ordinal) ||
            propertyName.Equals("Key", StringComparison.Ordinal))
        {
            return 0;
        }

        // 1: Layout (Width, Height, Margin, Padding, HorizontalAlignment, VerticalAlignment, Grid.*)
        if (propertyName is "Width" or "Height" or "MinWidth" or "MinHeight" or "MaxWidth" or "MaxHeight" or
            "Margin" or "Padding" or "HorizontalAlignment" or "VerticalAlignment" or
            "HorizontalContentAlignment" or "VerticalContentAlignment" ||
            propertyName.StartsWith("Grid.", StringComparison.Ordinal) ||
            propertyName.StartsWith("Canvas.", StringComparison.Ordinal) ||
            propertyName.StartsWith("DockPanel.", StringComparison.Ordinal))
        {
            return 1;
        }

        // 2: Appearance (Background, Foreground, FontSize, Opacity, etc.)
        if (propertyName is "Background" or "Foreground" or "BorderBrush" or "BorderThickness" or
            "FontSize" or "FontWeight" or "FontFamily" or "FontStyle" or
            "Opacity" or "IsVisible" or "CornerRadius")
        {
            return 2;
        }

        // 3: Common (Content, Text, Header, Command, etc.)
        if (propertyName is "Content" or "Text" or "Header" or "Title" or
            "Command" or "CommandParameter" or "IsEnabled" or "IsChecked")
        {
            return 3;
        }

        // 4: Events and other
        return 4;
    }
}
