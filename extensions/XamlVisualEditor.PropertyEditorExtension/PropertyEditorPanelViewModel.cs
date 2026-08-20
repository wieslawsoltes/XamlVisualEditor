using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Media;
using ReactiveUI;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.PropertyEditorExtension;

public sealed class PropertyEntryViewModel : ReactiveObject
{
    private static readonly HashSet<string> BooleanTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bool",
        "boolean",
        "System.Boolean"
    };
    private static readonly HashSet<string> NumericTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "byte",
        "sbyte",
        "short",
        "ushort",
        "int",
        "int32",
        "uint",
        "uint32",
        "long",
        "int64",
        "ulong",
        "uint64",
        "float",
        "single",
        "double",
        "decimal",
        "System.Byte",
        "System.SByte",
        "System.Int16",
        "System.UInt16",
        "System.Int32",
        "System.UInt32",
        "System.Int64",
        "System.UInt64",
        "System.Single",
        "System.Double",
        "System.Decimal"
    };
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> WellKnownEnumOptions = new Dictionary<string, IReadOnlyList<string>>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["HorizontalAlignment"] = new[] { "Stretch", "Left", "Center", "Right" },
        ["VerticalAlignment"] = new[] { "Stretch", "Top", "Center", "Bottom" },
        ["HorizontalContentAlignment"] = new[] { "Stretch", "Left", "Center", "Right" },
        ["VerticalContentAlignment"] = new[] { "Stretch", "Top", "Center", "Bottom" },
        ["TextAlignment"] = new[] { "Left", "Center", "Right", "Justify" },
        ["TextWrapping"] = new[] { "NoWrap", "Wrap", "WrapWithOverflow" },
        ["TextTrimming"] = new[] { "None", "CharacterEllipsis", "WordEllipsis" },
        ["FontWeight"] = new[] { "Thin", "ExtraLight", "Light", "Normal", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" },
        ["FontStyle"] = new[] { "Normal", "Italic", "Oblique" },
        ["Orientation"] = new[] { "Horizontal", "Vertical" },
        ["DockPanel.Dock"] = new[] { "Left", "Top", "Right", "Bottom" },
        ["Dock"] = new[] { "Left", "Top", "Right", "Bottom" },
        ["HorizontalScrollBarVisibility"] = new[] { "Disabled", "Auto", "Hidden", "Visible" },
        ["VerticalScrollBarVisibility"] = new[] { "Disabled", "Auto", "Hidden", "Visible" }
    };
    private string? _value;
    private string? _committedValue;
    private bool _isSet;
    private bool? _boolValue;
    private double? _numberValue;
    private string? _enumValue;
    private Color? _colorValue;
    private IBrush? _brushPreview;
    private bool _isUpdating;

    public PropertyEntryViewModel(
        string nodeId,
        string name,
        string propertyType,
        string? value,
        bool isReadOnly,
        string? category,
        string? description,
        string? defaultValue,
        bool isAttached,
        string? ownerType,
        IReadOnlyList<string>? enumOptions,
        PropertyEditorDescriptor? descriptor)
    {
        NodeId = nodeId;
        Name = name;
        PropertyType = propertyType;
        IsReadOnly = isReadOnly;
        Category = category ?? "Misc";
        Description = description;
        DefaultValue = defaultValue;
        IsAttached = isAttached;
        OwnerType = ownerType;
        Descriptor = descriptor;
        IReadOnlyList<string>? resolvedEnumOptions = descriptor?.EnumOptions ?? enumOptions ?? ResolveWellKnownEnumOptions(name);
        EnumOptions = resolvedEnumOptions;
        BrushPresets = descriptor?.BrushPresets;
        EditorKind = GetEditorKind(name, propertyType, resolvedEnumOptions, descriptor);
        SetValueInternal(value);
        _committedValue = _value;
        ApplyPresetCommand = ReactiveCommand.Create<string?>(preset => Value = preset);
    }

    public string NodeId { get; }
    public string Name { get; }
    public string PropertyType { get; }
    public bool IsReadOnly { get; }
    public string Category { get; }
    public string? Description { get; }
    public string? DefaultValue { get; }
    public bool IsAttached { get; }
    public string? OwnerType { get; }
    public PropertyEditorDescriptor? Descriptor { get; }
    public PropertyEditorKind EditorKind { get; }
    public IReadOnlyList<string>? EnumOptions { get; }
    public IReadOnlyList<string>? BrushPresets { get; }
    public ReactiveCommand<string?, Unit> ApplyPresetCommand { get; }

    public string? Value
    {
        get => _value;
        set
        {
            if (_isUpdating)
            {
                return;
            }

            SetValueInternal(value);
        }
    }

    public string? CommittedValue => _committedValue;

    public bool IsSet
    {
        get => _isSet;
        private set => this.RaiseAndSetIfChanged(ref _isSet, value);
    }

    public bool? BoolValue
    {
        get => _boolValue;
        set
        {
            if (_isUpdating)
            {
                return;
            }

            SetValueInternal(value.HasValue ? value.Value.ToString() : null);
        }
    }

    public double? NumberValue
    {
        get => _numberValue;
        set
        {
            if (_isUpdating)
            {
                return;
            }

            SetValueInternal(value?.ToString(CultureInfo.InvariantCulture));
        }
    }

    public string? EnumValue
    {
        get => _enumValue;
        set
        {
            if (_isUpdating)
            {
                return;
            }

            SetValueInternal(value);
        }
    }

    public Color? ColorValue
    {
        get => _colorValue;
        set
        {
            if (_isUpdating)
            {
                return;
            }

            SetValueInternal(value?.ToString());
        }
    }

    public IBrush? BrushPreview
    {
        get => _brushPreview;
        private set => this.RaiseAndSetIfChanged(ref _brushPreview, value);
    }

    public void MarkCommitted(string? value)
    {
        _committedValue = value;
    }

    private void SetValueInternal(string? value)
    {
        _isUpdating = true;
        try
        {
            _value = value;
            this.RaisePropertyChanged(nameof(Value));
            IsSet = !string.IsNullOrWhiteSpace(value);

            if (bool.TryParse(value, out bool parsedBool))
            {
                _boolValue = parsedBool;
            }
            else
            {
                _boolValue = null;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedNumber))
            {
                _numberValue = parsedNumber;
            }
            else
            {
                _numberValue = null;
            }

            _enumValue = FindEnumValue(EnumOptions, value);

            if (Color.TryParse(value, out Color parsedColor))
            {
                _colorValue = parsedColor;
                BrushPreview = new SolidColorBrush(parsedColor);
            }
            else
            {
                _colorValue = null;
                BrushPreview = null;
            }

            this.RaisePropertyChanged(nameof(BoolValue));
            this.RaisePropertyChanged(nameof(NumberValue));
            this.RaisePropertyChanged(nameof(EnumValue));
            this.RaisePropertyChanged(nameof(ColorValue));
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private static PropertyEditorKind GetEditorKind(
        string propertyName,
        string propertyType,
        IReadOnlyList<string>? enumOptions,
        PropertyEditorDescriptor? descriptor)
    {
        if (descriptor is not null)
        {
            return descriptor.Kind;
        }

        if (enumOptions is not null && enumOptions.Count > 0)
        {
            return PropertyEditorKind.Enum;
        }

        if (string.IsNullOrWhiteSpace(propertyType))
        {
            return PropertyEditorKind.Text;
        }

        string normalizedType = NormalizeTypeName(propertyType);
        string simpleType = GetSimpleTypeName(normalizedType);

        if (IsMarkupExtensionType(normalizedType) || IsMarkupExtensionType(simpleType))
        {
            return PropertyEditorKind.MarkupExtension;
        }

        if (IsTemplateType(normalizedType) || IsTemplateType(simpleType))
        {
            return PropertyEditorKind.Template;
        }

        if (IsCollectionType(normalizedType) || IsCollectionType(simpleType))
        {
            return PropertyEditorKind.Collection;
        }

        if (IsUriType(normalizedType) || IsUriType(simpleType))
        {
            return PropertyEditorKind.Uri;
        }

        if (IsTimeSpanType(normalizedType) || IsTimeSpanType(simpleType))
        {
            return PropertyEditorKind.TimeSpan;
        }

        if (IsFontWeightType(normalizedType) || IsFontWeightType(simpleType))
        {
            return PropertyEditorKind.FontWeight;
        }

        if (IsFontStyleType(normalizedType) || IsFontStyleType(simpleType))
        {
            return PropertyEditorKind.FontStyle;
        }

        if (IsFontFamilyType(normalizedType) || IsFontFamilyType(simpleType))
        {
            return PropertyEditorKind.FontFamily;
        }

        if (IsGridLengthType(normalizedType) || IsGridLengthType(simpleType))
        {
            return PropertyEditorKind.GridLength;
        }

        if (IsRectType(normalizedType) || IsRectType(simpleType))
        {
            return PropertyEditorKind.Rect;
        }

        if (IsSizeType(normalizedType) || IsSizeType(simpleType))
        {
            return PropertyEditorKind.Size;
        }

        if (IsPointType(normalizedType) || IsPointType(simpleType))
        {
            return PropertyEditorKind.Point;
        }

        if (IsCornerRadiusType(normalizedType) || IsCornerRadiusType(simpleType))
        {
            return PropertyEditorKind.CornerRadius;
        }

        if (IsThicknessType(normalizedType) || IsThicknessType(simpleType))
        {
            return PropertyEditorKind.Thickness;
        }

        if (IsColorType(normalizedType) || IsColorType(simpleType))
        {
            return PropertyEditorKind.Color;
        }

        if (IsBrushType(normalizedType) || IsBrushType(simpleType))
        {
            return PropertyEditorKind.Brush;
        }

        if (BooleanTypeNames.Contains(normalizedType) || BooleanTypeNames.Contains(simpleType))
        {
            return PropertyEditorKind.Boolean;
        }

        if (NumericTypeNames.Contains(normalizedType) || NumericTypeNames.Contains(simpleType))
        {
            return PropertyEditorKind.Number;
        }

        if (IsThicknessPropertyName(propertyName))
        {
            return PropertyEditorKind.Thickness;
        }

        if (IsCornerRadiusPropertyName(propertyName))
        {
            return PropertyEditorKind.CornerRadius;
        }

        if (IsBrushPropertyName(propertyName))
        {
            return PropertyEditorKind.Brush;
        }

        if (IsBooleanPropertyName(propertyName))
        {
            return PropertyEditorKind.Boolean;
        }

        if (IsNumericPropertyName(propertyName))
        {
            return PropertyEditorKind.Number;
        }

        return PropertyEditorKind.Text;
    }

    private static bool IsColorType(string propertyType)
    {
        return IsKnownType(propertyType, "Color", "System.Drawing.Color", "Avalonia.Media.Color");
    }

    private static bool IsBrushType(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            return false;
        }

        string normalized = propertyType.Trim();
        if (normalized.Equals("Brush", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("IBrush", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("SolidColorBrush", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("ImmutableSolidColorBrush", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Avalonia.Media.IBrush", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Avalonia.Media.Brush", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Avalonia.Media.SolidColorBrush", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("Brush", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsBooleanPropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return propertyName switch
        {
            "IsVisible" or "ClipToBounds" or "IsEnabled" or "IsHitTestVisible"
                or "IsChecked" or "IsReadOnly" or "AcceptsReturn" or "AcceptsTab"
                or "ShowButtonSpinner" or "IsThreeState" or "CanDrag"
                or "AllowAutoHide" or "IsDefault" or "IsCancel"
                => true,
            _ => false
        };
    }

    private static bool IsNumericPropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return propertyName switch
        {
            "Width" or "Height" or "MinWidth" or "MinHeight" or "MaxWidth" or "MaxHeight"
                or "FontSize" or "Opacity" or "Spacing" or "Row" or "Column"
                or "RowSpan" or "ColumnSpan" or "Canvas.Left" or "Canvas.Top"
                or "Canvas.Right" or "Canvas.Bottom" or "ZIndex"
                or "SelectedIndex" or "Minimum" or "Maximum" or "Value"
                or "Increment" or "TabIndex"
                => true,
            _ => false
        };
    }

    private static bool IsBrushPropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return propertyName switch
        {
            "Background" or "Foreground" or "BorderBrush" or "Fill" or "Stroke"
                or "OpacityMask" or "CaretBrush" or "SelectionBrush"
                => true,
            _ => false
        };
    }

    private static bool IsThicknessPropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return propertyName is "Margin" or "Padding" or "BorderThickness";
    }

    private static bool IsCornerRadiusPropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return propertyName.Equals("CornerRadius", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string>? ResolveWellKnownEnumOptions(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        return WellKnownEnumOptions.TryGetValue(propertyName, out IReadOnlyList<string>? options)
            ? options
            : null;
    }

    private static bool IsThicknessType(string propertyType)
    {
        return IsKnownType(propertyType, "Thickness", "Avalonia.Thickness");
    }

    private static bool IsCornerRadiusType(string propertyType)
    {
        return IsKnownType(propertyType, "CornerRadius", "Avalonia.CornerRadius");
    }

    private static bool IsPointType(string propertyType)
    {
        return IsKnownType(propertyType, "Point", "Avalonia.Point");
    }

    private static bool IsSizeType(string propertyType)
    {
        return IsKnownType(propertyType, "Size", "Avalonia.Size");
    }

    private static bool IsRectType(string propertyType)
    {
        return IsKnownType(propertyType, "Rect", "Avalonia.Rect");
    }

    private static bool IsGridLengthType(string propertyType)
    {
        return IsKnownType(propertyType, "GridLength", "Avalonia.Controls.GridLength");
    }

    private static bool IsFontFamilyType(string propertyType)
    {
        return IsKnownType(propertyType, "FontFamily", "Avalonia.Media.FontFamily");
    }

    private static bool IsFontWeightType(string propertyType)
    {
        return IsKnownType(propertyType, "FontWeight", "Avalonia.Media.FontWeight");
    }

    private static bool IsFontStyleType(string propertyType)
    {
        return IsKnownType(propertyType, "FontStyle", "Avalonia.Media.FontStyle");
    }

    private static bool IsTimeSpanType(string propertyType)
    {
        return IsKnownType(propertyType, "TimeSpan", "System.TimeSpan");
    }

    private static bool IsUriType(string propertyType)
    {
        return IsKnownType(propertyType, "Uri", "System.Uri");
    }

    private static bool IsCollectionType(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            return false;
        }

        string normalized = propertyType.Trim();
        return normalized.Contains("IList", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("ICollection", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("IEnumerable", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Collection", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("[]", StringComparison.Ordinal)
            || normalized.Contains("Dictionary", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("System.Collections.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemplateType(string propertyType)
    {
        return IsKnownType(propertyType, "ControlTemplate", "DataTemplate", "ITemplate", "Avalonia.Controls.Templates.IDataTemplate");
    }

    private static bool IsMarkupExtensionType(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            return false;
        }

        string normalized = propertyType.Trim();
        return normalized.Contains("MarkupExtension", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Binding", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CompiledBinding", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("DynamicResource", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("StaticResource", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownType(string propertyType, params string[] candidates)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            return false;
        }

        string normalized = propertyType.Trim();
        for (int i = 0; i < candidates.Length; i++)
        {
            if (normalized.Equals(candidates[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetSimpleTypeName(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            return string.Empty;
        }

        string normalized = propertyType.Trim();
        if (normalized.EndsWith("[]", StringComparison.Ordinal))
        {
            normalized = normalized[..^2];
        }

        int genericTick = normalized.IndexOf('`');
        if (genericTick > 0)
        {
            normalized = normalized[..genericTick];
        }

        int genericStart = normalized.IndexOf('<');
        if (genericStart > 0)
        {
            normalized = normalized[..genericStart];
        }

        int lastDot = normalized.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < normalized.Length - 1)
        {
            normalized = normalized[(lastDot + 1)..];
        }

        int lastPlus = normalized.LastIndexOf('+');
        if (lastPlus >= 0 && lastPlus < normalized.Length - 1)
        {
            normalized = normalized[(lastPlus + 1)..];
        }

        return normalized;
    }

    private static string NormalizeTypeName(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            return string.Empty;
        }

        string normalized = propertyType.Trim();
        if (normalized.StartsWith("global::", StringComparison.Ordinal))
        {
            normalized = normalized["global::".Length..];
        }

        normalized = normalized.TrimEnd('?');
        if (normalized.StartsWith("System.Nullable<", StringComparison.OrdinalIgnoreCase)
            && normalized.EndsWith(">", StringComparison.Ordinal))
        {
            normalized = normalized["System.Nullable<".Length..^1].Trim();
        }
        else if (normalized.StartsWith("Nullable<", StringComparison.OrdinalIgnoreCase)
                 && normalized.EndsWith(">", StringComparison.Ordinal))
        {
            normalized = normalized["Nullable<".Length..^1].Trim();
        }
        else if (normalized.StartsWith("System.Nullable`1", StringComparison.OrdinalIgnoreCase)
                 || normalized.StartsWith("Nullable`1", StringComparison.OrdinalIgnoreCase))
        {
            int start = normalized.IndexOf('[');
            int end = normalized.LastIndexOf(']');
            if (start >= 0 && end > start)
            {
                string inner = normalized[(start + 1)..end].Trim();
                inner = inner.Trim('[', ']');
                int comma = inner.IndexOf(',');
                if (comma > 0)
                {
                    inner = inner[..comma];
                }

                if (!string.IsNullOrWhiteSpace(inner))
                {
                    normalized = inner.Trim();
                }
            }
        }

        if (normalized.StartsWith("global::", StringComparison.Ordinal))
        {
            normalized = normalized["global::".Length..];
        }

        return normalized.Trim();
    }

    private static string? FindEnumValue(IReadOnlyList<string>? options, string? value)
    {
        if (options is null || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        for (int i = 0; i < options.Count; i++)
        {
            string option = options[i];
            if (string.Equals(option, value, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }

        return null;
    }
}

public sealed class EventEntryViewModel : ReactiveObject
{
    private string? _handlerName;
    private string? _committedHandler;
    private bool _isUpdating;

    public EventEntryViewModel(string nodeId, string name, string? handlerName, string? description)
    {
        NodeId = nodeId;
        Name = name;
        Description = description;
        _handlerName = handlerName;
        _committedHandler = handlerName;
    }

    public string NodeId { get; }
    public string Name { get; }
    public string? Description { get; }

    public string? HandlerName
    {
        get => _handlerName;
        set
        {
            if (_isUpdating)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _handlerName, value);
        }
    }

    public string? CommittedHandler => _committedHandler;

    public void MarkCommitted(string? value)
    {
        _committedHandler = value;
    }

    public void RestoreCommitted()
    {
        _isUpdating = true;
        try
        {
            HandlerName = _committedHandler;
        }
        finally
        {
            _isUpdating = false;
        }
    }
}

public sealed class PropertyRowViewModel
{
    public PropertyRowViewModel(PropertyEntryViewModel property, string groupName)
    {
        Property = property;
        GroupName = groupName;
    }

    public PropertyEntryViewModel Property { get; }

    public string GroupName { get; }
}

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

public sealed class PropertyEditorPanelViewModel : ReactiveObject, IDisposable
{
    private readonly IDesignerHost _designer;
    private readonly IPropertyEditorRegistry _propertyEditors;
    private readonly Dictionary<string, PropertyEditorDescriptor?> _descriptorCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly CompositeDisposable _disposables = new();
    private readonly CompositeDisposable _entrySubscriptions = new();
    private readonly CompositeDisposable _eventSubscriptions = new();
    private readonly ObservableCollection<PropertyEntryViewModel> _properties = new();
    private readonly ObservableCollection<PropertyRowViewModel> _groupedRows = new();
    private readonly ObservableCollection<EventEntryViewModel> _events = new();
    private CancellationTokenSource? _loadCts;
    private string? _selectedTypeName;
    private string? _selectedNodeId;
    private string? _searchText;
    private int _suspendUpdates;
    private bool _showProperties = true;
    private bool _showGroupedView = true;
    private bool _showLocalValuesOnly;
    private bool _groupedViewDirty = true;
    private string _lastSelectionKey = string.Empty;
    private string _lastActiveDocumentPath = string.Empty;
    private DateTime _lastSelectionUpdateUtc = DateTime.MinValue;
    private static readonly TimeSpan SelectionPollInterval = TimeSpan.FromMilliseconds(500);
    private const string LocalValuesGroupName = "Local Values";
    private static readonly IReadOnlyDictionary<string, int> CategorySortOrder = new Dictionary<string, int>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["Layout"] = 0,
        ["Appearance"] = 1,
        ["Text"] = 2,
        ["Common"] = 3,
        ["Miscellaneous"] = 4,
        ["Misc"] = 5
    };

    public PropertyEditorPanelViewModel(IDesignerHost designer, IPropertyEditorRegistry propertyEditors)
    {
        _designer = designer;
        _propertyEditors = propertyEditors;
        PropertiesView = new DataGridCollectionView(_properties)
        {
            Filter = FilterProperty
        };
        GroupedPropertiesView = new DataGridCollectionView(_groupedRows)
        {
            Filter = FilterGroupedProperty
        };
        EventsView = new DataGridCollectionView(_events)
        {
            Filter = FilterEvent
        };

        _disposables.Add(this.WhenAnyValue(x => x.SearchText)
            .Subscribe(_ =>
            {
                PropertiesView.Refresh();
                EventsView.Refresh();
                RequestGroupedRefresh();
            }));

        _disposables.Add(this.WhenAnyValue(x => x.ShowLocalValuesOnly)
            .Subscribe(_ =>
            {
                PropertiesView.Refresh();
                RequestGroupedRefresh();
            }));

        _disposables.Add(this.WhenAnyValue(x => x.ShowGroupedView)
            .Subscribe(showGrouped =>
            {
                if (showGrouped && _groupedViewDirty)
                {
                    RebuildGroupedView();
                }
            }));

        _disposables.Add(Observable.Interval(SelectionPollInterval, RxSchedulers.TaskpoolScheduler)
            .Where(_ => !string.IsNullOrWhiteSpace(_designer.ActiveDocumentPath))
            .Where(_ => DateTime.UtcNow - _lastSelectionUpdateUtc >= SelectionPollInterval)
            .SelectMany(_ => Observable.FromAsync(PollSelectedNodesAsync))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(nodes =>
            {
                string activeDocumentPath = _designer.ActiveDocumentPath ?? _lastActiveDocumentPath;
                string selectionKey = BuildSelectionKey(activeDocumentPath, nodes);
                if (string.Equals(selectionKey, _lastSelectionKey, StringComparison.Ordinal))
                {
                    return;
                }

                RunBackground(UpdateSelectionAsync(nodes, CancellationToken.None, activeDocumentPath: activeDocumentPath));
            }));
    }

    public DataGridCollectionView PropertiesView { get; }
    public DataGridCollectionView GroupedPropertiesView { get; }
    public DataGridCollectionView EventsView { get; }

    public string? SelectedTypeName
    {
        get => _selectedTypeName;
        private set => this.RaiseAndSetIfChanged(ref _selectedTypeName, value);
    }

    public string? SelectedNodeId
    {
        get => _selectedNodeId;
        private set => this.RaiseAndSetIfChanged(ref _selectedNodeId, value);
    }

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public bool ShowProperties
    {
        get => _showProperties;
        set => this.RaiseAndSetIfChanged(ref _showProperties, value);
    }

    public bool ShowGroupedView
    {
        get => _showGroupedView;
        set => this.RaiseAndSetIfChanged(ref _showGroupedView, value);
    }

    public bool ShowLocalValuesOnly
    {
        get => _showLocalValuesOnly;
        set => this.RaiseAndSetIfChanged(ref _showLocalValuesOnly, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DesignerNodeSummary> selected = await _designer.GetSelectedNodesAsync(cancellationToken);
        string activeDocumentPath = _designer.ActiveDocumentPath ?? string.Empty;
        _lastActiveDocumentPath = activeDocumentPath;
        _lastSelectionUpdateUtc = DateTime.UtcNow;
        _lastSelectionKey = BuildSelectionKey(activeDocumentPath, selected);
        await UpdateSelectionAsync(selected, cancellationToken, forceReload: true, activeDocumentPath: activeDocumentPath);
    }

    public async Task HandleSelectionChangedAsync(
        IReadOnlyList<DesignerNodeSummary> selectedNodes,
        CancellationToken cancellationToken)
    {
        string activeDocumentPath = _designer.ActiveDocumentPath ?? _lastActiveDocumentPath;
        if (selectedNodes.Count == 0 && !string.IsNullOrWhiteSpace(activeDocumentPath))
        {
            try
            {
                IReadOnlyList<DesignerNodeSummary> refreshed = await _designer.GetSelectedNodesAsync(cancellationToken);
                if (refreshed.Count > 0)
                {
                    selectedNodes = refreshed;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        await UpdateSelectionAsync(selectedNodes, cancellationToken, activeDocumentPath: activeDocumentPath);
    }

    public async Task HandleDocumentChangedAsync(string? documentPath, CancellationToken cancellationToken)
    {
        string normalizedDocumentPath = documentPath ?? string.Empty;
        _lastActiveDocumentPath = normalizedDocumentPath;

        IReadOnlyList<DesignerNodeSummary> selectedNodes = await _designer.GetSelectedNodesAsync(cancellationToken);
        await UpdateSelectionAsync(
            selectedNodes,
            cancellationToken,
            forceReload: true,
            activeDocumentPath: normalizedDocumentPath);
    }

    public async Task UpdateSelectionAsync(
        IReadOnlyList<DesignerNodeSummary> selectedNodes,
        CancellationToken cancellationToken,
        bool forceReload = false,
        string? activeDocumentPath = null)
    {
        _lastActiveDocumentPath = activeDocumentPath ?? _designer.ActiveDocumentPath ?? _lastActiveDocumentPath;
        _lastSelectionUpdateUtc = DateTime.UtcNow;
        string selectionKey = BuildSelectionKey(_lastActiveDocumentPath, selectedNodes);
        if (!forceReload && string.Equals(selectionKey, _lastSelectionKey, StringComparison.Ordinal))
        {
            return;
        }

        _lastSelectionKey = selectionKey;

        CancellationTokenSource? previous = _loadCts;
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        previous?.Cancel();
        previous?.Dispose();

        CancellationToken token = _loadCts.Token;
        try
        {
            _suspendUpdates++;
            if (selectedNodes.Count == 0)
            {
                SelectedNodeId = null;
                SelectedTypeName = null;
                ClearProperties();
                ClearEvents();
                return;
            }

            DesignerNodeSummary node = selectedNodes[0];
            SelectedNodeId = node.NodeId;
            SelectedTypeName = node.TypeName;

            Task<IReadOnlyList<DesignerPropertyInfo>> propertiesTask = _designer.GetPropertiesAsync(node.NodeId, token);
            Task<IReadOnlyList<DesignerEventInfo>> eventsTask = _designer.GetEventsAsync(node.NodeId, token);
            await Task.WhenAll(propertiesTask, eventsTask);
            IReadOnlyList<DesignerPropertyInfo> properties = propertiesTask.Result;
            IReadOnlyList<DesignerEventInfo> events = eventsTask.Result;

            UpdatePropertyEntries(node, properties);
            UpdateEventEntries(node, events);
            PropertiesView.Refresh();
            EventsView.Refresh();
            RequestGroupedRefresh();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
        finally
        {
            _suspendUpdates = Math.Max(0, _suspendUpdates - 1);
        }
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _entrySubscriptions.Dispose();
        _eventSubscriptions.Dispose();
        _disposables.Dispose();
    }

    private void ClearProperties()
    {
        _entrySubscriptions.Clear();
        _properties.Clear();
        _groupedRows.Clear();
        _groupedViewDirty = true;
    }

    private void ClearEvents()
    {
        _eventSubscriptions.Clear();
        _events.Clear();
    }

    private void UpdatePropertyEntries(DesignerNodeSummary node, IReadOnlyList<DesignerPropertyInfo> properties)
    {
        Dictionary<string, PropertyEntryViewModel> existing = new(StringComparer.OrdinalIgnoreCase);
        foreach (PropertyEntryViewModel entry in _properties)
        {
            if (string.Equals(entry.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase))
            {
                existing[BuildPropertyEntryKey(entry.Name, entry.OwnerType)] = entry;
            }
        }

        List<PropertyEntryViewModel> ordered = new(properties.Count);
        bool structureChanged = _properties.Count != properties.Count;
        _entrySubscriptions.Clear();

        foreach (DesignerPropertyInfo property in properties)
        {
            string propertyKey = BuildPropertyEntryKey(property.Name, property.OwnerType);
            if (existing.TryGetValue(propertyKey, out PropertyEntryViewModel? reused)
                && CanReusePropertyEntry(reused, node.NodeId, property))
            {
                existing.Remove(propertyKey);
                reused.Value = property.Value;
                reused.MarkCommitted(property.Value);
                ordered.Add(reused);
                continue;
            }

            structureChanged = true;
            ordered.Add(CreatePropertyEntry(node.NodeId, property));
        }

        if (existing.Count > 0 || !AreSameEntrySequence(_properties, ordered))
        {
            structureChanged = true;
        }

        if (structureChanged)
        {
            _properties.Clear();
            foreach (PropertyEntryViewModel entry in ordered)
            {
                _properties.Add(entry);
            }
        }

        foreach (PropertyEntryViewModel entry in _properties)
        {
            HookPropertyEntry(entry);
        }
    }

    private void UpdateEventEntries(DesignerNodeSummary node, IReadOnlyList<DesignerEventInfo> events)
    {
        Dictionary<string, EventEntryViewModel> existing = new(StringComparer.OrdinalIgnoreCase);
        foreach (EventEntryViewModel entry in _events)
        {
            if (string.Equals(entry.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase))
            {
                existing[entry.Name] = entry;
            }
        }

        List<EventEntryViewModel> ordered = new(events.Count);
        bool structureChanged = _events.Count != events.Count;
        _eventSubscriptions.Clear();

        foreach (DesignerEventInfo evt in events)
        {
            if (existing.TryGetValue(evt.Name, out EventEntryViewModel? reused)
                && CanReuseEventEntry(reused, node.NodeId, evt))
            {
                existing.Remove(evt.Name);
                reused.HandlerName = evt.HandlerName;
                reused.MarkCommitted(evt.HandlerName);
                ordered.Add(reused);
                continue;
            }

            structureChanged = true;
            ordered.Add(new EventEntryViewModel(node.NodeId, evt.Name, evt.HandlerName, evt.Description));
        }

        if (existing.Count > 0 || !AreSameEntrySequence(_events, ordered))
        {
            structureChanged = true;
        }

        if (structureChanged)
        {
            _events.Clear();
            foreach (EventEntryViewModel entry in ordered)
            {
                _events.Add(entry);
            }
        }

        foreach (EventEntryViewModel entry in _events)
        {
            HookEventEntry(entry);
        }
    }

    private PropertyEntryViewModel CreatePropertyEntry(string nodeId, DesignerPropertyInfo property)
    {
        return new PropertyEntryViewModel(
            nodeId,
            property.Name,
            property.PropertyType,
            property.Value,
            property.IsReadOnly,
            property.Category,
            property.Description,
            property.DefaultValue,
            property.IsAttached,
            property.OwnerType,
            property.EnumOptions,
            ResolveDescriptor(property.Name, property.PropertyType));
    }

    private static bool CanReusePropertyEntry(PropertyEntryViewModel entry, string nodeId, DesignerPropertyInfo property)
    {
        return string.Equals(entry.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Name, property.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.PropertyType, property.PropertyType, StringComparison.OrdinalIgnoreCase)
            && entry.IsReadOnly == property.IsReadOnly
            && entry.IsAttached == property.IsAttached
            && string.Equals(entry.OwnerType, property.OwnerType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Category, property.Category ?? "Misc", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanReuseEventEntry(EventEntryViewModel entry, string nodeId, DesignerEventInfo evt)
    {
        return string.Equals(entry.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Name, evt.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Description, evt.Description, StringComparison.Ordinal);
    }

    private static string BuildPropertyEntryKey(string name, string? ownerType)
    {
        string owner = string.IsNullOrWhiteSpace(ownerType) ? string.Empty : ownerType.Trim();
        return owner + "|" + name;
    }

    private static bool AreSameEntrySequence<T>(IList<T> current, IList<T> next)
        where T : class
    {
        if (current.Count != next.Count)
        {
            return false;
        }

        for (int i = 0; i < current.Count; i++)
        {
            if (!ReferenceEquals(current[i], next[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildSelectionKey(string documentPath, IReadOnlyList<DesignerNodeSummary> selectedNodes)
    {
        if (selectedNodes.Count == 0)
        {
            return documentPath;
        }

        return documentPath + "|" + string.Join("|", selectedNodes.Select(node => node.NodeId));
    }

    private void HookPropertyEntry(PropertyEntryViewModel entry)
    {
        IDisposable subscription = entry.WhenAnyValue(x => x.Value)
            .Skip(1)
            .DistinctUntilChanged()
            .Subscribe(value =>
            {
                RunBackground(ApplyPropertyChangeAsync(entry, value));
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    PropertiesView.Refresh();
                    RequestGroupedRefresh();
                }
            });
        _entrySubscriptions.Add(subscription);

        IDisposable setSubscription = entry.WhenAnyValue(x => x.IsSet)
            .Skip(1)
            .DistinctUntilChanged()
            .Subscribe(_ =>
            {
                if (_suspendUpdates > 0)
                {
                    return;
                }

                RequestGroupedRefresh();
            });
        _entrySubscriptions.Add(setSubscription);
    }

    private void HookEventEntry(EventEntryViewModel entry)
    {
        IDisposable subscription = entry.WhenAnyValue(x => x.HandlerName)
            .Skip(1)
            .DistinctUntilChanged()
            .Subscribe(value =>
            {
                RunBackground(ApplyEventChangeAsync(entry, value));
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    EventsView.Refresh();
                }
            });
        _eventSubscriptions.Add(subscription);
    }

    private async Task ApplyPropertyChangeAsync(PropertyEntryViewModel entry, string? value)
    {
        if (_suspendUpdates > 0 || entry.IsReadOnly)
        {
            return;
        }

        if (string.Equals(entry.CommittedValue, value, StringComparison.Ordinal))
        {
            return;
        }

        string? nodeId = SelectedNodeId;
        if (string.IsNullOrWhiteSpace(nodeId) || !string.Equals(nodeId, entry.NodeId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CancellationTokenSource? cts = _loadCts;
        if (cts is null)
        {
            return;
        }

        CancellationToken token = cts.Token;
        IDesignerTransaction transaction = _designer.BeginTransaction($"Set {entry.Name}");
        try
        {
            bool applied = await _designer.SetPropertyAsync(nodeId, entry.Name, value, token);
            if (applied)
            {
                entry.MarkCommitted(value);
                await transaction.CommitAsync(token);
            }
            else
            {
                await transaction.RollbackAsync(token);
                RestoreEntryValue(entry);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            RestoreEntryValue(entry);
        }
        finally
        {
            transaction.Dispose();
        }
    }

    private async Task ApplyEventChangeAsync(EventEntryViewModel entry, string? value)
    {
        if (_suspendUpdates > 0)
        {
            return;
        }

        if (string.Equals(entry.CommittedHandler, value, StringComparison.Ordinal))
        {
            return;
        }

        string? nodeId = SelectedNodeId;
        if (string.IsNullOrWhiteSpace(nodeId) || !string.Equals(nodeId, entry.NodeId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CancellationTokenSource? cts = _loadCts;
        if (cts is null)
        {
            return;
        }

        CancellationToken token = cts.Token;
        IDesignerTransaction transaction = _designer.BeginTransaction($"Set {entry.Name}");
        try
        {
            bool applied = await _designer.SetPropertyAsync(nodeId, entry.Name, value, token);
            if (applied)
            {
                entry.MarkCommitted(value);
                await transaction.CommitAsync(token);
            }
            else
            {
                await transaction.RollbackAsync(token);
                entry.RestoreCommitted();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            entry.RestoreCommitted();
        }
        finally
        {
            transaction.Dispose();
        }
    }

    private void RestoreEntryValue(PropertyEntryViewModel entry)
    {
        _suspendUpdates++;
        try
        {
            entry.Value = entry.CommittedValue;
        }
        finally
        {
            _suspendUpdates = Math.Max(0, _suspendUpdates - 1);
        }
    }

    private bool FilterProperty(object? item)
    {
        if (item is not PropertyEntryViewModel entry)
        {
            return false;
        }

        return (!ShowLocalValuesOnly || entry.IsSet) && MatchesPropertyFilter(entry);
    }

    private void RebuildGroupedView()
    {
        _groupedRows.Clear();

        List<PropertyEntryViewModel> filtered = _properties
            .Where(MatchesPropertyFilter)
            .ToList();

        if (filtered.Count == 0)
        {
            GroupedPropertiesView.GroupDescriptions.Clear();
            GroupedPropertiesView.Refresh();
            _groupedViewDirty = false;
            return;
        }

        List<PropertyEntryViewModel> localValues = filtered
            .Where(entry => entry.IsSet)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (PropertyEntryViewModel entry in localValues)
        {
            _groupedRows.Add(new PropertyRowViewModel(entry, LocalValuesGroupName));
        }

        List<string> categoryOrder = new();
        if (!ShowLocalValuesOnly)
        {
            Dictionary<string, List<PropertyEntryViewModel>> categories = new(StringComparer.OrdinalIgnoreCase);
            foreach (PropertyEntryViewModel entry in filtered)
            {
                string category = string.IsNullOrWhiteSpace(entry.Category) ? "Misc" : entry.Category;
                if (!categories.TryGetValue(category, out List<PropertyEntryViewModel>? list))
                {
                    list = new List<PropertyEntryViewModel>();
                    categories[category] = list;
                }

                list.Add(entry);
            }

            categoryOrder = categories.Keys
                .OrderBy(GetCategorySortKey)
                .ThenBy(category => category, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string category in categoryOrder)
            {
                foreach (PropertyEntryViewModel entry in categories[category]
                             .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                {
                    _groupedRows.Add(new PropertyRowViewModel(entry, category));
                }
            }
        }

        using (GroupedPropertiesView.DeferRefresh())
        {
            GroupedPropertiesView.GroupDescriptions.Clear();
            if (_groupedRows.Count > 0)
            {
                PropertyRowGroupDescription groupDescription = new();
                if (localValues.Count > 0)
                {
                    groupDescription.GroupKeys.Add(LocalValuesGroupName);
                }

                foreach (string category in categoryOrder)
                {
                    groupDescription.GroupKeys.Add(category);
                }

                GroupedPropertiesView.GroupDescriptions.Add(groupDescription);
            }
        }

        GroupedPropertiesView.Refresh();
        _groupedViewDirty = false;
    }

    private static int GetCategorySortKey(string category)
    {
        if (CategorySortOrder.TryGetValue(category, out int order))
        {
            return order;
        }

        return 100;
    }

    private void RequestGroupedRefresh()
    {
        _groupedViewDirty = true;
        if (!ShowGroupedView)
        {
            return;
        }

        RebuildGroupedView();
    }

    private bool FilterEvent(object? item)
    {
        if (item is not EventEntryViewModel entry)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return entry.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || (entry.HandlerName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private bool FilterGroupedProperty(object? item)
    {
        return item is PropertyRowViewModel row && FilterProperty(row.Property);
    }

    private async Task<IReadOnlyList<DesignerNodeSummary>> PollSelectedNodesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _designer.GetSelectedNodesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<DesignerNodeSummary>();
        }
        catch
        {
            return Array.Empty<DesignerNodeSummary>();
        }
    }

    private bool MatchesPropertyFilter(PropertyEntryViewModel entry)
    {
        if (ShowLocalValuesOnly && !entry.IsSet)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return entry.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || (entry.Value?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
            || entry.PropertyType.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || entry.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private PropertyEditorDescriptor? ResolveDescriptor(string propertyName, string propertyType)
    {
        string cacheKey = (propertyType ?? string.Empty).Trim()
            + "||"
            + (propertyName ?? string.Empty).Trim();
        if (_descriptorCache.TryGetValue(cacheKey, out PropertyEditorDescriptor? cached))
        {
            return cached;
        }

        _propertyEditors.TryResolve(propertyName, propertyType, out PropertyEditorDescriptor? descriptor);
        _descriptorCache[cacheKey] = descriptor;
        return descriptor;
    }

    private static void RunBackground(Task task)
    {
        _ = task.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
