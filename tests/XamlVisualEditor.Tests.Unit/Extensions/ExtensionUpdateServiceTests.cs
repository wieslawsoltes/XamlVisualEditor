using System.IO.Compression;
using System.Text;
using XamlVisualEditor.Extensions.Hosting;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_FindsNewerPackages()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "xve-update-" + Guid.NewGuid().ToString("N"));
        string installRoot = Path.Combine(tempRoot, "installed");
        string catalogRoot = Path.Combine(tempRoot, "catalog");
        Directory.CreateDirectory(tempRoot);

        try
        {
            string installedPkg = CreatePackage(tempRoot, "sample-extension", "example", "1.0.0");
            string availablePkg = CreatePackage(catalogRoot, "sample-extension", "example", "1.1.0");

            ExtensionPackageLoader loader = new();
            ExtensionPackageStore store = new(installRoot, loader);
            await store.InstallAsync(installedPkg, CancellationToken.None);

            LocalExtensionPackageCatalog catalog = new(catalogRoot, loader);
            ExtensionUpdateService service = new(store, catalog);

            IReadOnlyList<ExtensionUpdateInfo> updates =
                await service.CheckForUpdatesAsync(CancellationToken.None);

            Assert.Single(updates);
            Assert.Equal("1.1.0", updates[0].Available.Manifest.Version);
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
        Directory.CreateDirectory(root);
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
