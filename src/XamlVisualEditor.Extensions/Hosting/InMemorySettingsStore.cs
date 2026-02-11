using System.Collections.Generic;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>In-memory settings store with user/workspace precedence.</summary>
public sealed class InMemorySettingsStore : ISettings
{
    private readonly object _gate = new();
    private readonly Dictionary<string, object?> _user = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _workspace = new(StringComparer.Ordinal);

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

        return Task.CompletedTask;
    }
}
