using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class HostServicesTests
{
    [Fact]
    public async Task CommandRegistryRegistersAndExecutes()
    {
        var registry = new CommandRegistry();
        bool invoked = false;

        using IDisposable reg = registry.Register("test.command", _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        await registry.ExecuteAsync("test.command", null, CancellationToken.None);

        Assert.True(invoked);
    }

    [Fact]
    public async Task CommandRegistryUnregisters()
    {
        var registry = new CommandRegistry();

        IDisposable reg = registry.Register("test.command", _ => Task.CompletedTask);
        reg.Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.ExecuteAsync("test.command", null, CancellationToken.None));
    }

    [Fact]
    public async Task SettingsStoreUsesWorkspaceOverride()
    {
        var settings = new InMemorySettingsStore();

        await settings.UpdateAsync("setting", "user", SettingsTarget.User, CancellationToken.None);
        Assert.Equal("user", settings.Get<string>("setting"));

        await settings.UpdateAsync("setting", "workspace", SettingsTarget.Workspace, CancellationToken.None);
        Assert.Equal("workspace", settings.Get<string>("setting"));
    }

    [Fact]
    public async Task ExtensionStorageStoresValues()
    {
        var storage = new InMemoryExtensionStorage();

        await storage.SetAsync("key", 42, CancellationToken.None);
        int? value = await storage.GetAsync<int>("key", CancellationToken.None);

        Assert.Equal(42, value);

        await storage.RemoveAsync("key", CancellationToken.None);
        value = await storage.GetAsync<int>("key", CancellationToken.None);

        Assert.Null(value);
    }

    [Fact]
    public async Task WorkspaceFindsAndReadsFiles()
    {
        var workspace = new InMemoryWorkspace();
        byte[] content = Encoding.UTF8.GetBytes("hello");

        await workspace.WriteFileAsync("views/main.xaml", content, CancellationToken.None);
        await workspace.WriteFileAsync("readme.md", content, CancellationToken.None);

        var matches = await workspace.FindFilesAsync("**/*.xaml", null, CancellationToken.None);

        Assert.Single(matches);
        Assert.Equal("views/main.xaml", matches[0]);

        byte[] read = await workspace.ReadFileAsync("views/main.xaml", CancellationToken.None);
        Assert.Equal(content, read);
    }
}
