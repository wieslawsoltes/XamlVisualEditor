using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
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
    /// Gets the full type name of the property value.
    /// </summary>
    public string? TypeFullName { get; }

    /// <summary>
    /// Gets whether this property is attached.
    /// </summary>
    public bool IsAttached { get; }

    /// <summary>
    /// Gets whether this property is read-only.
    /// </summary>
    public bool IsReadOnly { get; }

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

    public PropertyItemViewModel(
        string name,
        string category,
        PropertyKind kind,
        Guid astNodeId,
        string? typeFullName = null,
        bool isAttached = false,
        bool isReadOnly = false)
    {
        Name = name;
        Category = category;
        Kind = kind;
        AstNodeId = astNodeId;
        TypeFullName = typeFullName;
        IsAttached = isAttached;
        IsReadOnly = isReadOnly;
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
    /// Gets or sets whether the category is visible after filtering.
    /// </summary>
    [Reactive]
    public bool IsVisible { get; set; } = true;

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
/// Provides row data for grouped property views.
/// </summary>
public sealed class PropertyRowViewModel
{
    public PropertyItemViewModel Property { get; }

    public string GroupName { get; }

    public PropertyRowViewModel(PropertyItemViewModel property, string groupName)
    {
        Property = property;
        GroupName = groupName;
    }
}

/// <summary>
/// Groups property rows without reflection-based property paths.
/// </summary>
public sealed class PropertyRowGroupDescription : DataGridGroupDescription
{
    public override string PropertyName => nameof(PropertyRowViewModel.GroupName);

    public override object GroupKeyFromItem(object item, int level, CultureInfo culture)
    {
        if (item is PropertyRowViewModel row)
        {
            return row.GroupName;
        }

        return string.Empty;
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
    /// Gets the grouped rows for grid display.
    /// </summary>
    public ObservableCollection<PropertyRowViewModel> GroupedRows { get; } = new();

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
    /// Gets or sets whether grouped view is shown (vs flat grid).
    /// </summary>
    [Reactive]
    public bool ShowGroupedView { get; set; }

    /// <summary>
    /// Fires when a property value changes (for upstream sync).
    /// </summary>
    public event Action<PropertyItemViewModel>? PropertyValueApplied;

    private readonly AstNodeMap _nodeMap;
    private readonly ITypeMetadataService? _metadataService;
    private readonly ILogger<PropertyEditorViewModel> _logger;
    private readonly CompositeDisposable _propertySubscriptions = new();
    private readonly CompositeDisposable _disposables = new();
    private const string LocalValuesGroupName = "Local Values";
    private DataGridCollectionView? _groupedCollectionView;
    private bool _isDisposed;

    /// <summary>
    /// Gets the grouped collection view used by the DataGrid.
    /// </summary>
    public DataGridCollectionView? GroupedCollectionView
    {
        get => _groupedCollectionView;
        private set => this.RaiseAndSetIfChanged(ref _groupedCollectionView, value);
    }

    public PropertyEditorViewModel(
        AstNodeMap nodeMap,
        ITypeMetadataService? metadataService = null,
        ILogger<PropertyEditorViewModel>? logger = null)
    {
        _nodeMap = nodeMap;
        _metadataService = metadataService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PropertyEditorViewModel>.Instance;
        ShowGroupedView = true;

        // Filter properties on search text change
        IDisposable searchSubscription = this.WhenAnyValue(x => x.SearchText)
            .Select(text => string.IsNullOrWhiteSpace(text) ? null : text.Trim())
            .DistinctUntilChanged()
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(filter => FilterProperties(filter));
        _disposables.Add(searchSubscription);
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
        bool hasFilter = !string.IsNullOrWhiteSpace(filter);
        IEnumerable<PropertyCategoryViewModel> categories = Categories;
        Dictionary<PropertyItemViewModel, bool> matchCache = new();

        foreach (PropertyCategoryViewModel cat in categories)
        {
            bool anyVisible = false;
            bool categoryMatch = hasFilter && cat.Name.Contains(filter!, StringComparison.OrdinalIgnoreCase);

            foreach (PropertyItemViewModel prop in cat.Properties)
            {
                if (!matchCache.TryGetValue(prop, out bool visible))
                {
                    visible = !hasFilter || categoryMatch ||
                              prop.Name.Contains(filter!, StringComparison.OrdinalIgnoreCase) ||
                              prop.Category.Contains(filter!, StringComparison.OrdinalIgnoreCase) ||
                              (!string.IsNullOrWhiteSpace(prop.Value) &&
                               prop.Value.Contains(filter!, StringComparison.OrdinalIgnoreCase));
                    matchCache[prop] = visible;
                }

                if (prop.IsVisible != visible)
                {
                    prop.IsVisible = visible;
                }

                if (visible)
                {
                    anyVisible = true;
                }
            }

            cat.IsVisible = anyVisible;
            if (hasFilter)
            {
                cat.IsExpanded = anyVisible;
            }
        }

        GroupedCollectionView?.Refresh();
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

        Dictionary<string, PropertyCategoryViewModel> catMap = new();
        Dictionary<string, MutableAstPropertyNode> existingProps = node.Properties
            .ToDictionary(p => p.PropertyName, StringComparer.OrdinalIgnoreCase);

        TypeMetadata? meta = _metadataService?.GetType(node.XmlNamespace, node.TypeName);

        if (meta is not null && _metadataService is not null)
        {
            foreach (PropertyMetadata prop in _metadataService.GetProperties(meta))
            {
                string category = string.IsNullOrWhiteSpace(prop.Category)
                    ? CategorizeProperty(prop.Name)
                    : prop.Category;
                PropertyItemViewModel propVm = CreatePropertyItem(prop, node.Id, category);

                if (existingProps.TryGetValue(prop.Name, out MutableAstPropertyNode? existing))
                {
                    propVm.Value = (existing.Value as MutableAstTextNode)?.Text;
                    propVm.IsSet = !string.IsNullOrWhiteSpace(propVm.Value);
                }

                AddPropertyToCategory(catMap, category, propVm);
            }

            foreach (EventMetadata evt in _metadataService.GetEvents(meta))
            {
                EventItemViewModel eventVm = new(evt.Name)
                {
                    HandlerName = node.GetPropertyValue(evt.Name)
                };
                Events.Add(eventVm);
            }
        }

        foreach (MutableAstPropertyNode prop in node.Properties)
        {
            if (catMap.Values.SelectMany(c => c.Properties)
                .Any(p => string.Equals(p.Name, prop.PropertyName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string category = CategorizeProperty(prop.PropertyName);
            (PropertyKind kind, IReadOnlyList<string>? enumValues) = ResolvePropertyKind(prop.PropertyName, null);
            PropertyItemViewModel propVm = new(prop.PropertyName, category, kind, node.Id)
            {
                IsSet = true,
                EnumValues = enumValues,
                Value = (prop.Value as MutableAstTextNode)?.Text
            };

            AddPropertyToCategory(catMap, category, propVm);
        }

        if (catMap.Count == 0)
        {
            AddWellKnownProperties(node, catMap);
        }

        // Sort categories and wire up value-change subscriptions
        foreach (PropertyCategoryViewModel cat in catMap.Values.OrderBy(c => c.Name))
        {
            Categories.Add(cat);

            foreach (PropertyItemViewModel prop in cat.Properties)
            {
                IDisposable valueSubscription = prop.WhenAnyValue(p => p.Value)
                    .Skip(1) // skip initial value
                    .Throttle(TimeSpan.FromMilliseconds(300))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ => ApplyPropertyChange(prop));
                _propertySubscriptions.Add(valueSubscription);
            }
        }

        RebuildFlatProperties();
        RebuildGroupedCollectionView();
        FilterProperties(SearchText);
    }

    private void RebuildGroupedCollectionView()
    {
        try
        {
            GroupedRows.Clear();

            List<PropertyItemViewModel> allProperties = Categories
                .SelectMany(c => c.Properties)
                .ToList();
            List<PropertyItemViewModel> localValues = allProperties
                .Where(p => p.IsSet)
                .OrderBy(p => p.Name)
                .ToList();

            foreach (PropertyItemViewModel prop in localValues)
            {
                GroupedRows.Add(new PropertyRowViewModel(prop, LocalValuesGroupName));
            }

            foreach (PropertyCategoryViewModel category in Categories.OrderBy(c => c.Name))
            {
                foreach (PropertyItemViewModel prop in category.Properties.OrderBy(p => p.Name))
                {
                    GroupedRows.Add(new PropertyRowViewModel(prop, category.Name));
                }
            }

            DataGridCollectionView view = GroupedCollectionView ?? new DataGridCollectionView(GroupedRows);
            using (view.DeferRefresh())
            {
                view.Filter = item => item is PropertyRowViewModel row && row.Property.IsVisible;
                view.GroupDescriptions.Clear();

                PropertyRowGroupDescription groupDescription = new();
                groupDescription.GroupKeys.Add(LocalValuesGroupName);
                foreach (PropertyCategoryViewModel category in Categories.OrderBy(c => c.Name))
                {
                    groupDescription.GroupKeys.Add(category.Name);
                }

                view.GroupDescriptions.Add(groupDescription);
            }

            view.Refresh();
            GroupedCollectionView ??= view;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Property editor grouping failed: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Applies a property value change back to the AST.
    /// </summary>
    public void ApplyPropertyChange(PropertyItemViewModel prop)
    {
        if (prop.IsReadOnly)
        {
            return;
        }

        MutableAstObjectNode? node = _nodeMap.FindById(prop.AstNodeId) as MutableAstObjectNode;
        if (node is null)
        {
            return;
        }

        node.SetPropertyValue(prop.Name, string.IsNullOrEmpty(prop.Value) ? null : prop.Value);
        prop.IsSet = !string.IsNullOrEmpty(prop.Value);
        PropertyValueApplied?.Invoke(prop);
        GroupedCollectionView?.Refresh();
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
        GroupedCollectionView?.Refresh();
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
        return ResolvePropertyKind(propertyName, null);
    }

    internal static (PropertyKind Kind, IReadOnlyList<string>? EnumValues) ResolvePropertyKind(
        string propertyName,
        Type? propertyType)
    {
        if (propertyType is not null)
        {
            if (propertyType.IsEnum)
            {
                return (PropertyKind.Enum, Enum.GetNames(propertyType));
            }

            if (propertyType == typeof(string))
            {
                return (PropertyKind.String, null);
            }

            if (propertyType == typeof(bool) || propertyType == typeof(bool?))
            {
                return (PropertyKind.Boolean, null);
            }

            if (propertyType == typeof(Avalonia.Thickness))
            {
                return (PropertyKind.Thickness, null);
            }

            if (propertyType == typeof(Avalonia.CornerRadius))
            {
                return (PropertyKind.CornerRadius, null);
            }

            if (propertyType == typeof(Avalonia.Media.Color))
            {
                return (PropertyKind.Color, null);
            }

            if (typeof(Avalonia.Media.IBrush).IsAssignableFrom(propertyType))
            {
                return (PropertyKind.Brush, null);
            }

            if (propertyType == typeof(Avalonia.Point))
            {
                return (PropertyKind.Point, null);
            }

            if (propertyType == typeof(Avalonia.Size))
            {
                return (PropertyKind.Size, null);
            }

            if (propertyType == typeof(Avalonia.Rect))
            {
                return (PropertyKind.Rect, null);
            }

            if (propertyType == typeof(Avalonia.Controls.GridLength))
            {
                return (PropertyKind.GridLength, null);
            }

            if (propertyType == typeof(Avalonia.Media.FontFamily))
            {
                return (PropertyKind.FontFamily, null);
            }

            if (propertyType == typeof(Avalonia.Media.FontWeight))
            {
                return (PropertyKind.FontWeight, null);
            }

            if (propertyType == typeof(Avalonia.Media.FontStyle))
            {
                return (PropertyKind.FontStyle, null);
            }

            if (propertyType == typeof(TimeSpan) || propertyType == typeof(TimeSpan?))
            {
                return (PropertyKind.TimeSpan, null);
            }

            if (propertyType == typeof(Uri))
            {
                return (PropertyKind.Uri, null);
            }

            if (typeof(Avalonia.Controls.Templates.IDataTemplate).IsAssignableFrom(propertyType) ||
                typeof(Avalonia.Controls.Templates.IControlTemplate).IsAssignableFrom(propertyType))
            {
                return (PropertyKind.Template, null);
            }

            if (typeof(Avalonia.Markup.Xaml.MarkupExtension).IsAssignableFrom(propertyType))
            {
                return (PropertyKind.MarkupExtension, null);
            }

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(propertyType) && propertyType != typeof(string))
            {
                return (PropertyKind.Collection, null);
            }

            if (propertyType.IsPrimitive)
            {
                return (PropertyKind.Numeric, null);
            }

            switch (Type.GetTypeCode(propertyType))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return (PropertyKind.Numeric, null);
            }
        }

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
            _ => (PropertyKind.String, null)
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
                (PropertyKind kind, IReadOnlyList<string>? enumValues) = ResolvePropertyKind(propName, null);

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

    private static void AddPropertyToCategory(
        IDictionary<string, PropertyCategoryViewModel> catMap,
        string category,
        PropertyItemViewModel propVm)
    {
        if (!catMap.TryGetValue(category, out PropertyCategoryViewModel? catVm))
        {
            catVm = new PropertyCategoryViewModel(category);
            catMap[category] = catVm;
        }

        catVm.Properties.Add(propVm);
    }

    private PropertyItemViewModel CreatePropertyItem(PropertyMetadata prop, Guid nodeId, string category)
    {
        (PropertyKind kind, IReadOnlyList<string>? enumValues) = ResolvePropertyKind(prop.Name, prop.ClrType);
        PropertyItemViewModel propVm = new(
            prop.Name,
            category,
            kind,
            nodeId,
            prop.TypeFullName,
            prop.IsAttached,
            prop.IsReadOnly)
        {
            EnumValues = enumValues,
            DefaultValueHint = prop.DefaultValue is null ? null : Convert.ToString(prop.DefaultValue),
            IsSet = false,
            Value = null
        };

        return propVm;
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
