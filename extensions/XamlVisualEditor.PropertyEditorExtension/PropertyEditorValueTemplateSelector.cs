using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.PropertyEditorExtension;

public sealed class PropertyEditorValueTemplateSelector : IDataTemplate
{
    [Content]
    public IDataTemplate? TextEditor { get; set; }

    public IDataTemplate? BooleanEditor { get; set; }

    public IDataTemplate? NumberEditor { get; set; }

    public IDataTemplate? EnumEditor { get; set; }

    public IDataTemplate? BrushEditor { get; set; }

    public IDataTemplate? ColorEditor { get; set; }

    public IDataTemplate? ThicknessEditor { get; set; }

    public IDataTemplate? CornerRadiusEditor { get; set; }

    public IDataTemplate? PointEditor { get; set; }

    public IDataTemplate? SizeEditor { get; set; }

    public IDataTemplate? RectEditor { get; set; }

    public IDataTemplate? GridLengthEditor { get; set; }

    public IDataTemplate? FontFamilyEditor { get; set; }

    public IDataTemplate? FontWeightEditor { get; set; }

    public IDataTemplate? FontStyleEditor { get; set; }

    public IDataTemplate? TimeSpanEditor { get; set; }

    public IDataTemplate? UriEditor { get; set; }

    public IDataTemplate? CollectionEditor { get; set; }

    public IDataTemplate? TemplateEditor { get; set; }

    public IDataTemplate? MarkupExtensionEditor { get; set; }

    public bool Match(object? data)
    {
        return data is PropertyEntryViewModel;
    }

    public Control? Build(object? data)
    {
        if (data is not PropertyEntryViewModel entry)
        {
            return null;
        }

        IDataTemplate? template = entry.EditorKind switch
        {
            PropertyEditorKind.Boolean => BooleanEditor,
            PropertyEditorKind.Number => NumberEditor,
            PropertyEditorKind.Enum => EnumEditor,
            PropertyEditorKind.Brush => BrushEditor,
            PropertyEditorKind.Color => ColorEditor,
            PropertyEditorKind.Thickness => ThicknessEditor,
            PropertyEditorKind.CornerRadius => CornerRadiusEditor,
            PropertyEditorKind.Point => PointEditor,
            PropertyEditorKind.Size => SizeEditor,
            PropertyEditorKind.Rect => RectEditor,
            PropertyEditorKind.GridLength => GridLengthEditor,
            PropertyEditorKind.FontFamily => FontFamilyEditor,
            PropertyEditorKind.FontWeight => FontWeightEditor,
            PropertyEditorKind.FontStyle => FontStyleEditor,
            PropertyEditorKind.TimeSpan => TimeSpanEditor,
            PropertyEditorKind.Uri => UriEditor,
            PropertyEditorKind.Collection => CollectionEditor,
            PropertyEditorKind.Template => TemplateEditor,
            PropertyEditorKind.MarkupExtension => MarkupExtensionEditor,
            _ => TextEditor
        };

        return (template ?? TextEditor)?.Build(data);
    }
}
