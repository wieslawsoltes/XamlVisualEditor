using Avalonia.Controls;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Designer.Rendering;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Xaml.Parsing;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class ControlFactoryTests
{
    [Fact]
    public void CreateControlTree_AppliesGridDefinitionPropertyElements()
    {
        const string xaml = """
            <Grid xmlns="https://github.com/avaloniaui">
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="2*" MinWidth="10" />
              </Grid.ColumnDefinitions>
              <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" MinHeight="20" />
              </Grid.RowDefinitions>
              <Button Grid.Row="1" Grid.Column="1" />
            </Grid>
            """;
        XamlParsingService parser = new();
        ParseResult result = parser.Parse(xaml, new XamlParserOptions { UseTolerantParser = false });
        MutableAstDocument document = Assert.IsType<MutableAstDocument>(result.Document);
        ControlFactory factory = new();

        Grid grid = Assert.IsType<Grid>(factory.CreateControlTree(document.Root!));

        Assert.Equal(2, grid.ColumnDefinitions.Count);
        Assert.True(grid.ColumnDefinitions[0].Width.IsAuto);
        Assert.True(grid.ColumnDefinitions[1].Width.IsStar);
        Assert.Equal(2, grid.ColumnDefinitions[1].Width.Value);
        Assert.Equal(10, grid.ColumnDefinitions[1].MinWidth);
        Assert.Equal(2, grid.RowDefinitions.Count);
        Assert.True(grid.RowDefinitions[0].Height.IsAuto);
        Assert.True(grid.RowDefinitions[1].Height.IsStar);
        Assert.Equal(20, grid.RowDefinitions[1].MinHeight);
        Button button = Assert.IsType<Button>(Assert.Single(grid.Children));
        Assert.Equal(1, Grid.GetRow(button));
        Assert.Equal(1, Grid.GetColumn(button));
    }
}
