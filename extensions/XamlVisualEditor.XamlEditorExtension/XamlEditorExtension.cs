using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.XamlEditorExtension;

public sealed class XamlEditorExtension : IXveExtension
{
    private static readonly string[] DefaultBrushPresets =
    [
        "Transparent",
        "Black",
        "White",
        "Red",
        "Green",
        "Blue",
        "Yellow",
        "Orange",
        "Purple",
        "Gray",
        "#FF1E1E1E",
        "#FF2D2D2D",
        "#FF3C3C3C",
        "#FF007ACC"
    ];

    private static readonly ExtensionPropertyEditorContribution[] Contributions =
    [
        new("HorizontalAlignment", PropertyEditorKind.Enum, new[] { "Left", "Center", "Right", "Stretch" }),
        new("VerticalAlignment", PropertyEditorKind.Enum, new[] { "Top", "Center", "Bottom", "Stretch" }),
        new("Dock", PropertyEditorKind.Enum, new[] { "Left", "Right", "Top", "Bottom" }),
        new("Orientation", PropertyEditorKind.Enum, new[] { "Horizontal", "Vertical" }),
        new("TextWrapping", PropertyEditorKind.Enum, new[] { "NoWrap", "Wrap", "WrapWithOverflow" }),
        new("TextTrimming", PropertyEditorKind.Enum, new[] { "None", "CharacterEllipsis", "WordEllipsis" }),
        new("HorizontalScrollBarVisibility", PropertyEditorKind.Enum, new[] { "Disabled", "Hidden", "Auto", "Visible" }),
        new("VerticalScrollBarVisibility", PropertyEditorKind.Enum, new[] { "Disabled", "Hidden", "Auto", "Visible" }),
        new("Stretch", PropertyEditorKind.Enum, new[] { "None", "Fill", "Uniform", "UniformToFill" }),
        new("StretchDirection", PropertyEditorKind.Enum, new[] { "Both", "UpOnly", "DownOnly" }),
        new("Brush", PropertyEditorKind.Brush, BrushPresets: DefaultBrushPresets),
        new("IBrush", PropertyEditorKind.Brush, BrushPresets: DefaultBrushPresets),
        new("SolidColorBrush", PropertyEditorKind.Brush, BrushPresets: DefaultBrushPresets),
        new("Color", PropertyEditorKind.Color, BrushPresets: DefaultBrushPresets),
        new("Thickness", PropertyEditorKind.Thickness),
        new("CornerRadius", PropertyEditorKind.CornerRadius),
        new("Point", PropertyEditorKind.Point),
        new("Size", PropertyEditorKind.Size),
        new("Rect", PropertyEditorKind.Rect),
        new("GridLength", PropertyEditorKind.GridLength),
        new("FontFamily", PropertyEditorKind.FontFamily),
        new("FontWeight", PropertyEditorKind.FontWeight),
        new("FontStyle", PropertyEditorKind.FontStyle),
        new("TimeSpan", PropertyEditorKind.TimeSpan),
        new("Uri", PropertyEditorKind.Uri),
        new("ControlTemplate", PropertyEditorKind.Template),
        new("DataTemplate", PropertyEditorKind.Template),
        new("MarkupExtension", PropertyEditorKind.MarkupExtension)
    ];

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        context.Subscriptions.Add(context.Contributions.RegisterPropertyEditors(
            context.ExtensionId,
            Contributions));

        foreach (ExtensionPropertyEditorContribution contribution in Contributions)
        {
            context.Subscriptions.Add(context.PropertyEditors.Register(new PropertyEditorDescriptor(
                contribution.PropertyType,
                contribution.Kind,
                contribution.EnumOptions,
                contribution.BrushPresets)));
        }

        return Task.CompletedTask;
    }
}
