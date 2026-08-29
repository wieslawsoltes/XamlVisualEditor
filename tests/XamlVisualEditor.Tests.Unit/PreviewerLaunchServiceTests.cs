using System;
using System.IO;
using XamlVisualEditor.Core;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class PreviewerLaunchServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "XamlVisualEditor-PreviewerLaunch-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResolveHostApplicationPath_UsesSharedOutputParentName()
    {
        string outputDirectory = Path.Combine(_root, "build", "SampleHost", "Debug");
        Directory.CreateDirectory(outputDirectory);
        string xamlAssembly = CreateFile(outputDirectory, "Library.dll");
        string hostAssembly = CreateHostApplication(outputDirectory, "SampleHost");

        string? resolved = PreviewerLaunchService.ResolveHostApplicationPath(
            xamlAssembly,
            Array.Empty<ProjectModel>(),
            appOverride: null);

        Assert.Equal(Path.GetFullPath(hostAssembly), resolved);
    }

    [Fact]
    public void ResolveHostApplicationPath_PrefersExplicitOverride()
    {
        string outputDirectory = Path.Combine(_root, "output");
        Directory.CreateDirectory(outputDirectory);
        string xamlAssembly = CreateFile(outputDirectory, "Library.dll");
        string overrideAssembly = CreateHostApplication(outputDirectory, "ExplicitHost");

        string? resolved = PreviewerLaunchService.ResolveHostApplicationPath(
            xamlAssembly,
            Array.Empty<ProjectModel>(),
            overrideAssembly);

        Assert.Equal(Path.GetFullPath(overrideAssembly), resolved);
    }

    [Fact]
    public void PreviewerTcpSession_ReportErrorPreservesVisibleError()
    {
        using PreviewerTcpSession session = new("Sample.axaml", log: null);
        PreviewerErrorInfo? received = null;
        session.ErrorReceived += error => received = error;

        session.ReportError("Host application has no entry point.");

        Assert.Equal("Host application has no entry point.", session.LastError);
        Assert.Equal("Host application has no entry point.", received?.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string CreateHostApplication(string directory, string baseName)
    {
        string assemblyPath = CreateFile(directory, baseName + ".dll");
        CreateFile(directory, baseName + ".runtimeconfig.json");
        CreateFile(directory, baseName + ".deps.json");
        return assemblyPath;
    }

    private static string CreateFile(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, Array.Empty<byte>());
        return path;
    }
}
