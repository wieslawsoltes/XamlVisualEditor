using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionViewRegistryTests
{
    [Fact]
    public void RegisterTreeProvider_MakesProviderAvailable()
    {
        var registry = new ExtensionViewRegistry();
        var provider = new TestTreeProvider();

        using (registry.RegisterTreeDataProvider("view.tree", provider))
        {
            Assert.True(registry.TryGetTreeProvider("view.tree", out IExtensionTreeDataProvider? resolved));
            Assert.NotNull(resolved);
        }

        Assert.False(registry.TryGetTreeProvider("view.tree", out _));
    }

    private sealed class TestTreeProvider : ITreeDataProvider<string>
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public Task<IReadOnlyList<string>> GetChildrenAsync(string? element, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task<TreeItem> GetTreeItemAsync(string element, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TreeItem(element, null, null));
        }
    }
}
