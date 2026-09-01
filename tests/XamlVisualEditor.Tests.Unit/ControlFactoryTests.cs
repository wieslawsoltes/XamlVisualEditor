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

    [Fact]
    public void CreateControlTree_AppliesGridDefinitionAttributes()
    {
        const string xaml = """
            <Grid xmlns="https://github.com/avaloniaui"
                  RowDefinitions="Auto,Auto,*"
                  ColumnDefinitions="Auto,2*">
              <Button Grid.Row="2" Grid.Column="1" />
            </Grid>
            """;
        XamlParsingService parser = new();
        ParseResult result = parser.Parse(xaml, new XamlParserOptions { UseTolerantParser = false });
        MutableAstDocument document = Assert.IsType<MutableAstDocument>(result.Document);
        ControlFactory factory = new();

        Grid grid = Assert.IsType<Grid>(factory.CreateControlTree(document.Root!));

        Assert.Equal(3, grid.RowDefinitions.Count);
        Assert.True(grid.RowDefinitions[2].Height.IsStar);
        Assert.Equal(2, grid.ColumnDefinitions.Count);
        Assert.Equal(2, grid.ColumnDefinitions[1].Width.Value);
    }

    [Fact]
    public void CreateControlTree_AssignsPropertyElementControlContent()
    {
        const string xaml = """
            <Border xmlns="https://github.com/avaloniaui">
              <Border.Child>
                <TextBlock Text="content" />
              </Border.Child>
            </Border>
            """;
        XamlParsingService parser = new();
        ParseResult result = parser.Parse(xaml, new XamlParserOptions { UseTolerantParser = false });
        MutableAstDocument document = Assert.IsType<MutableAstDocument>(result.Document);
        ControlFactory factory = new();

        Border border = Assert.IsType<Border>(factory.CreateControlTree(document.Root!));

        TextBlock textBlock = Assert.IsType<TextBlock>(border.Child);
        Assert.Equal("content", textBlock.Text);
    }

    [Fact]
    public void CreateControlTree_SetsUnlistedStyledPropertyDynamically()
    {
        const string xaml = """
            <ScrollViewer xmlns="https://github.com/avaloniaui"
                          HorizontalScrollBarVisibility="Visible" />
            """;
        XamlParsingService parser = new();
        ParseResult result = parser.Parse(xaml, new XamlParserOptions { UseTolerantParser = false });
        MutableAstDocument document = Assert.IsType<MutableAstDocument>(result.Document);
        ControlFactory factory = new();

        ScrollViewer viewer = Assert.IsType<ScrollViewer>(factory.CreateControlTree(document.Root!));

        Assert.Equal(Avalonia.Controls.Primitives.ScrollBarVisibility.Visible, viewer.HorizontalScrollBarVisibility);
    }

    [Fact]
    public void ApplyProperties_SetsClrPropertiesOfCustomControls()
    {
        BodySurfaceControl control = new();
        MutableAstObjectNode node = new()
        {
            TypeName = "BodySurfaceControl",
            XmlNamespace = "clr-namespace:XamlVisualEditor.Tests.Unit",
            Properties =
            {
                new MutableAstPropertyNode
                {
                    PropertyName = "Title",
                    Value = new MutableAstTextNode { Text = "header" }
                },
                new MutableAstPropertyNode
                {
                    PropertyName = "BodySurfaceControl.Body",
                    Value = new MutableAstObjectNode
                    {
                        TypeName = "TextBlock",
                        XmlNamespace = "https://github.com/avaloniaui",
                        Properties =
                        {
                            new MutableAstPropertyNode
                            {
                                PropertyName = "Text",
                                Value = new MutableAstTextNode { Text = "body" }
                            }
                        }
                    }
                }
            }
        };
        ControlFactory factory = new();

        factory.ApplyProperties(control, node);

        Assert.Equal("header", control.Title);
        TextBlock body = Assert.IsType<TextBlock>(control.Body);
        Assert.Equal("body", body.Text);
    }

    private sealed class BodySurfaceControl : ContentControl
    {
        public string Title { get; set; } = string.Empty;

        public Control? Body { get; set; }
    }
}
