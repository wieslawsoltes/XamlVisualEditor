using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>
/// Host-backed runtime permission flow for extensions.
/// </summary>
public sealed class ExtensionPermissionService : IExtensionPermissions
{
    private const string SettingsPrefix = "extensions.permissions";
    private const string AllowOnceLabel = "Allow once";
    private const string AlwaysAllowLabel = "Always allow";
    private const string DenyOnceLabel = "Deny once";
    private const string AlwaysDenyLabel = "Always deny";

    private readonly string _extensionId;
    private readonly string _settingsSection;
    private readonly ISettings _settings;
    private readonly IWindow _window;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _declarationsGate = new();
    private readonly Dictionary<string, ExtensionCapabilityDeclaration> _declarations = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a permission service for one extension.
    /// </summary>
    public ExtensionPermissionService(string extensionId, ISettings settings, IWindow window)
    {
        if (string.IsNullOrWhiteSpace(extensionId))
        {
            throw new ArgumentException("Extension id is required.", nameof(extensionId));
        }

        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _extensionId = extensionId;
        _settingsSection = SettingsPrefix + "." + extensionId;
    }

    /// <inheritdoc />
    public event EventHandler<ExtensionPermissionAuditEventArgs>? AccessAudited;

    /// <inheritdoc />
    public event EventHandler<ExtensionPermissionChangedEventArgs>? Changed;

    /// <inheritdoc />
    public void Declare(IReadOnlyList<ExtensionCapabilityDeclaration> capabilities)
    {
        if (capabilities is null)
        {
            throw new ArgumentNullException(nameof(capabilities));
        }

        lock (_declarationsGate)
        {
            foreach (ExtensionCapabilityDeclaration capability in capabilities)
            {
                if (string.IsNullOrWhiteSpace(capability.CapabilityId))
                {
                    throw new ArgumentException("Capability id is required.", nameof(capabilities));
                }

                if (string.IsNullOrWhiteSpace(capability.DisplayName))
                {
                    throw new ArgumentException("Capability display name is required.", nameof(capabilities));
                }

                _declarations[capability.CapabilityId] = capability;
            }
        }
    }

    /// <inheritdoc />
    public async Task<ExtensionPermissionDecision> RequestAsync(string capabilityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(capabilityId))
        {
            throw new ArgumentException("Capability id is required.", nameof(capabilityId));
        }

        ExtensionCapabilityDeclaration? declaration = GetDeclaration(capabilityId);
        if (declaration is null)
        {
            return Audit(new ExtensionPermissionDecision(
                capabilityId,
                IsAllowed: false,
                IsRemembered: false,
                ExtensionPermissionDecisionSource.Undeclared,
                DateTimeOffset.UtcNow));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, StoredPermissionEntry> remembered = LoadRemembered();
            if (remembered.TryGetValue(capabilityId, out StoredPermissionEntry? stored))
            {
                return Audit(new ExtensionPermissionDecision(
                    capabilityId,
                    stored.IsAllowed,
                    IsRemembered: true,
                    ExtensionPermissionDecisionSource.Remembered,
                    stored.GrantedAt));
            }

            QuickPickItem? choice = await PromptAsync(declaration!, cancellationToken).ConfigureAwait(false);
            ExtensionPermissionDecision decision = CreateDecision(capabilityId, choice);
            if (decision.IsRemembered)
            {
                remembered[capabilityId] = new StoredPermissionEntry(decision.IsAllowed, decision.DecidedAt);
                await SaveRememberedAsync(remembered, cancellationToken).ConfigureAwait(false);
                Changed?.Invoke(this, new ExtensionPermissionChangedEventArgs(capabilityId, decision.IsAllowed));
            }

            return Audit(decision);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExtensionPermissionEntry>> GetRememberedAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, StoredPermissionEntry> remembered = LoadRemembered();
            List<ExtensionPermissionEntry> results = new(remembered.Count);
            foreach (KeyValuePair<string, StoredPermissionEntry> entry in remembered)
            {
                results.Add(new ExtensionPermissionEntry(entry.Key, entry.Value.IsAllowed, entry.Value.GrantedAt));
            }

            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearRememberedAsync(string? capabilityId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (string.IsNullOrWhiteSpace(capabilityId))
            {
                await SaveRememberedAsync(new Dictionary<string, StoredPermissionEntry>(StringComparer.OrdinalIgnoreCase), cancellationToken)
                    .ConfigureAwait(false);
                Changed?.Invoke(this, new ExtensionPermissionChangedEventArgs(null, null));
                return;
            }

            Dictionary<string, StoredPermissionEntry> remembered = LoadRemembered();
            if (!remembered.Remove(capabilityId))
            {
                return;
            }

            await SaveRememberedAsync(remembered, cancellationToken).ConfigureAwait(false);
            Changed?.Invoke(this, new ExtensionPermissionChangedEventArgs(capabilityId, null));
        }
        finally
        {
            _gate.Release();
        }
    }

    private ExtensionCapabilityDeclaration? GetDeclaration(string capabilityId)
    {
        lock (_declarationsGate)
        {
            _declarations.TryGetValue(capabilityId, out ExtensionCapabilityDeclaration? declaration);
            return declaration;
        }
    }

    private async Task<QuickPickItem?> PromptAsync(ExtensionCapabilityDeclaration declaration, CancellationToken cancellationToken)
    {
        string risk = declaration.IsHighRisk ? "High risk capability" : "Standard capability";
        IReadOnlyList<QuickPickItem> choices =
        [
            new QuickPickItem(AllowOnceLabel, "Grant access for this request only", risk),
            new QuickPickItem(AlwaysAllowLabel, "Remember and allow future requests", risk),
            new QuickPickItem(DenyOnceLabel, "Block this request only", risk),
            new QuickPickItem(AlwaysDenyLabel, "Remember and block future requests", risk)
        ];

        string title = _extensionId + " requests " + declaration.DisplayName;
        return await _window.ShowQuickPickAsync(choices, new QuickPickOptions(title, false), cancellationToken).ConfigureAwait(false);
    }

    private static ExtensionPermissionDecision CreateDecision(string capabilityId, QuickPickItem? choice)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (choice is null)
        {
            return new ExtensionPermissionDecision(
                capabilityId,
                IsAllowed: false,
                IsRemembered: false,
                ExtensionPermissionDecisionSource.Dismissed,
                now);
        }

        return choice.Label switch
        {
            AlwaysAllowLabel => new ExtensionPermissionDecision(capabilityId, true, true, ExtensionPermissionDecisionSource.Prompt, now),
            AllowOnceLabel => new ExtensionPermissionDecision(capabilityId, true, false, ExtensionPermissionDecisionSource.Prompt, now),
            AlwaysDenyLabel => new ExtensionPermissionDecision(capabilityId, false, true, ExtensionPermissionDecisionSource.Prompt, now),
            DenyOnceLabel => new ExtensionPermissionDecision(capabilityId, false, false, ExtensionPermissionDecisionSource.Prompt, now),
            _ => new ExtensionPermissionDecision(capabilityId, false, false, ExtensionPermissionDecisionSource.Dismissed, now)
        };
    }

    private Dictionary<string, StoredPermissionEntry> LoadRemembered()
    {
        Dictionary<string, StoredPermissionEntry>? remembered =
            _settings.Get<Dictionary<string, StoredPermissionEntry>>(_settingsSection);
        if (remembered is null)
        {
            return new Dictionary<string, StoredPermissionEntry>(StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, StoredPermissionEntry>(remembered, StringComparer.OrdinalIgnoreCase);
    }

    private Task SaveRememberedAsync(Dictionary<string, StoredPermissionEntry> remembered, CancellationToken cancellationToken)
    {
        return _settings.UpdateAsync(_settingsSection, remembered, SettingsTarget.User, cancellationToken);
    }

    private ExtensionPermissionDecision Audit(ExtensionPermissionDecision decision)
    {
        AccessAudited?.Invoke(
            this,
            new ExtensionPermissionAuditEventArgs(
                decision.CapabilityId,
                decision.IsAllowed,
                decision.IsRemembered,
                decision.Source,
                decision.DecidedAt));
        return decision;
    }

    private sealed record StoredPermissionEntry(bool IsAllowed, DateTimeOffset GrantedAt);
}
