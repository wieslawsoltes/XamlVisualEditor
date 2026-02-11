using XamlVisualEditor.Extensions.Hosting;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionStateStoreTests
{
    [Fact]
    public async Task StateStore_PersistsEnabledFlag()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "xve-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string statePath = Path.Combine(tempRoot, "state.json");

        try
        {
            FileExtensionStateStore store = new(statePath);
            await store.SetEnabledAsync("example.sample", true, CancellationToken.None);

            bool enabled = await store.GetEnabledAsync("example.sample", CancellationToken.None);
            Assert.True(enabled);
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
