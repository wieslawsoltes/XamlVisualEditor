using System;
using System.Collections.Generic;
using System.Linq;

namespace XamlVisualEditor.Extensions.Debugging;

/// <summary>Supplies a debugger adapter path for a specific debugger service.</summary>
public interface IDebuggerAdapterLocator
{
    /// <summary>Resolves a debugger adapter path, or null if unavailable.</summary>
    string? ResolveAdapterPath();
}

/// <summary>Describes a debugger service registered by an extension.</summary>
public sealed record DebuggerServiceRegistration(
    string Id,
    string DisplayName,
    IDebuggerService Service,
    IDebuggerAdapterLocator? AdapterLocator = null);

/// <summary>Registry of debugger services exposed by extensions.</summary>
public interface IDebuggerServiceRegistry
{
    event EventHandler? Changed;

    IReadOnlyList<DebuggerServiceRegistration> Services { get; }

    string? ActiveServiceId { get; set; }

    DebuggerServiceRegistration? GetActiveRegistration();

    IDebuggerService? GetActiveService();

    void Register(DebuggerServiceRegistration registration, bool makeDefault = false);
}

/// <summary>Default in-memory implementation of <see cref="IDebuggerServiceRegistry"/>.</summary>
public sealed class DebuggerServiceRegistry : IDebuggerServiceRegistry
{
    private readonly List<DebuggerServiceRegistration> _services = new();
    private readonly object _gate = new();
    private string? _activeServiceId;

    public event EventHandler? Changed;

    public IReadOnlyList<DebuggerServiceRegistration> Services
    {
        get
        {
            lock (_gate)
            {
                return _services.ToList();
            }
        }
    }

    public string? ActiveServiceId
    {
        get
        {
            lock (_gate)
            {
                return _activeServiceId;
            }
        }
        set
        {
            bool updated = false;
            lock (_gate)
            {
                if (_activeServiceId == value)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(value) && _services.All(s => s.Id != value))
                {
                    return;
                }

                _activeServiceId = value;
                updated = true;
            }

            if (updated)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Register(DebuggerServiceRegistration registration, bool makeDefault = false)
    {
        bool updated = false;
        lock (_gate)
        {
            int existingIndex = _services.FindIndex(s => s.Id == registration.Id);
            if (existingIndex >= 0)
            {
                _services[existingIndex] = registration;
            }
            else
            {
                _services.Add(registration);
            }

            if (makeDefault || string.IsNullOrWhiteSpace(_activeServiceId))
            {
                _activeServiceId = registration.Id;
            }

            updated = true;
        }

        if (updated)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public IDebuggerService? GetActiveService()
    {
        lock (_gate)
        {
            return GetActiveRegistrationLocked()?.Service;
        }
    }

    public DebuggerServiceRegistration? GetActiveRegistration()
    {
        lock (_gate)
        {
            return GetActiveRegistrationLocked();
        }
    }

    private DebuggerServiceRegistration? GetActiveRegistrationLocked()
    {
        if (string.IsNullOrWhiteSpace(_activeServiceId))
        {
            return _services.FirstOrDefault();
        }

        return _services.FirstOrDefault(s => s.Id == _activeServiceId)
               ?? _services.FirstOrDefault();
    }
}
