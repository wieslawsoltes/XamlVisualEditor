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
    MarkupExtension,
    Color
}

/// <summary>Describes a property editor contribution.</summary>
public sealed record PropertyEditorDescriptor(
    /// <summary>
    /// Gets the lookup key for the editor descriptor.
    /// This can be a CLR type name (preferred) or a well-known property name fallback.
    /// </summary>
    string PropertyType,
    PropertyEditorKind Kind,
    IReadOnlyList<string>? EnumOptions = null,
    IReadOnlyList<string>? BrushPresets = null);

/// <summary>Registry for property editor metadata contributed by extensions.</summary>
public interface IPropertyEditorRegistry
{
    /// <summary>Registers a property editor descriptor.</summary>
    IDisposable Register(PropertyEditorDescriptor descriptor);

    /// <summary>Gets a descriptor by a raw lookup key.</summary>
    bool TryGet(string propertyType, out PropertyEditorDescriptor? descriptor);

    /// <summary>Resolves a descriptor using property type first, then property name fallbacks.</summary>
    bool TryResolve(string? propertyName, string? propertyType, out PropertyEditorDescriptor? descriptor);

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

        string propertyKey = NormalizeLookupKey(descriptor.PropertyType);
        if (string.IsNullOrWhiteSpace(propertyKey))
        {
            throw new ArgumentException("Property type key is required.", nameof(descriptor));
        }

        PropertyEditorDescriptor normalizedDescriptor = string.Equals(
            descriptor.PropertyType,
            propertyKey,
            StringComparison.Ordinal)
            ? descriptor
            : descriptor with { PropertyType = propertyKey };

        lock (_gate)
        {
            _entries[propertyKey] = normalizedDescriptor;
        }

        return new Registration(this, propertyKey, normalizedDescriptor);
    }

    public bool TryGet(string propertyType, out PropertyEditorDescriptor? descriptor)
    {
        string propertyKey = NormalizeLookupKey(propertyType);
        if (string.IsNullOrWhiteSpace(propertyKey))
        {
            descriptor = null;
            return false;
        }

        lock (_gate)
        {
            return _entries.TryGetValue(propertyKey, out descriptor);
        }
    }

    public bool TryResolve(string? propertyName, string? propertyType, out PropertyEditorDescriptor? descriptor)
    {
        if (TryResolveCore(propertyType, out descriptor))
        {
            return true;
        }

        if (TryResolveCore(propertyName, out descriptor))
        {
            return true;
        }

        descriptor = null;
        return false;
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

    private bool TryResolveCore(string? key, out PropertyEditorDescriptor? descriptor)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            descriptor = null;
            return false;
        }

        foreach (string candidate in EnumerateLookupKeys(key))
        {
            if (TryGet(candidate, out descriptor))
            {
                return true;
            }
        }

        descriptor = null;
        return false;
    }

    private static IEnumerable<string> EnumerateLookupKeys(string key)
    {
        string normalized = NormalizeLookupKey(key);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        yield return normalized;

        string simple = GetSimpleTypeName(normalized);
        if (!string.IsNullOrWhiteSpace(simple)
            && !string.Equals(simple, normalized, StringComparison.OrdinalIgnoreCase))
        {
            yield return simple;
        }
    }

    private static string NormalizeLookupKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        string normalized = key.Trim();
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

    private static string GetSimpleTypeName(string propertyType)
    {
        string normalized = NormalizeLookupKey(propertyType);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

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

        return normalized.Trim();
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
