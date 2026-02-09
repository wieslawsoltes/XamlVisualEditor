using System.Linq;
using Xunit;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Tests.Unit;

public sealed class TimelineTickBuilderTests
{
    [Fact]
    public void BuildTicksIncludesStartAndEnd()
    {
        var ticks = TimelineTickBuilder.BuildTicks(1.0, 100.0);
        Assert.NotEmpty(ticks);

        TimelineTickViewModel first = ticks.First();
        TimelineTickViewModel last = ticks.Last();

        Assert.Equal(0.0, first.TimeSeconds, 3);
        Assert.True(last.TimeSeconds >= 1.0 - 0.0001);
    }

    [Fact]
    public void BuildTicksCreatesMajorLabels()
    {
        var ticks = TimelineTickBuilder.BuildTicks(2.0, 120.0);
        var labels = ticks.Where(t => !string.IsNullOrWhiteSpace(t.Label)).ToList();

        Assert.Contains(labels, t => t.Label == "0.0s" || t.Label == "0s");
        Assert.Contains(labels, t => t.Label.Contains("1", System.StringComparison.Ordinal));
    }
}
