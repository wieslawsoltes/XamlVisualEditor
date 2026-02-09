using System;
using System.Linq;
using Avalonia.Controls;
using XamlVisualEditor.Core;
using XamlVisualEditor.Workspace;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class TypeMetadataServiceTests
{
    [Fact]
    public void GetAvailableNamespaces_IncludesAvaloniaNamespaceAfterLoad()
    {
        TypeMetadataService service = new();
        string assemblyPath = typeof(Button).Assembly.Location;

        service.LoadAssembly(assemblyPath);

        string[] namespaces = service.GetAvailableNamespaces().ToArray();
        Assert.Contains("https://github.com/avaloniaui", namespaces);
    }

    [Fact]
    public void GetType_ResolvesFromXmlNamespaceMapping()
    {
        TypeMetadataService service = new();
        string assemblyPath = typeof(Button).Assembly.Location;
        string assemblyName = typeof(Button).Assembly.GetName().Name ?? string.Empty;

        service.LoadAssembly(assemblyPath);

        TypeMetadata? meta = service.GetType("https://github.com/avaloniaui", "Button");
        Assert.NotNull(meta);
        Assert.Equal("Button", meta!.Name);
        Assert.Equal(assemblyName, meta.AssemblyName);
    }

    [Fact]
    public void GetAvailableTypes_ReturnsControlsFromMappedNamespace()
    {
        TypeMetadataService service = new();
        string assemblyPath = typeof(Button).Assembly.Location;

        service.LoadAssembly(assemblyPath);

        var types = service.GetAvailableTypes("https://github.com/avaloniaui");
        Assert.Contains(types, t => string.Equals(t.Name, "Button", StringComparison.Ordinal));
    }
}
