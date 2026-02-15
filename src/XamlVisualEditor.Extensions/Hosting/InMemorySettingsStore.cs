using System.Collections.Generic;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>In-memory settings store with user/workspace precedence.</summary>
public sealed class InMemorySettingsStore : ISettings
{
    private readonly object _gate = new();
    private readonly Dictionary<string, object?> _user = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _workspace = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SettingsSectionSchema> _schemas = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public event EventHandler<SettingsSectionChangedEventArgs>? SectionChanged;

    /// <inheritdoc />
    public T? Get<T>(string section, T? defaultValue = default)
    {
        lock (_gate)
        {
            if (_workspace.TryGetValue(section, out object? workspaceValue) && workspaceValue is T workspace)
            {
                return workspace;
            }

            if (_user.TryGetValue(section, out object? userValue) && userValue is T user)
            {
                return user;
            }
        }

        return defaultValue;
    }

    /// <inheritdoc />
    public Task UpdateAsync(string section, object? value, SettingsTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<SettingsValidationIssue> issues = Validate(section, value);
        if (issues.Count > 0)
        {
            string message = string.Join("; ", issues.Select(issue =>
                string.IsNullOrWhiteSpace(issue.Field)
                    ? issue.Message
                    : issue.Field + ": " + issue.Message));
            throw new InvalidOperationException(
                $"Settings validation failed for section '{section}': {message}");
        }

        lock (_gate)
        {
            Dictionary<string, object?> store = target == SettingsTarget.Workspace ? _workspace : _user;
            if (value is null)
            {
                store.Remove(section);
            }
            else
            {
                store[section] = value;
            }
        }

        SectionChanged?.Invoke(this, new SettingsSectionChangedEventArgs(section, target, value));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IDisposable RegisterSchema(SettingsSectionSchema schema)
    {
        if (schema is null)
        {
            throw new ArgumentNullException(nameof(schema));
        }

        if (string.IsNullOrWhiteSpace(schema.Section))
        {
            throw new ArgumentException("Schema section cannot be empty.", nameof(schema));
        }

        lock (_gate)
        {
            _schemas[schema.Section] = schema;
        }

        return new SchemaRegistration(this, schema.Section);
    }

    /// <inheritdoc />
    public IReadOnlyList<SettingsSectionSchema> GetSchemas()
    {
        lock (_gate)
        {
            return _schemas.Values
                .OrderBy(schema => schema.Section, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <inheritdoc />
    public bool TryGetSchema(string section, out SettingsSectionSchema schema)
    {
        lock (_gate)
        {
            return _schemas.TryGetValue(section, out schema!);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SettingsValidationIssue> Validate(string section, object? value)
    {
        SettingsSectionSchema? schema;
        lock (_gate)
        {
            _ = _schemas.TryGetValue(section, out schema);
        }

        if (schema?.Validator is null)
        {
            return Array.Empty<SettingsValidationIssue>();
        }

        IReadOnlyList<SettingsValidationIssue>? issues = schema.Validator(value);
        return issues ?? Array.Empty<SettingsValidationIssue>();
    }

    /// <inheritdoc />
    public IDisposable SubscribeSection<T>(string section, Action<SettingsSectionChangedEventArgs<T>> handler)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            throw new ArgumentException("Section cannot be empty.", nameof(section));
        }

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        EventHandler<SettingsSectionChangedEventArgs> wrapped = (_, args) =>
        {
            if (!string.Equals(args.Section, section, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (args.Value is null)
            {
                handler(new SettingsSectionChangedEventArgs<T>(args.Section, args.Target, default));
                return;
            }

            if (args.Value is T typed)
            {
                handler(new SettingsSectionChangedEventArgs<T>(args.Section, args.Target, typed));
            }
        };

        SectionChanged += wrapped;
        return new SectionSubscription(() => SectionChanged -= wrapped);
    }

    private void UnregisterSchema(string section)
    {
        lock (_gate)
        {
            _schemas.Remove(section);
        }
    }

    private sealed class SchemaRegistration : IDisposable
    {
        private readonly InMemorySettingsStore _owner;
        private readonly string _section;
        private bool _disposed;

        public SchemaRegistration(InMemorySettingsStore owner, string section)
        {
            _owner = owner;
            _section = section;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.UnregisterSchema(_section);
        }
    }

    private sealed class SectionSubscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public SectionSubscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _unsubscribe();
        }
    }
}
