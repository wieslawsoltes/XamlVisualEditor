using System.IO;
using System.Linq;
using System.Reflection;
using XamlVisualEditor.Core;
using XamlVisualEditor.Shell.ViewModels;
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

    [Fact]
    public void CollectWorkspaceAssemblies_FiltersReferenceAssembliesAndPrefersOutputs()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "XveWorkspaceTests", Path.GetRandomFileName());
        Directory.CreateDirectory(tempRoot);

        try
        {
            string projectDir = Path.Combine(tempRoot, "App");
            string outputDir = Path.Combine(projectDir, "bin", "Debug", "net8.0");
            string outputPath = Path.Combine(outputDir, "App.dll");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(outputPath, "dummy");

            string refDir = Path.Combine(projectDir, "ref");
            string refPath = Path.Combine(refDir, "RefOnly.dll");
            Directory.CreateDirectory(refDir);
            File.WriteAllText(refPath, "dummy");

            string normalRefDir = Path.Combine(projectDir, "refs");
            string normalRefPath = Path.Combine(normalRefDir, "Normal.dll");
            Directory.CreateDirectory(normalRefDir);
            File.WriteAllText(normalRefPath, "dummy");

            WorkspaceModel workspace = new()
            {
                Projects = new[]
                {
                    new ProjectModel
                    {
                        Name = "App",
                        ProjectPath = Path.Combine(projectDir, "App.csproj"),
                        XamlFiles = Array.Empty<XamlFileModel>(),
                        Files = Array.Empty<ProjectFileModel>(),
                        References = new[]
                        {
                            new AssemblyReference { Name = "RefOnly", Path = refPath },
                            new AssemblyReference { Name = "Normal", Path = normalRefPath }
                        },
                        OutputAssemblyPath = outputPath
                    }
                },
                ProjectFolders = new System.Collections.Generic.Dictionary<string, string>()
            };

            (object? result, bool hasAnyOutputs, bool hasMissingOutputs) = InvokeCollectWorkspaceAssemblies(workspace);
            var all = GetAssemblySetPaths(result, "All");
            var preferred = GetAssemblySetPaths(result, "Preferred");

            Assert.True(hasAnyOutputs);
            Assert.False(hasMissingOutputs);
            Assert.Contains(outputPath, preferred);
            Assert.Contains(outputPath, all);
            Assert.Contains(normalRefPath, all);
            Assert.DoesNotContain(refPath, all);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public void CollectWorkspaceAssemblies_ReportsMissingOutputs()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "XveWorkspaceTests", Path.GetRandomFileName());
        Directory.CreateDirectory(tempRoot);

        try
        {
            string projectDir = Path.Combine(tempRoot, "App");
            Directory.CreateDirectory(projectDir);

            WorkspaceModel workspace = new()
            {
                Projects = new[]
                {
                    new ProjectModel
                    {
                        Name = "App",
                        ProjectPath = Path.Combine(projectDir, "App.csproj"),
                        XamlFiles = Array.Empty<XamlFileModel>(),
                        Files = Array.Empty<ProjectFileModel>(),
                        References = Array.Empty<AssemblyReference>(),
                        OutputAssemblyPath = Path.Combine(projectDir, "bin", "Debug", "net8.0", "App.dll")
                    }
                },
                ProjectFolders = new System.Collections.Generic.Dictionary<string, string>()
            };

            (_, bool hasAnyOutputs, bool hasMissingOutputs) = InvokeCollectWorkspaceAssemblies(workspace);

            Assert.False(hasAnyOutputs);
            Assert.True(hasMissingOutputs);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    private static (object? Result, bool HasAnyOutputs, bool HasMissingOutputs)
        InvokeCollectWorkspaceAssemblies(WorkspaceModel workspace)
    {
        using MainWindowViewModel viewModel = new();
        MethodInfo? method = typeof(MainWindowViewModel).GetMethod(
            "CollectWorkspaceAssemblies",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        object?[] args = { workspace, null, null };
        object? result = method!.Invoke(viewModel, args);
        bool hasAnyOutputs = args[1] is bool any && any;
        bool hasMissingOutputs = args[2] is bool missing && missing;
        return (result, hasAnyOutputs, hasMissingOutputs);
    }

    private static string[] GetAssemblySetPaths(object? result, string propertyName)
    {
        Assert.NotNull(result);
        PropertyInfo? property = result!.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        object? value = property!.GetValue(result);
        Assert.NotNull(value);
        return ((System.Collections.Generic.IEnumerable<string>)value!).ToArray();
    }
}
