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
                Priority = prop.Kind == PropertyKind.Attached ? 2 : 1
            });
        }

        items.Sort((a, b) => string.Compare(a.DisplayText, b.DisplayText, StringComparison.Ordinal));
        return items;
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
        return registry;
    }
}
