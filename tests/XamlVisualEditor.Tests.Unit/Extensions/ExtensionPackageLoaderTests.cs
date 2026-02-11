using System.IO.Compression;
using System.Text;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionPackageLoaderTests
{
    [Fact]
    public async Task LoadAsync_ReadsManifestFromNupkg()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "xve-pkg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string packagePath = Path.Combine(tempRoot, "sample.1.0.0.nupkg");

        try
        {
            using (ZipArchive zip = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = zip.CreateEntry("xve.extension.json");
                using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
                await writer.WriteAsync("{" +
                    "\"name\":\"sample-extension\"," +
                    "\"publisher\":\"example\"," +
                    "\"version\":\"1.0.0\"," +
                    "\"main\":\"lib/net10.0/Sample.Extension.dll\"" +
                    "}");
            }

            ExtensionPackageLoader loader = new();
            ExtensionPackageInfo info = await loader.LoadAsync(packagePath, CancellationToken.None);

            Assert.Equal("sample-extension", info.Manifest.Name);
            Assert.Equal("example", info.Manifest.Publisher);
            Assert.Equal("1.0.0", info.Manifest.Version);
            Assert.Equal("example.sample-extension", info.Manifest.ExtensionId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
