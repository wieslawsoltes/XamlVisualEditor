using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Metadata;
using Avalonia.Styling;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>
/// Holds factories for the workspace application's styles and resource includes so
/// the design surface can theme instantiated controls the same way the target app
/// does at runtime. Factories (instead of shared instances) because every design
/// surface needs its own style instances.
/// </summary>
public static class WorkspaceDesignThemeRegistry
{
    public static int Version { get; private set; }

    public static IReadOnlyList<Func<IStyle?>> StyleFactories { get; private set; } =
        Array.Empty<Func<IStyle?>>();

    public static IReadOnlyList<Func<IResourceProvider?>> ResourceFactories { get; private set; } =
        Array.Empty<Func<IResourceProvider?>>();

    public static void Update(
        IReadOnlyList<Func<IStyle?>> styleFactories,
        IReadOnlyList<Func<IResourceProvider?>> resourceFactories)
    {
        StyleFactories = styleFactories;
        ResourceFactories = resourceFactories;
        Version++;
    }

    public static void Clear()
    {
        Update(Array.Empty<Func<IStyle?>>(), Array.Empty<Func<IResourceProvider?>>());
    }
}

/// <summary>
/// Parses the workspace application's App.axaml and builds theme/resource factories
/// from it. Types referenced there are resolved against the assemblies the workspace
/// loader has already brought into the default load context.
/// </summary>
public static class WorkspaceDesignThemeLoader
{
    /// <summary>
    /// Loads styles and resource includes from the given App.axaml into the registry.
    /// Returns human-readable detail lines for the output log.
    /// </summary>
    public static IReadOnlyList<string> LoadFromApplicationXaml(string appXamlPath)
    {
        List<string> details = new();
        List<Func<IStyle?>> styleFactories = new();
        List<Func<IResourceProvider?>> resourceFactories = new();

        XDocument document = XDocument.Load(appXamlPath);
        XElement? root = document.Root;
        if (root is null)
        {
            WorkspaceDesignThemeRegistry.Clear();
            return details;
        }

        XElement? stylesElement = root.Elements()
            .FirstOrDefault(e => e.Name.LocalName.EndsWith(".Styles", StringComparison.Ordinal));
        if (stylesElement is not null)
        {
            foreach (XElement child in stylesElement.Elements())
            {
                string localName = child.Name.LocalName;

                if (localName == "FluentTheme")
                {
                    details.Add("FluentTheme skipped (provided by the editor)");
                    continue;
                }

                if (localName == "Style")
                {
                    details.Add("inline Style skipped");
                    continue;
                }

                if (localName == "StyleInclude")
                {
                    Uri? source = TryGetAbsoluteUri(child.Attribute("Source")?.Value);
                    if (source is not null)
                    {
                        styleFactories.Add(() => new StyleInclude(source) { Source = source });
                        details.Add($"StyleInclude {source}");
                    }
                    else
                    {
                        details.Add($"StyleInclude with non-absolute source skipped: {child.Attribute("Source")?.Value}");
                    }

                    continue;
                }

                Type? styleType = ResolveXamlType(child.Name.NamespaceName, localName);
                if (styleType is not null
                    && typeof(IStyle).IsAssignableFrom(styleType)
                    && styleType.GetConstructor(Type.EmptyTypes) is not null)
                {
                    styleFactories.Add(() => Activator.CreateInstance(styleType) as IStyle);
                    details.Add($"theme {styleType.FullName}");
                }
                else
                {
                    details.Add($"style element '{localName}' not resolvable, skipped");
                }
            }
        }

        XElement? resourcesElement = root.Elements()
            .FirstOrDefault(e => e.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal));
        if (resourcesElement is not null)
        {
            foreach (XElement include in resourcesElement.Descendants()
                         .Where(e => e.Name.LocalName == "ResourceInclude"))
            {
                Uri? source = TryGetAbsoluteUri(include.Attribute("Source")?.Value);
                if (source is not null)
                {
                    resourceFactories.Add(() => new ResourceInclude(source) { Source = source });
                    details.Add($"ResourceInclude {source}");
                }
                else
                {
                    details.Add($"ResourceInclude with non-absolute source skipped: {include.Attribute("Source")?.Value}");
                }
            }
        }

        WorkspaceDesignThemeRegistry.Update(styleFactories, resourceFactories);
        return details;
    }

    private static Uri? TryGetAbsoluteUri(string? source)
    {
        return Uri.TryCreate(source, UriKind.Absolute, out Uri? uri) ? uri : null;
    }

    private static Type? ResolveXamlType(string xmlNamespace, string localName)
    {
        if (xmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            string body = xmlNamespace.Substring("clr-namespace:".Length);
            string[] parts = body.Split(';');
            string clrNamespace = parts[0].Trim();
            string? assemblyName = parts.Skip(1)
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.StartsWith("assembly=", StringComparison.Ordinal))?
                .Substring("assembly=".Length);

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assemblyName is not null
                    && !string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Type? byClrNamespace = TryGetType(assembly, clrNamespace + "." + localName);
                if (byClrNamespace is not null)
                {
                    return byClrNamespace;
                }
            }

            return null;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (XmlnsDefinitionAttribute definition in SafeGetXmlnsDefinitions(assembly))
            {
                if (!string.Equals(definition.XmlNamespace, xmlNamespace, StringComparison.Ordinal))
                {
                    continue;
                }

                Type? byXmlns = TryGetType(assembly, definition.ClrNamespace + "." + localName);
                if (byXmlns is not null)
                {
                    return byXmlns;
                }
            }
        }

        return null;
    }

    private static IEnumerable<XmlnsDefinitionAttribute> SafeGetXmlnsDefinitions(Assembly assembly)
    {
        try
        {
            return assembly.GetCustomAttributes<XmlnsDefinitionAttribute>();
        }
        catch
        {
            return Array.Empty<XmlnsDefinitionAttribute>();
        }
    }

    private static Type? TryGetType(Assembly assembly, string fullName)
    {
        try
        {
            return assembly.GetType(fullName, throwOnError: false);
        }
        catch
        {
            return null;
        }
    }
}
