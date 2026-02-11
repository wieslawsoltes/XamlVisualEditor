using System.IO.Compression;
using System.Text;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionPackageStoreTests
{
    [Fact]
    public async Task InstallAsync_CopiesPackage()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "xve-store-" + Guid.NewGuid().ToString("N"));
        string installRoot = Path.Combine(tempRoot, "installed");
        Directory.CreateDirectory(tempRoot);

        try
        {
            string packagePath = CreatePackage(tempRoot, "sample-extension", "example", "1.0.0");

            ExtensionPackageLoader loader = new();
            ExtensionPackageStore store = new(installRoot, loader);

            ExtensionPackageInfo installed = await store.InstallAsync(packagePath, CancellationToken.None);

            Assert.Equal("example.sample-extension", installed.Manifest.ExtensionId);
            Assert.True(Directory.Exists(installRoot));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string CreatePackage(string root, string name, string publisher, string version)
    {
        string packagePath = Path.Combine(root, name + "." + version + ".nupkg");
        using ZipArchive zip = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        ZipArchiveEntry entry = zip.CreateEntry("xve.extension.json");
        using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
        writer.Write("{" +
            "\"name\":\"" + name + "\"," +
            "\"publisher\":\"" + publisher + "\"," +
            "\"version\":\"" + version + "\"" +
            "}");

        return packagePath;
    }
}
