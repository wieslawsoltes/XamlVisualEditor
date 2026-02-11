using XamlVisualEditor.Extensions;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionSdkTypesTests
{
    [Fact]
    public void InputBoxOptions_StoresValues()
    {
        var options = new InputBoxOptions("Title", "Prompt", "Value");

        Assert.Equal("Title", options.Title);
        Assert.Equal("Prompt", options.Prompt);
        Assert.Equal("Value", options.Value);
    }

    [Fact]
    public void QuickPickItem_StoresValues()
    {
        var item = new QuickPickItem("Label", "Desc", "Detail");

        Assert.Equal("Label", item.Label);
        Assert.Equal("Desc", item.Description);
        Assert.Equal("Detail", item.Detail);
    }

    [Fact]
    public void ConfigurationChangedEventArgs_StoresSection()
    {
        var args = new ConfigurationChangedEventArgs("sample.section");

        Assert.Equal("sample.section", args.Section);
    }

    [Fact]
    public void TreeItem_StoresValues()
    {
        var item = new TreeItem("Label", "Desc", "Context");

        Assert.Equal("Label", item.Label);
        Assert.Equal("Desc", item.Description);
        Assert.Equal("Context", item.ContextValue);
    }
}
