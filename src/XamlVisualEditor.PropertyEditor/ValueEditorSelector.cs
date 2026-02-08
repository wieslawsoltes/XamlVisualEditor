using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using XamlVisualEditor.Core;

namespace XamlVisualEditor.PropertyEditor;

/// <summary>
/// Selects the appropriate value editor DataTemplate based on <see cref="PropertyItemViewModel.Kind"/>.
/// </summary>
public sealed class ValueEditorSelector : IDataTemplate
{
    /// <summary>Gets or sets the template for string/CLR properties (default TextBox).</summary>
    [Content]
    public IDataTemplate? StringEditor { get; set; }

    /// <summary>Gets or sets the template for boolean properties (CheckBox).</summary>
    public IDataTemplate? BooleanEditor { get; set; }

    /// <summary>Gets or sets the template for numeric properties (NumericUpDown).</summary>
    public IDataTemplate? NumericEditor { get; set; }

    /// <summary>Gets or sets the template for enum properties (ComboBox).</summary>
    public IDataTemplate? EnumEditor { get; set; }

    /// <summary>Gets or sets the template for brush/color properties.</summary>
    public IDataTemplate? BrushEditor { get; set; }

    /// <summary>Gets or sets the template for color properties.</summary>
    public IDataTemplate? ColorEditor { get; set; }

    /// <summary>Gets or sets the template for Thickness properties (4-field).</summary>
    public IDataTemplate? ThicknessEditor { get; set; }

    /// <summary>Gets or sets the template for CornerRadius properties (4-field).</summary>
    public IDataTemplate? CornerRadiusEditor { get; set; }

    /// <summary>Gets or sets the template for Point properties.</summary>
    public IDataTemplate? PointEditor { get; set; }

    /// <summary>Gets or sets the template for Size properties.</summary>
    public IDataTemplate? SizeEditor { get; set; }

    /// <summary>Gets or sets the template for Rect properties.</summary>
    public IDataTemplate? RectEditor { get; set; }

    /// <summary>Gets or sets the template for GridLength properties.</summary>
    public IDataTemplate? GridLengthEditor { get; set; }

    /// <summary>Gets or sets the template for FontFamily properties.</summary>
    public IDataTemplate? FontFamilyEditor { get; set; }

    /// <summary>Gets or sets the template for FontWeight properties.</summary>
    public IDataTemplate? FontWeightEditor { get; set; }

    /// <summary>Gets or sets the template for FontStyle properties.</summary>
    public IDataTemplate? FontStyleEditor { get; set; }

    /// <summary>Gets or sets the template for TimeSpan properties.</summary>
    public IDataTemplate? TimeSpanEditor { get; set; }

    /// <summary>Gets or sets the template for Uri properties.</summary>
    public IDataTemplate? UriEditor { get; set; }

    /// <summary>Gets or sets the template for collection properties.</summary>
    public IDataTemplate? CollectionEditor { get; set; }

    /// <summary>Gets or sets the template for template properties.</summary>
    public IDataTemplate? TemplateEditor { get; set; }

    /// <summary>Gets or sets the template for markup extension properties.</summary>
    public IDataTemplate? MarkupExtensionEditor { get; set; }

    /// <inheritdoc/>
    public bool Match(object? data)
    {
        return data is PropertyItemViewModel;
    }

    /// <inheritdoc/>
    public Control? Build(object? data)
    {
        if (data is not PropertyItemViewModel prop)
        {
            return null;
        }

        IDataTemplate? template = prop.Kind switch
        {
            PropertyKind.Boolean => BooleanEditor,
            PropertyKind.Numeric => NumericEditor,
            PropertyKind.Enum => EnumEditor,
            PropertyKind.Brush => BrushEditor,
            PropertyKind.Color => ColorEditor,
            PropertyKind.Thickness => ThicknessEditor,
            PropertyKind.CornerRadius => CornerRadiusEditor,
            PropertyKind.Point => PointEditor,
            PropertyKind.Size => SizeEditor,
            PropertyKind.Rect => RectEditor,
            PropertyKind.GridLength => GridLengthEditor,
            PropertyKind.FontFamily => FontFamilyEditor,
            PropertyKind.FontWeight => FontWeightEditor,
            PropertyKind.FontStyle => FontStyleEditor,
            PropertyKind.TimeSpan => TimeSpanEditor,
            PropertyKind.Uri => UriEditor,
            PropertyKind.Collection => CollectionEditor,
            PropertyKind.Template => TemplateEditor,
            PropertyKind.MarkupExtension => MarkupExtensionEditor,
            _ => StringEditor
        };

        return (template ?? StringEditor)?.Build(data);
    }
}
