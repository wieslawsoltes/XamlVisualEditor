using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class InstalledExtensionPackagesTests
{
    [Fact]
    public void ExtractContent_ExtractsArchiveNextToPackage()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string packagePath = Path.Combine(root, "sample.nupkg");
            using (ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("payload.txt");
                using StreamWriter writer = new(entry.Open());
                writer.Write("payload");
            }

            string contentDirectory = InstalledExtensionPackages.ExtractContent(packagePath);

            Assert.Equal(Path.Combine(root, "content"), contentDirectory);
            Assert.Equal("payload", File.ReadAllText(Path.Combine(contentDirectory, "payload.txt")));

            // A second call reuses the existing directory without failing.
            Assert.Equal(contentDirectory, InstalledExtensionPackages.ExtractContent(packagePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveMainAssemblyPath_RejectsMissingAndEscapingEntries()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string assemblyPath = Path.Combine(root, "extension.dll");
            File.WriteAllBytes(assemblyPath, new byte[] { 0x4D, 0x5A });

            Assert.Equal(assemblyPath, InstalledExtensionPackages.ResolveMainAssemblyPath(root, "extension.dll"));
            Assert.Null(InstalledExtensionPackages.ResolveMainAssemblyPath(root, "missing.dll"));
            Assert.Null(InstalledExtensionPackages.ResolveMainAssemblyPath(root, "..\\extension.dll"));
            Assert.Null(InstalledExtensionPackages.ResolveMainAssemblyPath(root, null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindExtensionTypes_FindsPublicParameterlessImplementations()
    {
        IReadOnlyList<Type> types = InstalledExtensionPackages.FindExtensionTypes(typeof(InstalledExtensionPackagesTests).Assembly);

        Assert.Contains(typeof(SampleInstalledExtension), types);
        Assert.DoesNotContain(typeof(SampleExtensionWithoutDefaultConstructor), types);
    }
}

public sealed class SampleInstalledExtension : IXveExtension
{
    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class SampleExtensionWithoutDefaultConstructor : IXveExtension
{
    public SampleExtensionWithoutDefaultConstructor(string _)
    {
    }

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
