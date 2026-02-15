using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class SettingsSchemaApiTests
{
    [Fact]
    public async Task UpdateAsync_ValidatesUsingRegisteredSchema()
    {
        InMemorySettingsStore settings = new();
        settings.RegisterSchema(new SettingsSectionSchema(
            "sample.section",
            "Sample",
            "Sample settings schema",
            "object",
            value =>
            {
                if (value is not string text || string.IsNullOrWhiteSpace(text))
                {
                    return new[] { new SettingsValidationIssue("Value must be a non-empty string.") };
                }

                return Array.Empty<SettingsValidationIssue>();
            }));

        await settings.UpdateAsync("sample.section", "ok", SettingsTarget.User, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            settings.UpdateAsync("sample.section", string.Empty, SettingsTarget.User, CancellationToken.None));
    }

    [Fact]
    public async Task SubscribeSection_EmitsTypedChangeEvents()
    {
        InMemorySettingsStore settings = new();
        string? observed = null;

        using IDisposable subscription = settings.SubscribeSection<string>(
            "sample.value",
            args => observed = args.Value);

        await settings.UpdateAsync("sample.value", "abc", SettingsTarget.Workspace, CancellationToken.None);

        Assert.Equal("abc", observed);
    }
}
