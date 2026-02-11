using XamlVisualEditor.Acp;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class AcpProfileTests
{
    [Fact]
    public void CreateClaudeProfileSetsDefaults()
    {
        AcpProfile profile = AcpProfile.CreateClaudeProfile();

        Assert.Equal("claude", profile.Id);
        Assert.Equal("claude", profile.Command);
        Assert.True(profile.IsBuiltIn);
        Assert.Equal("ANTHROPIC_API_KEY", profile.ApiKeyEnvVar);
        Assert.Equal("ANTHROPIC_MODEL", profile.ModelEnvVar);
    }
}
