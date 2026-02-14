using System;
using System.Collections.Generic;

namespace XamlVisualEditor.Extensions;

/// <summary>Defines known property editor kinds.</summary>
public enum PropertyEditorKind
{
    Text,
    Boolean,
    Number,
    Enum,
    Brush,
    Thickness,
    CornerRadius,
    Point,
    Size,
    Rect,
    GridLength,
    FontFamily,
    FontWeight,
    FontStyle,
    TimeSpan,
    Uri,
    Collection,
    Template,
    MarkupExtension
}

/// <summary>Describes a property editor contribution.</summary>
public sealed record PropertyEditorDescriptor(
    string PropertyType,
    PropertyEditorKind Kind,
    IReadOnlyList<string>? EnumOptions = null,
    IReadOnlyList<string>? BrushPresets = null);

/// <summary>Registry for property editor metadata contributed by extensions.</summary>
public interface IPropertyEditorRegistry
{
    /// <summary>Registers a property editor descriptor.</summary>
    IDisposable Register(PropertyEditorDescriptor descriptor);

    /// <summary>Gets a descriptor for the provided property type.</summary>
    bool TryGet(string propertyType, out PropertyEditorDescriptor? descriptor);

    /// <summary>Gets all registered descriptors.</summary>
    IReadOnlyList<PropertyEditorDescriptor> GetAll();
}

/// <summary>Default in-process registry for property editor descriptors.</summary>
public sealed class PropertyEditorRegistry : IPropertyEditorRegistry
{
    private readonly Dictionary<string, PropertyEditorDescriptor> _entries =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public IDisposable Register(PropertyEditorDescriptor descriptor)
    {
        if (descriptor is null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        lock (_gate)
        {
            _entries[descriptor.PropertyType] = descriptor;
        }

        return new Registration(this, descriptor.PropertyType, descriptor);
    }

    public bool TryGet(string propertyType, out PropertyEditorDescriptor? descriptor)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
        {
            descriptor = null;
            return false;
        }

        lock (_gate)
        {
            return _entries.TryGetValue(propertyType, out descriptor);
        }
    }

    public IReadOnlyList<PropertyEditorDescriptor> GetAll()
    {
        lock (_gate)
        {
            return new List<PropertyEditorDescriptor>(_entries.Values);
        }
    }

    private void Unregister(string propertyType, PropertyEditorDescriptor descriptor)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(propertyType, out PropertyEditorDescriptor? existing)
                && ReferenceEquals(existing, descriptor))
            {
                _entries.Remove(propertyType);
            }
        }
    }

    private sealed class Registration : IDisposable
    {
        private PropertyEditorRegistry? _owner;
        private readonly string _propertyType;
        private readonly PropertyEditorDescriptor _descriptor;

        public Registration(PropertyEditorRegistry owner, string propertyType, PropertyEditorDescriptor descriptor)
        {
            _owner = owner;
            _propertyType = propertyType;
            _descriptor = descriptor;
        }

        public void Dispose()
        {
            PropertyEditorRegistry? owner = _owner;
            if (owner is null)
            {
                return;
            }

            owner.Unregister(_propertyType, _descriptor);
            _owner = null;
        }
    }
}
