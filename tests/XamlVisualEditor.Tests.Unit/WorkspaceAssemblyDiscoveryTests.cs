using System;
using System.Collections.Generic;
using System.IO;
using XamlVisualEditor.Core;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class WorkspaceAssemblyDiscoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "XamlVisualEditor-WorkspaceAssemblyDiscovery-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FindProjectOutputs_UsesOnlyTopLevelSharedOutput()
    {
        string projectDirectory = CreateDirectory("src", "Sample");
        string outputDirectory = CreateDirectory("shared-output");
        string outputPath = CreateFile(outputDirectory, "Sample.dll");
        string runtimeDependency = CreateFile(outputDirectory, "RuntimeDependency.dll");
        CreateFile(outputDirectory, "Sample.resources.dll");
        CreateFile(CreateDirectory("shared-output", "de"), "Unrelated.resources.dll");
        CreateFile(CreateDirectory("shared-output", "nested"), "NestedDependency.dll");

        ProjectModel project = CreateProject(projectDirectory, outputPath);

        IReadOnlyList<string> outputs = WorkspaceAssemblyDiscovery.FindProjectOutputs(project);

        Assert.Equal(2, outputs.Count);
        Assert.Contains(Path.GetFullPath(outputPath), outputs);
        Assert.Contains(Path.GetFullPath(runtimeDependency), outputs);
    }

    [Fact]
    public void FindProjectOutputs_FallbackSkipsRuntimeAndReferenceCopies()
    {
        string projectDirectory = CreateDirectory("src", "Sample");
        string outputDirectory = CreateDirectory("shared-output");
        string expected = CreateFile(CreateDirectory("shared-output", "net10.0"), "Sample.dll");
        CreateFile(CreateDirectory("shared-output", "runtimes", "win", "lib", "net10.0"), "Sample.dll");
        CreateFile(CreateDirectory("shared-output", "ref", "net10.0"), "Sample.dll");
        string missingOutput = Path.Combine(outputDirectory, "Sample.dll");

        ProjectModel project = CreateProject(projectDirectory, missingOutput);

        IReadOnlyList<string> outputs = WorkspaceAssemblyDiscovery.FindProjectOutputs(project);

        string discovered = Assert.Single(outputs);
        Assert.Equal(Path.GetFullPath(expected), discovered);
    }

    [Fact]
    public void FindProjectOutputs_ExecutableWithoutOutputPathFindsManagedAssembly()
    {
        string projectDirectory = CreateDirectory("src", "Sample");
        string expected = CreateFile(CreateDirectory("src", "Sample", "bin", "Debug", "net10.0"), "Sample.dll");
        ProjectModel project = CreateProject(projectDirectory, outputAssemblyPath: null, isExecutable: true);

        IReadOnlyList<string> outputs = WorkspaceAssemblyDiscovery.FindProjectOutputs(project);

        string discovered = Assert.Single(outputs);
        Assert.Equal(Path.GetFullPath(expected), discovered);
    }

    [Fact]
    public void FindProjectOutputs_PreservesCaseDistinctFilesOnCaseSensitiveSystems()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string projectDirectory = CreateDirectory("src", "Sample");
        string outputDirectory = CreateDirectory("shared-output");
        string outputPath = CreateFile(outputDirectory, "Sample.dll");
        CreateFile(outputDirectory, "sample.dll");
        ProjectModel project = CreateProject(projectDirectory, outputPath);

        IReadOnlyList<string> outputs = WorkspaceAssemblyDiscovery.FindProjectOutputs(project);

        Assert.Equal(2, outputs.Count);
    }

    [Theory]
    [InlineData("de/Sample.resources.dll")]
    [InlineData("runtimes/win/lib/net10.0/Sample.dll")]
    [InlineData("ref/net10.0/Sample.dll")]
    [InlineData("refint/net10.0/Sample.dll")]
    public void IsIgnoredOutputPath_RejectsNonDesignAssemblies(string relativePath)
    {
        string path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(WorkspaceAssemblyDiscovery.IsIgnoredOutputPath(path));
    }

    [Fact]
    public void IsIgnoredOutputPath_AcceptsProjectAssembly()
    {
        string path = Path.Combine(_root, "net10.0", "Sample.dll");

        Assert.False(WorkspaceAssemblyDiscovery.IsIgnoredOutputPath(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ProjectModel CreateProject(
        string projectDirectory,
        string? outputAssemblyPath,
        bool isExecutable = false)
    {
        return new ProjectModel
        {
            Name = "Sample",
            ProjectPath = Path.Combine(projectDirectory, "Sample.csproj"),
            XamlFiles = Array.Empty<XamlFileModel>(),
            Files = Array.Empty<ProjectFileModel>(),
            References = Array.Empty<AssemblyReference>(),
            OutputAssemblyPath = outputAssemblyPath,
            TargetFramework = "net10.0",
            IsExecutable = isExecutable
        };
    }

    private string CreateDirectory(params string[] segments)
    {
        string path = _root;
        foreach (string segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateFile(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, Array.Empty<byte>());
        return path;
    }
}
