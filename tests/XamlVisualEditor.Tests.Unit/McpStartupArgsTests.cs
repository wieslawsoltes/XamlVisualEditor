using XamlVisualEditor.Extensions.Hosting.Mcp;
using XamlVisualEditor.McpExtension;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class McpStartupArgsTests
{
    [Fact]
    public void Apply_McpFlagEnablesLocalHttpTransport()
    {
        McpSettings settings = McpStartupArgs.Apply(new McpSettings(), new[] { "--mcp" });

        Assert.True(settings.Enabled);
        Assert.Equal("http", settings.Transport);
        Assert.Equal(4712, settings.HttpPort);
        Assert.Equal("/mcp/", settings.HttpPath);
    }

    [Fact]
    public void Apply_OverridesTransportPortAndPath()
    {
        string[] args =
        {
            "--mcp-transport", "both",
            "--mcp-port", "4812",
            "--mcp-path", "xve"
        };

        McpSettings settings = McpStartupArgs.Apply(new McpSettings(), args);

        Assert.True(settings.Enabled);
        Assert.Equal("both", settings.Transport);
        Assert.Equal(4812, settings.HttpPort);
        Assert.Equal("/xve/", settings.HttpPath);
    }
}
