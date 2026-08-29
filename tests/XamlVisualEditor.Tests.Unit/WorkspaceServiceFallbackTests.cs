using System;
using System.IO;
using XamlVisualEditor.Core;
using XamlVisualEditor.Workspace;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class WorkspaceServiceFallbackTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "XamlVisualEditor-WorkspaceFallback-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateFallbackWorkspace_UsesSharedBuildOutput()
    {
        string productRoot = Path.Combine(_root, "ProductRoot");
        string projectDirectory = Path.Combine(productRoot, "src", "Sample.Product");
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, "Sample.Product.csproj");
        File.WriteAllText(
            projectPath,
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><AssemblyName>Sample.Product</AssemblyName><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        string outputDirectory = Path.Combine(productRoot, "build", "SampleApp", "Debug");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, "Sample.Product.dll");
        File.WriteAllBytes(outputPath, Array.Empty<byte>());

        WorkspaceModel workspace = WorkspaceService.CreateFallbackWorkspace(projectPath);

        ProjectModel project = Assert.Single(workspace.Projects);
        Assert.Equal(projectPath, project.ProjectPath);
        Assert.Equal(outputPath, project.OutputAssemblyPath);
        Assert.Equal("net10.0", project.TargetFramework);
        Assert.False(project.IsExecutable);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
