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

    /// <summary>Gets or sets the template for Thickness properties (4-field).</summary>
    public IDataTemplate? ThicknessEditor { get; set; }

    /// <summary>Gets or sets the template for CornerRadius properties (4-field).</summary>
    public IDataTemplate? CornerRadiusEditor { get; set; }

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
            PropertyKind.Thickness => ThicknessEditor,
            PropertyKind.CornerRadius => CornerRadiusEditor,
            _ => StringEditor
        };

        return (template ?? StringEditor)?.Build(data);
    }
}
