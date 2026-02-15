namespace XamlVisualEditor.Extensions;

/// <summary>
/// Provides extension capability declaration and runtime permission checks.
/// </summary>
public interface IExtensionPermissions
{
    /// <summary>
    /// Raised whenever a permission access is evaluated.
    /// </summary>
    event EventHandler<ExtensionPermissionAuditEventArgs>? AccessAudited;

    /// <summary>
    /// Raised when a remembered permission decision changes.
    /// </summary>
    event EventHandler<ExtensionPermissionChangedEventArgs>? Changed;

    /// <summary>
    /// Declares extension capabilities that may request access at runtime.
    /// </summary>
    void Declare(IReadOnlyList<ExtensionCapabilityDeclaration> capabilities);

    /// <summary>
    /// Requests permission for a declared capability.
    /// </summary>
    Task<ExtensionPermissionDecision> RequestAsync(string capabilityId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets remembered permission decisions for the extension.
    /// </summary>
    Task<IReadOnlyList<ExtensionPermissionEntry>> GetRememberedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Clears remembered permission decisions.
    /// </summary>
    Task ClearRememberedAsync(string? capabilityId, CancellationToken cancellationToken);
}

/// <summary>
/// Declares a capability that can be granted or denied by the user.
/// </summary>
public sealed record ExtensionCapabilityDeclaration(
    string CapabilityId,
    string DisplayName,
    string Description,
    bool IsHighRisk = false);

/// <summary>
/// Describes how a permission decision was resolved.
/// </summary>
public enum ExtensionPermissionDecisionSource
{
    /// <summary>
    /// Decision was selected by the user during a runtime prompt.
    /// </summary>
    Prompt,

    /// <summary>
    /// Decision came from remembered settings.
    /// </summary>
    Remembered,

    /// <summary>
    /// Capability was not declared and access was denied.
    /// </summary>
    Undeclared,

    /// <summary>
    /// Prompt was dismissed and access was denied.
    /// </summary>
    Dismissed
}

/// <summary>
/// Represents a permission decision for a capability request.
/// </summary>
public sealed record ExtensionPermissionDecision(
    string CapabilityId,
    bool IsAllowed,
    bool IsRemembered,
    ExtensionPermissionDecisionSource Source,
    DateTimeOffset DecidedAt);

/// <summary>
/// Represents a remembered permission entry.
/// </summary>
public sealed record ExtensionPermissionEntry(
    string CapabilityId,
    bool IsAllowed,
    DateTimeOffset GrantedAt);

/// <summary>
/// Raised when a remembered permission decision is updated.
/// </summary>
public sealed class ExtensionPermissionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates event args.
    /// </summary>
    public ExtensionPermissionChangedEventArgs(string? capabilityId, bool? isAllowed)
    {
        CapabilityId = capabilityId;
        IsAllowed = isAllowed;
    }

    /// <summary>
    /// Gets the affected capability id. Null means all capabilities were cleared.
    /// </summary>
    public string? CapabilityId { get; }

    /// <summary>
    /// Gets the remembered allowed/denied state when available.
    /// </summary>
    public bool? IsAllowed { get; }
}

/// <summary>
/// Raised when a capability access request is evaluated.
/// </summary>
public sealed class ExtensionPermissionAuditEventArgs : EventArgs
{
    /// <summary>
    /// Creates event args.
    /// </summary>
    public ExtensionPermissionAuditEventArgs(
        string capabilityId,
        bool isAllowed,
        bool isRemembered,
        ExtensionPermissionDecisionSource source,
        DateTimeOffset timestamp)
    {
        CapabilityId = capabilityId;
        IsAllowed = isAllowed;
        IsRemembered = isRemembered;
        Source = source;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the capability id.
    /// </summary>
    public string CapabilityId { get; }

    /// <summary>
    /// Gets whether access was allowed.
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Gets whether the decision came from remembered state.
    /// </summary>
    public bool IsRemembered { get; }

    /// <summary>
    /// Gets the decision source.
    /// </summary>
    public ExtensionPermissionDecisionSource Source { get; }

    /// <summary>
    /// Gets the decision timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; }
}
