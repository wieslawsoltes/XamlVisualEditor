using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.Xaml.Intellisense;

/// <summary>
/// Provides code completion for XAML element names after '&lt;'.
/// </summary>
public sealed class ElementCompletionProvider : ICompletionProvider
{
    /// <inheritdoc />
    public bool ShouldTrigger(CompletionContext context)
    {
        return context.Trigger == CompletionTrigger.CharacterTyped &&
               context.TextBefore.TrimEnd().EndsWith('<');
    }

    /// <inheritdoc />
    public IReadOnlyList<CompletionItem> GetCompletions(CompletionContext context)
    {
        if (context.Metadata is null)
        {
            return Array.Empty<CompletionItem>();
        }

        IReadOnlyList<TypeMetadata> types = context.Metadata.GetAvailableTypes();
        List<CompletionItem> items = new(types.Count);

        foreach (TypeMetadata type in types)
        {
            if (!type.IsControl) continue;

            items.Add(new CompletionItem
            {
                DisplayText = type.Name,
                InsertText = type.Name,
                Description = type.FullName,
                Kind = CompletionItemKind.Element,
                Priority = type.IsPanel ? 0 : 1
            });
        }

        items.Sort((a, b) => string.Compare(a.DisplayText, b.DisplayText, StringComparison.Ordinal));
        return items;
    }
}

/// <summary>
/// Provides code completion for XAML attribute names (properties).
/// </summary>
public sealed class AttributeCompletionProvider : ICompletionProvider
{
    /// <inheritdoc />
    public bool ShouldTrigger(CompletionContext context)
    {
        if (context.Trigger != CompletionTrigger.CharacterTyped &&
            context.Trigger != CompletionTrigger.Invoked)
        {
            return false;
        }

        // Trigger after a space inside an element tag
        string trimmed = context.TextBefore.TrimEnd();
        return IsInsideOpeningTag(trimmed) && context.TextBefore.EndsWith(' ');
    }

    /// <inheritdoc />
    public IReadOnlyList<CompletionItem> GetCompletions(CompletionContext context)
    {
        if (context.Metadata is null)
        {
            return Array.Empty<CompletionItem>();
        }

        // Extract the current element type from context
        string? elementTypeName = ExtractCurrentElementType(context.TextBefore);
        if (elementTypeName is null)
        {
            return Array.Empty<CompletionItem>();
        }

        // Find the type in metadata
        TypeMetadata? type = context.Metadata.GetType(
            "https://github.com/avaloniaui",
            elementTypeName);

        if (type is null)
        {
            return Array.Empty<CompletionItem>();
        }

        IReadOnlyList<PropertyMetadata> properties = context.Metadata.GetProperties(type);
        List<CompletionItem> items = new(properties.Count);

        foreach (PropertyMetadata prop in properties)
        {
            if (prop.IsReadOnly) continue;

            items.Add(new CompletionItem
            {
                DisplayText = prop.Name,
                InsertText = $"{prop.Name}=\"\"",
                Description = $"{prop.Name} : {prop.TypeFullName}",
                Kind = CompletionItemKind.Property,
                Priority = prop.IsAttached ? 2 : 1
            });
        }

        AddXamlDirectiveAttributes(items);

        items.Sort((a, b) => string.Compare(a.DisplayText, b.DisplayText, StringComparison.Ordinal));
        return items;
    }

    private static void AddXamlDirectiveAttributes(List<CompletionItem> items)
    {
        string[] directives =
        {
            "x:Name",
            "x:Class",
            "x:Key",
            "x:DataType",
            "x:FieldModifier"
        };

        foreach (string directive in directives)
        {
            items.Add(new CompletionItem
            {
                DisplayText = directive,
                InsertText = $"{directive}=\"\"",
                Description = directive,
                Kind = CompletionItemKind.Property,
                Priority = 0
            });
        }
    }

    private static bool IsInsideOpeningTag(string text)
    {
        int lastOpen = text.LastIndexOf('<');
        int lastClose = text.LastIndexOf('>');
        return lastOpen > lastClose;
    }

    private static string? ExtractCurrentElementType(string text)
    {
        int lastOpen = text.LastIndexOf('<');
        if (lastOpen < 0) return null;

        string afterOpen = text.Substring(lastOpen + 1).TrimStart();
        int spaceIndex = afterOpen.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        return spaceIndex > 0 ? afterOpen.Substring(0, spaceIndex) : null;
    }
}

/// <summary>
/// Provides code completion for XAML attribute values.
/// </summary>
public sealed class AttributeValueCompletionProvider : ICompletionProvider
{
    /// <inheritdoc />
    public bool ShouldTrigger(CompletionContext context)
    {
        return context.Trigger == CompletionTrigger.CharacterTyped &&
               context.TextBefore.TrimEnd().EndsWith("=\"");
    }

    /// <inheritdoc />
    public IReadOnlyList<CompletionItem> GetCompletions(CompletionContext context)
    {
        // Provide common values like True/False, alignment values, etc.
        List<CompletionItem> items = new();

        string? propertyName = ExtractCurrentProperty(context.TextBefore);
        if (propertyName is null)
        {
            return items;
        }

        // Add common value completions based on known property types
        switch (propertyName)
        {
            case "Mode":
                AddEnumValues(items, "Default", "OneWay", "TwoWay", "OneTime", "OneWayToSource");
                break;
            case "UpdateSourceTrigger":
                AddEnumValues(items, "Default", "PropertyChanged", "LostFocus", "Explicit");
                break;
            case "HorizontalAlignment":
                AddEnumValues(items, "Left", "Center", "Right", "Stretch");
                break;
            case "VerticalAlignment":
                AddEnumValues(items, "Top", "Center", "Bottom", "Stretch");
                break;
            case "Orientation":
                AddEnumValues(items, "Horizontal", "Vertical");
                break;
            case "Visibility" or "IsVisible":
                AddEnumValues(items, "True", "False");
                break;
            case "IsEnabled":
                AddEnumValues(items, "True", "False");
                break;
            case "TextWrapping":
                AddEnumValues(items, "NoWrap", "Wrap", "WrapWithOverflow");
                break;
            case "FontWeight":
                AddEnumValues(items, "Normal", "Bold", "Light", "SemiBold", "ExtraBold");
                break;
            case "Dock":
                AddEnumValues(items, "Left", "Top", "Right", "Bottom");
                break;
        }

        // Always offer markup extension templates
        items.Add(new CompletionItem
        {
            DisplayText = "{Binding}",
            InsertText = "{Binding }",
            Kind = CompletionItemKind.MarkupExtension,
            Priority = 10
        });
        items.Add(new CompletionItem
        {
            DisplayText = "{StaticResource}",
            InsertText = "{StaticResource }",
            Kind = CompletionItemKind.MarkupExtension,
            Priority = 10
        });
        items.Add(new CompletionItem
        {
            DisplayText = "{DynamicResource}",
            InsertText = "{DynamicResource }",
            Kind = CompletionItemKind.MarkupExtension,
            Priority = 10
        });
        items.Add(new CompletionItem
        {
            DisplayText = "{TemplateBinding}",
            InsertText = "{TemplateBinding }",
            Kind = CompletionItemKind.MarkupExtension,
            Priority = 10
        });

        // Add binding and resource keys when inside markup extensions
        string? markup = ExtractMarkupExtension(context.TextBefore);
        if (!string.IsNullOrWhiteSpace(markup))
        {
            AddBindingKeywords(items, markup);
            AddBindingValueCompletions(items, propertyName, markup, context.DocumentText, context.Metadata);
            AddResourceKeys(items, context.DocumentText);
        }

        return items;
    }

    private static void AddEnumValues(List<CompletionItem> items, params string[] values)
    {
        foreach (string value in values)
        {
            items.Add(new CompletionItem
            {
                DisplayText = value,
                InsertText = value,
                Kind = CompletionItemKind.Value,
                Priority = 0
            });
        }
    }

    private static string? ExtractCurrentProperty(string text)
    {
        // Find the property name before ="
        int eqIndex = text.LastIndexOf("=\"", StringComparison.Ordinal);
        if (eqIndex < 0) return null;

        string before = text.Substring(0, eqIndex).TrimEnd();
        int spaceIndex = before.LastIndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        return spaceIndex >= 0 ? before.Substring(spaceIndex + 1) : before;
    }

    private static string? ExtractMarkupExtension(string text)
    {
        int braceIndex = text.LastIndexOf('{');
        if (braceIndex < 0)
        {
            return null;
        }

        string after = text.Substring(braceIndex + 1).TrimStart();
        int end = after.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '}' });
        if (end < 0)
        {
            return after;
        }

        return end > 0 ? after.Substring(0, end) : null;
    }

    private static void AddBindingKeywords(List<CompletionItem> items, string markup)
    {
        if (!markup.Equals("Binding", StringComparison.OrdinalIgnoreCase) &&
            !markup.Equals("TemplateBinding", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string[] keywords =
        {
            "Path=",
            "ElementName=",
            "RelativeSource=",
            "Mode=",
            "Converter=",
            "ConverterParameter=",
            "StringFormat=",
            "FallbackValue=",
            "TargetNullValue=",
            "UpdateSourceTrigger=",
            "Source=",
            "x:Reference "
        };

        foreach (string keyword in keywords)
        {
            items.Add(new CompletionItem
            {
                DisplayText = keyword,
                InsertText = keyword,
                Kind = CompletionItemKind.Property,
                Priority = 5
            });
        }
    }

    private static void AddBindingValueCompletions(
        List<CompletionItem> items,
        string propertyName,
        string markup,
        string? documentText,
        ITypeMetadataService? metadata)
    {
        if (!markup.Equals("Binding", StringComparison.OrdinalIgnoreCase) &&
            !markup.Equals("TemplateBinding", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (propertyName.Equals("ElementName", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string name in ExtractElementNames(documentText))
            {
                items.Add(new CompletionItem
                {
                    DisplayText = name,
                    InsertText = name,
                    Kind = CompletionItemKind.Value,
                    Priority = 1
                });
            }

            return;
        }

        if (propertyName.Equals("Path", StringComparison.OrdinalIgnoreCase))
        {
            AddDataTypePropertyPaths(items, documentText, metadata);
            foreach (string path in ExtractBindingPaths(documentText))
            {
                items.Add(new CompletionItem
                {
                    DisplayText = path,
                    InsertText = path,
                    Kind = CompletionItemKind.Value,
                    Priority = 1
                });
            }
        }
    }

    private static void AddResourceKeys(List<CompletionItem> items, string? documentText)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return;
        }

        foreach (string key in ExtractResourceKeys(documentText))
        {
            items.Add(new CompletionItem
            {
                DisplayText = key,
                InsertText = key,
                Kind = CompletionItemKind.Value,
                Priority = 1
            });
        }
    }

    private static IReadOnlyList<string> ExtractResourceKeys(string text)
    {
        List<string> keys = new();
        int index = 0;
        while (index < text.Length)
        {
            int keyIndex = text.IndexOf("x:Key=\"", index, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                break;
            }

            int start = keyIndex + "x:Key=\"".Length;
            int end = text.IndexOf('"', start);
            if (end > start)
            {
                string key = text.Substring(start, end - start);
                if (!string.IsNullOrWhiteSpace(key) && !keys.Contains(key, StringComparer.Ordinal))
                {
                    keys.Add(key);
                }
                index = end + 1;
            }
            else
            {
                break;
            }
        }

        return keys;
    }

    private static IReadOnlyList<string> ExtractElementNames(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        ExtractAttributeValues(text, "x:Name", names);
        ExtractAttributeValues(text, "Name", names);
        return names.ToList();
    }

    private static IReadOnlyList<string> ExtractBindingPaths(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        HashSet<string> paths = new(StringComparer.Ordinal);
        int index = 0;
        while (index < text.Length)
        {
            int bindingIndex = text.IndexOf("{Binding", index, StringComparison.Ordinal);
            if (bindingIndex < 0)
            {
                break;
            }

            int start = bindingIndex + "{Binding".Length;
            int endBrace = text.IndexOf('}', start);
            if (endBrace < 0)
            {
                break;
            }

            string content = text.Substring(start, endBrace - start).Trim();
            if (content.StartsWith("Path=", StringComparison.Ordinal))
            {
                string path = ReadToken(content, "Path=");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    paths.Add(path);
                }
            }
            else if (!string.IsNullOrWhiteSpace(content))
            {
                string firstToken = ReadFirstToken(content);
                if (!string.IsNullOrWhiteSpace(firstToken))
                {
                    paths.Add(firstToken);
                }
            }

            index = endBrace + 1;
        }

        return paths.ToList();
    }

    private static void AddDataTypePropertyPaths(
        List<CompletionItem> items,
        string? documentText,
        ITypeMetadataService? metadata)
    {
        if (metadata is null || string.IsNullOrWhiteSpace(documentText))
        {
            return;
        }

        string? dataType = ExtractDataType(documentText);
        if (string.IsNullOrWhiteSpace(dataType))
        {
            return;
        }

        string? xmlNamespace = ResolveXmlNamespace(documentText, dataType, out string typeName);
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(xmlNamespace))
        {
            return;
        }

        TypeMetadata? type = metadata.GetType(xmlNamespace, typeName);
        if (type is null)
        {
            return;
        }

        foreach (PropertyMetadata prop in metadata.GetProperties(type))
        {
            items.Add(new CompletionItem
            {
                DisplayText = prop.Name,
                InsertText = prop.Name,
                Description = prop.TypeFullName,
                Kind = CompletionItemKind.Value,
                Priority = 1
            });
        }
    }

    private static string? ExtractDataType(string text)
    {
        return ExtractQuotedAttribute(text, "x:DataType");
    }

    private static string? ResolveXmlNamespace(string text, string dataType, out string typeName)
    {
        typeName = string.Empty;
        string trimmed = dataType.Trim();

        int colonIndex = trimmed.IndexOf(':');
        if (colonIndex < 0)
        {
            typeName = trimmed;
            string? defaultXmlns = ExtractQuotedAttribute(text, "xmlns");
            return defaultXmlns;
        }

        string prefix = trimmed.Substring(0, colonIndex);
        typeName = trimmed.Substring(colonIndex + 1);

        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        return ExtractQuotedAttribute(text, "xmlns:" + prefix);
    }

    private static string? ExtractQuotedAttribute(string text, string attributeName)
    {
        string doubleMarker = attributeName + "=\"";
        int index = text.IndexOf(doubleMarker, StringComparison.Ordinal);
        if (index >= 0)
        {
            int start = index + doubleMarker.Length;
            int end = text.IndexOf('"', start);
            return end > start ? text.Substring(start, end - start) : null;
        }

        string singleMarker = attributeName + "='";
        index = text.IndexOf(singleMarker, StringComparison.Ordinal);
        if (index >= 0)
        {
            int start = index + singleMarker.Length;
            int end = text.IndexOf('\'', start);
            return end > start ? text.Substring(start, end - start) : null;
        }

        return null;
    }

    private static void ExtractAttributeValues(string text, string attributeName, HashSet<string> values)
    {
        string marker = attributeName + "=\"";
        int index = 0;
        while (index < text.Length)
        {
            int attrIndex = text.IndexOf(marker, index, StringComparison.Ordinal);
            if (attrIndex < 0)
            {
                break;
            }

            int start = attrIndex + marker.Length;
            int end = text.IndexOf('"', start);
            if (end > start)
            {
                string value = text.Substring(start, end - start);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
                index = end + 1;
            }
            else
            {
                break;
            }
        }
    }

    private static string ReadToken(string content, string prefix)
    {
        string trimmed = content.Substring(prefix.Length).Trim();
        int end = trimmed.IndexOfAny(new[] { ',', ' ', '\t', '\r', '\n', '}' });
        return end >= 0 ? trimmed.Substring(0, end) : trimmed;
    }

    private static string ReadFirstToken(string content)
    {
        string trimmed = content.TrimStart();
        int end = trimmed.IndexOfAny(new[] { ',', ' ', '\t', '\r', '\n', '}' });
        return end >= 0 ? trimmed.Substring(0, end) : trimmed;
    }
}

/// <summary>
/// Provides code completion for XML namespace declarations.
/// </summary>
public sealed class XmlnsCompletionProvider : ICompletionProvider
{
    private static readonly (string Prefix, string Namespace)[] KnownNamespaces = new[]
    {
        ("", "https://github.com/avaloniaui"),
        ("x", "http://schemas.microsoft.com/winfx/2006/xaml"),
        ("d", "http://schemas.microsoft.com/expression/blend/2008"),
        ("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006"),
        ("i", "clr-namespace:Avalonia.Xaml.Interactivity;assembly=Avalonia.Xaml.Interactivity"),
        ("ia", "clr-namespace:Avalonia.Xaml.Interactions.Core;assembly=Avalonia.Xaml.Interactions"),
    };

    /// <inheritdoc />
    public bool ShouldTrigger(CompletionContext context)
    {
        return context.TextBefore.TrimEnd().EndsWith("xmlns:", StringComparison.Ordinal) ||
               context.TextBefore.TrimEnd().EndsWith("xmlns=\"", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyList<CompletionItem> GetCompletions(CompletionContext context)
    {
        List<CompletionItem> items = new();

        foreach ((string prefix, string ns) in KnownNamespaces)
        {
            string displayText = string.IsNullOrEmpty(prefix) ? ns : $"{prefix} → {ns}";
            items.Add(new CompletionItem
            {
                DisplayText = displayText,
                InsertText = string.IsNullOrEmpty(prefix)
                    ? ns
                    : $"{prefix}=\"{ns}\"",
                Description = ns,
                Kind = CompletionItemKind.Namespace,
                Priority = 0
            });
        }

        return items;
    }
}

/// <summary>
/// Provides completion for markup extension names and common binding keywords.
/// </summary>
public sealed class MarkupExtensionCompletionProvider : ICompletionProvider
{
    /// <inheritdoc />
    public bool ShouldTrigger(CompletionContext context)
    {
        return context.Trigger == CompletionTrigger.CharacterTyped &&
               context.TextBefore.TrimEnd().EndsWith("{", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyList<CompletionItem> GetCompletions(CompletionContext context)
    {
        List<CompletionItem> items = new()
        {
            new CompletionItem
            {
                DisplayText = "Binding",
                InsertText = "Binding ",
                Kind = CompletionItemKind.MarkupExtension,
                Priority = 0
            },
            new CompletionItem
            {
                DisplayText = "StaticResource",
                InsertText = "StaticResource ",
                Kind = CompletionItemKind.MarkupExtension,
                Priority = 0
            },
            new CompletionItem
            {
                DisplayText = "DynamicResource",
                InsertText = "DynamicResource ",
                Kind = CompletionItemKind.MarkupExtension,
                Priority = 0
            },
            new CompletionItem
            {
                DisplayText = "TemplateBinding",
                InsertText = "TemplateBinding ",
                Kind = CompletionItemKind.MarkupExtension,
                Priority = 0
            },
            new CompletionItem
            {
                DisplayText = "x:Reference",
                InsertText = "x:Reference ",
                Kind = CompletionItemKind.MarkupExtension,
                Priority = 0
            },
            new CompletionItem
            {
                DisplayText = "x:Static",
                InsertText = "x:Static ",
                Kind = CompletionItemKind.MarkupExtension,
                Priority = 0
            },
            new CompletionItem
            {
                DisplayText = "x:Type",
                InsertText = "x:Type ",
                Kind = CompletionItemKind.MarkupExtension,
                Priority = 0
            }
        };

        return items;
    }
}

/// <summary>
/// Provides auto-completion for closing tags.
/// </summary>
public sealed class ClosingTagCompletionProvider : ICompletionProvider
{
    /// <inheritdoc />
    public bool ShouldTrigger(CompletionContext context)
    {
        return context.TextBefore.TrimEnd().EndsWith("</", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyList<CompletionItem> GetCompletions(CompletionContext context)
    {
        List<CompletionItem> items = new();

        // Find the most recent unclosed tag
        string? tagName = FindUnclosedTag(context.TextBefore);
        if (tagName is not null)
        {
            items.Add(new CompletionItem
            {
                DisplayText = $"</{tagName}>",
                InsertText = $"{tagName}>",
                Description = $"Close <{tagName}> element",
                Kind = CompletionItemKind.ClosingTag,
                Priority = 0
            });
        }

        return items;
    }

    private static string? FindUnclosedTag(string text)
    {
        // Simple stack-based approach to find unclosed tags
        Stack<string> openTags = new();
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
                        string closingName = text.Substring(nameStart, nameEnd - nameStart).Trim();
                        if (openTags.Count > 0 && openTags.Peek() == closingName)
                        {
                            openTags.Pop();
                        }
                    }
                    i = nameEnd > 0 ? nameEnd + 1 : i + 1;
                }
                else
                {
                    // Opening tag
                    int nameStart = i + 1;
                    int spaceOrClose = nameStart;
                    while (spaceOrClose < text.Length &&
                           text[spaceOrClose] != ' ' &&
                           text[spaceOrClose] != '>' &&
                           text[spaceOrClose] != '/' &&
                           text[spaceOrClose] != '\r' &&
                           text[spaceOrClose] != '\n')
                    {
                        spaceOrClose++;
                    }

                    if (spaceOrClose > nameStart)
                    {
                        string tagName = text.Substring(nameStart, spaceOrClose - nameStart);

                        // Check if self-closing
                        int closeIndex = text.IndexOf('>', spaceOrClose);
                        if (closeIndex > 0 && text[closeIndex - 1] != '/')
                        {
                            openTags.Push(tagName);
                        }
                    }

                    int gt = text.IndexOf('>', i);
                    i = gt > 0 ? gt + 1 : i + 1;
                }
            }
            else
            {
                i++;
            }
        }

        return openTags.Count > 0 ? openTags.Peek() : null;
    }
}

/// <summary>
/// Registry that aggregates multiple completion providers.
/// </summary>
public sealed class CompletionProviderRegistry
{
    private readonly List<ICompletionProvider> _providers = new();

    /// <summary>
    /// Registers a completion provider.
    /// </summary>
    public void Register(ICompletionProvider provider)
    {
        _providers.Add(provider);
    }

    /// <summary>
    /// Gets completions from all providers that should trigger for the given context.
    /// </summary>
    public IReadOnlyList<CompletionItem> GetCompletions(CompletionContext context)
    {
        List<CompletionItem> allItems = new();

        foreach (ICompletionProvider provider in _providers)
        {
            if (provider.ShouldTrigger(context))
            {
                IReadOnlyList<CompletionItem> items = provider.GetCompletions(context);
                allItems.AddRange(items);
            }
        }

        return allItems;
    }

    /// <summary>
    /// Creates a registry with all standard XAML completion providers.
    /// </summary>
    public static CompletionProviderRegistry CreateDefault()
    {
        CompletionProviderRegistry registry = new();
        registry.Register(new ElementCompletionProvider());
        registry.Register(new AttributeCompletionProvider());
        registry.Register(new AttributeValueCompletionProvider());
        registry.Register(new XmlnsCompletionProvider());
        registry.Register(new ClosingTagCompletionProvider());
        registry.Register(new MarkupExtensionCompletionProvider());
        return registry;
    }
}
