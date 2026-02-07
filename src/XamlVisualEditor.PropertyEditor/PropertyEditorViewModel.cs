using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Designer.Core;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.PropertyEditor;

/// <summary>
/// Describes one editable property of a design item.
/// </summary>
public sealed class PropertyItemViewModel : ReactiveObject
{
    /// <summary>
    /// Gets the property name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the category (Layout, Appearance, Common, etc.).
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets the kind of property (e.g., String, Numeric, Boolean, Brush, Enum, Thickness, CornerRadius).
    /// </summary>
    public PropertyKind Kind { get; }

    /// <summary>
    /// Gets or sets the current value as a string.
    /// </summary>
    [Reactive]
    public string? Value { get; set; }

    /// <summary>
    /// Gets whether this property is currently set (vs. using default).
    /// </summary>
    [Reactive]
    public bool IsSet { get; set; }

    /// <summary>
    /// Gets the default value hint.
    /// </summary>
    public string? DefaultValueHint { get; init; }

    /// <summary>
    /// Gets the available enum values if the property is an enum type.
    /// </summary>
    public IReadOnlyList<string>? EnumValues { get; init; }

    /// <summary>
    /// Gets or sets whether this property is visible after filtering.
    /// </summary>
    [Reactive]
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Gets the AST node id this property belongs to.
    /// </summary>
    public Guid AstNodeId { get; }

    public PropertyItemViewModel(string name, string category, PropertyKind kind, Guid astNodeId)
    {
        Name = name;
        Category = category;
        Kind = kind;
        AstNodeId = astNodeId;
    }
}

/// <summary>
/// A group of properties in the same category.
/// </summary>
public sealed class PropertyCategoryViewModel : ReactiveObject
{
    /// <summary>
    /// Gets the category name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets whether the category is expanded.
    /// </summary>
    [Reactive]
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// Gets the properties in this category.
    /// </summary>
    public ObservableCollection<PropertyItemViewModel> Properties { get; } = new();

    public PropertyCategoryViewModel(string name)
    {
        Name = name;
    }
}

/// <summary>
/// ViewModel for the property editor panel.
/// </summary>
public sealed class PropertyEditorViewModel : ReactiveObject, IDisposable
{
    /// <summary>
    /// Gets the name of the selected control type.
    /// </summary>
    [Reactive]
    public string? SelectedTypeName { get; set; }

    /// <summary>
    /// Gets or sets the search/filter text.
    /// </summary>
    [Reactive]
    public string? SearchText { get; set; }

    /// <summary>
    /// Gets the categorized properties.
    /// </summary>
    public ObservableCollection<PropertyCategoryViewModel> Categories { get; } = new();

    /// <summary>
    /// Gets the flattened properties for grid display.
    /// </summary>
    public ObservableCollection<PropertyItemViewModel> FlatProperties { get; } = new();

    /// <summary>
    /// Gets or sets the active events list.
    /// </summary>
    public ObservableCollection<EventItemViewModel> Events { get; } = new();

    /// <summary>
    /// Gets or sets whether Properties tab is shown (vs Events tab).
    /// </summary>
    [Reactive]
    public bool ShowProperties { get; set; } = true;

    /// <summary>
    /// Fires when a property value changes (for upstream sync).
    /// </summary>
    public event Action<PropertyItemViewModel>? PropertyValueApplied;

    private readonly AstNodeMap _nodeMap;
    private readonly CompositeDisposable _propertySubscriptions = new();
    private readonly CompositeDisposable _disposables = new();
    private bool _isDisposed;

    public PropertyEditorViewModel(AstNodeMap nodeMap)
    {
        _nodeMap = nodeMap;

        // Filter properties on search text change
        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(filter => FilterProperties(filter))
            .DisposeWith(_disposables);
    }

    /// <summary>
    /// Releases all subscriptions.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _propertySubscriptions.Dispose();
        _disposables.Dispose();
    }

    private void FilterProperties(string? filter)
    {
        foreach (PropertyCategoryViewModel cat in Categories)
        {
            bool anyVisible = false;
            foreach (PropertyItemViewModel prop in cat.Properties)
            {
                bool visible = string.IsNullOrEmpty(filter) ||
                               prop.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
                prop.IsVisible = visible;
                if (visible) anyVisible = true;
            }

            cat.IsExpanded = anyVisible;
        }
    }

    private void RebuildFlatProperties()
    {
        FlatProperties.Clear();
        foreach (PropertyCategoryViewModel category in Categories)
        {
            foreach (PropertyItemViewModel prop in category.Properties)
            {
                FlatProperties.Add(prop);
            }
        }
    }

    /// <summary>
    /// Populates the property editor from a design item.
    /// </summary>
    public void LoadFromDesignItem(IDesignItem item)
    {
        // Dispose previous property value subscriptions
        _propertySubscriptions.Clear();

        Categories.Clear();
        Events.Clear();
        SelectedTypeName = item.TypeName;

        MutableAstObjectNode? node = _nodeMap.FindById(item.AstNodeId) as MutableAstObjectNode;
        if (node is null)
        {
            return;
        }

        // Build property items from current AST properties
        Dictionary<string, PropertyCategoryViewModel> catMap = new();

        foreach (MutableAstPropertyNode prop in node.Properties)
        {
            string category = CategorizeProperty(prop.PropertyName);
            (PropertyKind kind, IReadOnlyList<string>? enumValues) = ResolvePropertyKind(prop.PropertyName);

            PropertyItemViewModel propVm = new(prop.PropertyName, category, kind, node.Id)
            {
                IsSet = true,
                EnumValues = enumValues
            };

            // Get value
            if (prop.Value is MutableAstTextNode textNode)
            {
                propVm.Value = textNode.Text;
            }

            if (!catMap.TryGetValue(category, out PropertyCategoryViewModel? catVm))
            {
                catVm = new PropertyCategoryViewModel(category);
                catMap[category] = catVm;
            }

            catVm.Properties.Add(propVm);
        }

        // Add well-known properties for common types
        AddWellKnownProperties(node, catMap);

        // Sort categories and wire up value-change subscriptions
        foreach (PropertyCategoryViewModel cat in catMap.Values.OrderBy(c => c.Name))
        {
            Categories.Add(cat);

            foreach (PropertyItemViewModel prop in cat.Properties)
            {
                prop.WhenAnyValue(p => p.Value)
                    .Skip(1) // skip initial value
                    .Throttle(TimeSpan.FromMilliseconds(300))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ => ApplyPropertyChange(prop))
                    .DisposeWith(_propertySubscriptions);
            }
        }

        RebuildFlatProperties();
        FilterProperties(SearchText);
    }

    /// <summary>
    /// Applies a property value change back to the AST.
    /// </summary>
    public void ApplyPropertyChange(PropertyItemViewModel prop)
    {
        MutableAstObjectNode? node = _nodeMap.FindById(prop.AstNodeId) as MutableAstObjectNode;
        if (node is null)
        {
            return;
        }

        node.SetPropertyValue(prop.Name, string.IsNullOrEmpty(prop.Value) ? null : prop.Value);
        prop.IsSet = !string.IsNullOrEmpty(prop.Value);
        PropertyValueApplied?.Invoke(prop);
    }

    /// <summary>
    /// Resets a property to its default value.
    /// </summary>
    public void ResetProperty(PropertyItemViewModel prop)
    {
        MutableAstObjectNode? node = _nodeMap.FindById(prop.AstNodeId) as MutableAstObjectNode;
        if (node is null)
        {
            return;
        }

        // Remove the property from the AST
        MutableAstPropertyNode? existingProp = node.Properties
            .FirstOrDefault(p => p.PropertyName == prop.Name);

        if (existingProp is not null)
        {
            node.Properties.Remove(existingProp);
        }

        prop.Value = null;
        prop.IsSet = false;
    }

    private static string CategorizeProperty(string propertyName)
    {
        return propertyName switch
        {
            "Width" or "Height" or "MinWidth" or "MinHeight" or "MaxWidth" or "MaxHeight"
                or "Margin" or "Padding" or "HorizontalAlignment" or "VerticalAlignment"
                or "Row" or "Column" or "RowSpan" or "ColumnSpan"
                or "DockPanel.Dock" or "Canvas.Left" or "Canvas.Top"
                or "Canvas.Right" or "Canvas.Bottom"
                => "Layout",

            "Background" or "Foreground" or "BorderBrush" or "BorderThickness"
                or "CornerRadius" or "Opacity" or "IsVisible" or "ClipToBounds"
                or "RenderTransform" or "RenderTransformOrigin"
                => "Appearance",

            "FontFamily" or "FontSize" or "FontWeight" or "FontStyle"
                or "TextAlignment" or "TextWrapping" or "TextDecorations"
                or "TextTrimming"
                => "Text",

            "Name" or "x:Name" or "Classes" or "Tag" or "DataContext"
                => "Miscellaneous",

            _ => "Common"
        };
    }

    /// <summary>
    /// Determines the appropriate property kind and enum values for a given property name.
    /// </summary>
    internal static (PropertyKind Kind, IReadOnlyList<string>? EnumValues) ResolvePropertyKind(string propertyName)
    {
        return propertyName switch
        {
            // Boolean properties
            "IsVisible" or "ClipToBounds" or "IsEnabled" or "IsHitTestVisible"
                or "IsChecked" or "IsReadOnly" or "AcceptsReturn" or "AcceptsTab"
                or "ShowButtonSpinner" or "IsThreeState" or "CanDrag"
                or "AllowAutoHide" or "IsDefault" or "IsCancel"
                => (PropertyKind.Boolean, null),

            // Numeric properties
            "Width" or "Height" or "MinWidth" or "MinHeight" or "MaxWidth" or "MaxHeight"
                or "FontSize" or "Opacity" or "Spacing" or "Row" or "Column"
                or "RowSpan" or "ColumnSpan" or "Canvas.Left" or "Canvas.Top"
                or "Canvas.Right" or "Canvas.Bottom" or "ZIndex"
                or "SelectedIndex" or "Minimum" or "Maximum" or "Value"
                or "Increment" or "TabIndex"
                => (PropertyKind.Numeric, null),

            // Brush properties
            "Background" or "Foreground" or "BorderBrush" or "Fill" or "Stroke"
                or "OpacityMask" or "CaretBrush" or "SelectionBrush"
                => (PropertyKind.Brush, null),

            // Thickness properties
            "Margin" or "Padding" or "BorderThickness"
                => (PropertyKind.Thickness, null),

            // CornerRadius property
            "CornerRadius"
                => (PropertyKind.CornerRadius, null),

            // Enum: HorizontalAlignment
            "HorizontalAlignment"
                => (PropertyKind.Enum, new[] { "Stretch", "Left", "Center", "Right" }),

            // Enum: VerticalAlignment
            "VerticalAlignment"
                => (PropertyKind.Enum, new[] { "Stretch", "Top", "Center", "Bottom" }),

            // Enum: HorizontalContentAlignment
            "HorizontalContentAlignment"
                => (PropertyKind.Enum, new[] { "Stretch", "Left", "Center", "Right" }),

            // Enum: VerticalContentAlignment
            "VerticalContentAlignment"
                => (PropertyKind.Enum, new[] { "Stretch", "Top", "Center", "Bottom" }),

            // Enum: TextAlignment
            "TextAlignment"
                => (PropertyKind.Enum, new[] { "Left", "Center", "Right", "Justify" }),

            // Enum: TextWrapping
            "TextWrapping"
                => (PropertyKind.Enum, new[] { "NoWrap", "Wrap", "WrapWithOverflow" }),

            // Enum: TextTrimming
            "TextTrimming"
                => (PropertyKind.Enum, new[] { "None", "CharacterEllipsis", "WordEllipsis" }),

            // Enum: FontWeight
            "FontWeight"
                => (PropertyKind.Enum, new[] { "Thin", "ExtraLight", "Light", "Normal", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" }),

            // Enum: FontStyle
            "FontStyle"
                => (PropertyKind.Enum, new[] { "Normal", "Italic", "Oblique" }),

            // Enum: Orientation
            "Orientation"
                => (PropertyKind.Enum, new[] { "Horizontal", "Vertical" }),

            // Enum: Dock
            "DockPanel.Dock" or "Dock"
                => (PropertyKind.Enum, new[] { "Left", "Top", "Right", "Bottom" }),

            // Enum: ScrollBarVisibility
            "HorizontalScrollBarVisibility" or "VerticalScrollBarVisibility"
                => (PropertyKind.Enum, new[] { "Disabled", "Auto", "Hidden", "Visible" }),

            // Default string
            _ => (PropertyKind.ClrProperty, null)
        };
    }



    private void AddWellKnownProperties(MutableAstObjectNode node, Dictionary<string, PropertyCategoryViewModel> catMap)
    {
        // Add common layout properties if not already present
        string[] layoutProps = { "Width", "Height", "Margin", "HorizontalAlignment", "VerticalAlignment" };

        foreach (string propName in layoutProps)
        {
            bool alreadyExists = catMap.Values
                .SelectMany(c => c.Properties)
                .Any(p => p.Name == propName);

            if (!alreadyExists)
            {
                string category = CategorizeProperty(propName);
                (PropertyKind kind, IReadOnlyList<string>? enumValues) = ResolvePropertyKind(propName);

                PropertyItemViewModel propVm = new(propName, category, kind, node.Id)
                {
                    IsSet = false,
                    Value = node.GetPropertyValue(propName),
                    EnumValues = enumValues
                };

                if (!catMap.TryGetValue(category, out PropertyCategoryViewModel? catVm))
                {
                    catVm = new PropertyCategoryViewModel(category);
                    catMap[category] = catVm;
                }

                catVm.Properties.Add(propVm);
            }
        }
    }
}

/// <summary>
/// ViewModel for an event in the property editor.
/// </summary>
public sealed class EventItemViewModel : ReactiveObject
{
    /// <summary>
    /// Gets the event name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the handler name.
    /// </summary>
    [Reactive]
    public string? HandlerName { get; set; }

    public EventItemViewModel(string name)
    {
        Name = name;
    }
}
