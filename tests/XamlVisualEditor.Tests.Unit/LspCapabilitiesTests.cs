using XamlVisualEditor.Core.Lsp;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class LspCapabilitiesTests
{
    [Fact]
    public void SupportsReturnsFalseWhenDisabled()
    {
        LspServerCapabilities caps = new()
        {
            CompletionProvider = false,
            HoverProvider = false,
            SignatureHelpProvider = false,
            DefinitionProvider = false,
            ReferencesProvider = false,
            SemanticTokensProvider = false
        };

        Assert.False(caps.Supports(LspFeature.Completion));
        Assert.False(caps.Supports(LspFeature.Hover));
        Assert.False(caps.Supports(LspFeature.SignatureHelp));
        Assert.False(caps.Supports(LspFeature.Definition));
        Assert.False(caps.Supports(LspFeature.References));
        Assert.False(caps.Supports(LspFeature.SemanticTokens));
    }

    [Fact]
    public void SupportsDefaultsToTrueWhenUnset()
    {
        LspServerCapabilities caps = new();

        Assert.True(caps.Supports(LspFeature.Completion));
        Assert.True(caps.Supports(LspFeature.Hover));
        Assert.True(caps.Supports(LspFeature.SignatureHelp));
        Assert.True(caps.Supports(LspFeature.Definition));
        Assert.True(caps.Supports(LspFeature.References));
        Assert.True(caps.Supports(LspFeature.SemanticTokens));
    }
}
