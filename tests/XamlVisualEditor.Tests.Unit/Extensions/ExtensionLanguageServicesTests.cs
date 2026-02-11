using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionLanguageServicesTests
{
    [Fact]
    public async Task CompletionProviders_AreAggregated()
    {
        ExtensionLanguageServiceRegistry registry = new();
        ExtensionLanguageIntellisenseService service = new(registry);

        using IDisposable registration = registry.RegisterCompletionProvider("xaml", new TestCompletionProvider());

        CompletionContext context = new()
        {
            Offset = 1,
            TextBefore = "<",
            DocumentText = "<",
            FilePath = "test.xaml",
            LanguageId = "xaml",
            Trigger = CompletionTrigger.Invoked,
            TriggerCharacter = null
        };

        IReadOnlyList<CompletionItem> items = await service.GetCompletionsAsync(context, CancellationToken.None);

        Assert.Single(items);
        Assert.Equal("Test", items[0].DisplayText);
    }

    private sealed class TestCompletionProvider : IExtensionCompletionProvider
    {
        public Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(CompletionContext context, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<CompletionItem>>(new[]
            {
                new CompletionItem
                {
                    DisplayText = "Test",
                    InsertText = "Test",
                    Kind = CompletionItemKind.Keyword
                }
            });
        }
    }
}
