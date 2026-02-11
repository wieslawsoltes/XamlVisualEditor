using XamlVisualEditor.Extensions.Hosting.VscodeCompat;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class VscodeCompatExtensionLocatorTests
{
    [Fact]
    public void ResolveExtensions_SelectsLatestVersion()
    {
        string root = Path.Combine(Path.GetTempPath(), "xve-vscode-compat", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        string extensionId = "publisher.sample";
        string older = Path.Combine(root, extensionId + "-1.0.0");
        string newer = Path.Combine(root, extensionId + "-2.1.0");
        Directory.CreateDirectory(older);
        Directory.CreateDirectory(newer);

        try
        {
            var locator = new VscodeCompatExtensionLocator();
            IReadOnlyList<string> results = locator.ResolveExtensions(root, new[] { extensionId });

            Assert.Single(results);
            Assert.Equal(newer, results[0]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
