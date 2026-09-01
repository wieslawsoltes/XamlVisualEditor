using XamlVisualEditor.Designer.Core;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class DesignCanvasSizingTests
{
    [Fact]
    public void Compute_UsesDefaults_WhenNothingIsDeclared()
    {
        (double width, double height) = DesignCanvasSizing.Compute(null, null, null, null, null, null);

        Assert.Equal(DesignCanvasSizing.DefaultWidth, width);
        Assert.Equal(DesignCanvasSizing.DefaultHeight, height);
    }

    [Fact]
    public void Compute_ExplicitSizeWins_OverMeasuredContent()
    {
        (double width, double height) = DesignCanvasSizing.Compute(400, 300, 580, 772, 620, 1250);

        Assert.Equal(400, width);
        Assert.Equal(300, height);
    }

    [Fact]
    public void Compute_GrowsMinimumSizeToMeasuredContent()
    {
        (double width, double height) = DesignCanvasSizing.Compute(null, null, 580, 772, 620, 1250);

        Assert.Equal(620, width);
        Assert.Equal(1250, height);
    }

    [Fact]
    public void Compute_KeepsMinimum_WhenContentIsSmaller()
    {
        (double width, double height) = DesignCanvasSizing.Compute(null, null, 580, 772, 320, 200);

        Assert.Equal(580, width);
        Assert.Equal(772, height);
    }

    [Fact]
    public void Compute_IgnoresInfiniteMeasurement()
    {
        (double width, double height) = DesignCanvasSizing.Compute(null, null, null, null, double.PositiveInfinity, double.PositiveInfinity);

        Assert.Equal(DesignCanvasSizing.DefaultWidth, width);
        Assert.Equal(DesignCanvasSizing.DefaultHeight, height);
    }
}
