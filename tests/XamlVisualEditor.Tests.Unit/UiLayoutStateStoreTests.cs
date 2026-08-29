using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using XamlVisualEditor.App.Services;
using XamlVisualEditor.App.Views;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class UiLayoutStateStoreTests
{
    [AvaloniaFact]
    public void AttachedBehaviorsStoreTheirConfiguration()
    {
        Window window = new();
        Grid grid = new();

        WindowLayoutBehavior.SetIsEnabled(window, true);
        SplitterLayoutBehavior.SetPersistenceKey(grid, "DesignerDocument");

        Assert.True(WindowLayoutBehavior.GetIsEnabled(window));
        Assert.Equal("DesignerDocument", SplitterLayoutBehavior.GetPersistenceKey(grid));
    }

    [Fact]
    public void SerializeRoundTrip_PreservesWindowAndSplitterLayout()
    {
        UiLayoutState state = new()
        {
            WindowWidth = 1512,
            WindowHeight = 976,
            Splitters =
            {
                ["DesignerDocument"] = new SplitterLayoutState
                {
                    ColumnRatio = 0.62,
                    RowRatio = 0.71
                }
            }
        };

        UiLayoutState restored = UiLayoutStateStore.Deserialize(UiLayoutStateStore.Serialize(state));

        Assert.Equal(1512, restored.WindowWidth);
        Assert.Equal(976, restored.WindowHeight);
        Assert.Equal(0.62, restored.Splitters["DesignerDocument"].ColumnRatio);
        Assert.Equal(0.71, restored.Splitters["DesignerDocument"].RowRatio);
    }

    [Theory]
    [InlineData(900, 300, 0.75)]
    [InlineData(0, 0, 0.5)]
    public void CalculateRatio_ReturnsStableStarRatio(double first, double second, double expected)
    {
        Assert.Equal(expected, SplitterLayoutBehavior.CalculateRatio(first, second));
    }
}
