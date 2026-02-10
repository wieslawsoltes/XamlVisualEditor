using System.Linq;
using Xunit;
using XamlVisualEditor.Animation;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Xaml.Parsing;
using XamlVisualEditor.Xaml.Serialization;

namespace XamlVisualEditor.Tests.Unit;

public sealed class AnimationResourceWriterTests
{
    [Fact]
    public void AddsAnimationResourceToDocumentResources()
    {
        const string source = """
    <UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Samples.MainView">
      <Grid />
    </UserControl>
    """;

        XamlParsingService parser = new();
        ParseResult result = parser.Parse(source);
        Assert.NotNull(result.Document);

        MutableAstDocument document = (MutableAstDocument)result.Document!;
        AnimationTrackModel track = new()
        {
            PropertyName = "Opacity"
        };
        track.Keyframes.Add(new AnimationKeyframeModel { TimeSeconds = 0, Value = "0" });
        track.Keyframes.Add(new AnimationKeyframeModel { TimeSeconds = 1, Value = "1" });

        AnimationResourceDefinition definition = new(
            key: "FadeIn",
            durationSeconds: 1.0,
            tracks: new[] { track },
            scope: AnimationResourceScope.DocumentResources);

        bool added = AnimationResourceWriter.TryAddAnimationResource(document, definition, out string? error);
        Assert.True(added, error);

        XamlSerializationService serializer = new();
        string updated = serializer.Serialize(document);
        Assert.Contains("UserControl.Resources", updated);
        Assert.Contains("Animation", updated);
        Assert.Contains("x:Key=\"FadeIn\"", updated);
        Assert.Contains("KeyFrame", updated);
    }

    [Fact]
    public void AddsAnimationResourceToSelectedElementResources()
    {
        const string source = """
    <UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Samples.MainView">
      <Border />
    </UserControl>
    """;

        XamlParsingService parser = new();
        ParseResult result = parser.Parse(source);
        Assert.NotNull(result.Document);

        MutableAstDocument document = (MutableAstDocument)result.Document!;
        MutableAstObjectNode border = document.Root!.Children.OfType<MutableAstObjectNode>().First();

        AnimationTrackModel track = new()
        {
            PropertyName = "Width"
        };
        track.Keyframes.Add(new AnimationKeyframeModel { TimeSeconds = 0.5, Value = "120" });

        AnimationResourceDefinition definition = new(
            key: "Grow",
            durationSeconds: 1.0,
            tracks: new[] { track },
            scope: AnimationResourceScope.SelectedElementResources,
            scopeOwner: border);

        bool added = AnimationResourceWriter.TryAddAnimationResource(document, definition, out string? error);
        Assert.True(added, error);

        XamlSerializationService serializer = new();
        string updated = serializer.Serialize(document);
        Assert.Contains("Border.Resources", updated);
        Assert.Contains("x:Key=\"Grow\"", updated);
    }
}
