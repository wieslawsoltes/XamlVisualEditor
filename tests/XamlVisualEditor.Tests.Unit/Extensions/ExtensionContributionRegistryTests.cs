using System;
using Xunit;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionContributionRegistryTests
{
    [Fact]
    public void RegisterMenuItems_AddsItemsAndRaisesChanged()
    {
        var registry = new ExtensionContributionRegistry();
        int changedCount = 0;
        registry.Changed += (_, _) => changedCount++;

        using (registry.RegisterMenuItems("ext", new[]
        {
            new ExtensionMenuContribution("cmd.hello", "Hello", ExtensionMenuLocations.Extensions, "group")
        }))
        {
            Assert.Single(registry.MenuItems);
        }

        Assert.Empty(registry.MenuItems);
        Assert.Equal(2, changedCount);
    }

    [Fact]
    public void RegisterViews_ReplacesByExtension()
    {
        var registry = new ExtensionContributionRegistry();

        registry.RegisterViews("ext", new[]
        {
            new ExtensionViewContribution("view.one", "One", ExtensionViewType.Tree, ExtensionViewLocation.Left, 0)
        });

        registry.RegisterViews("ext", new[]
        {
            new ExtensionViewContribution("view.two", "Two", ExtensionViewType.Tree, ExtensionViewLocation.Right, 1)
        });

        Assert.Single(registry.ViewContributions);
        Assert.Equal("view.two", registry.ViewContributions[0].ViewId);
    }
}
