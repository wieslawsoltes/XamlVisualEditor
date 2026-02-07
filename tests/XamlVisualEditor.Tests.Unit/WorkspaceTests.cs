using System.IO;
using XamlVisualEditor.Core;
using XamlVisualEditor.Workspace;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class WorkspaceTests
{
    [Fact]
    public void LoadAssembly_LoadsTypesFromAssembly()
    {
        TypeMetadataService service = new();
        string assemblyPath = typeof(TypeMetadata).Assembly.Location;

        Assert.False(string.IsNullOrWhiteSpace(assemblyPath));
        Assert.True(File.Exists(assemblyPath));

        service.LoadAssembly(assemblyPath);
        TypeMetadata? meta = service.GetType(string.Empty, typeof(TypeMetadata).FullName!);

        Assert.NotNull(meta);
        Assert.Equal(typeof(TypeMetadata).FullName, meta!.FullName);
    }

    [Fact]
    public void LoadAssemblies_LoadsTypesFromAssembly()
    {
        TypeMetadataService service = new();
        string assemblyPath = typeof(TypeMetadata).Assembly.Location;

        service.LoadAssemblies(new[] { assemblyPath });
        TypeMetadata? meta = service.GetType(string.Empty, typeof(TypeMetadata).FullName!);

        Assert.NotNull(meta);
    }

    [Fact]
    public void ResolveClrType_ReturnsTypeFromLoadedAssembly()
    {
        TypeMetadataService service = new();
        string assemblyPath = typeof(TypeMetadata).Assembly.Location;

        service.LoadAssembly(assemblyPath);
        TypeMetadata? meta = service.GetType(string.Empty, typeof(TypeMetadata).FullName!);

        Assert.NotNull(meta);
        Type? resolved = service.ResolveClrType(meta!);
        Assert.NotNull(resolved);
        Assert.Equal(typeof(TypeMetadata), resolved);
    }
}
