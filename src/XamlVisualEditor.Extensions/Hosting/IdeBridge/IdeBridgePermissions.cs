using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Extensions.Hosting.IdeBridge;

/// <summary>Stores a workspace permission decision.</summary>
public sealed record IdeBridgeWorkspacePermissionState(
    string SessionToken,
    IdeBridgeCapabilities Capabilities,
    DateTimeOffset GrantedAt);

/// <summary>Allowlist entry for UI.</summary>
public sealed record IdeBridgeWorkspacePermissionEntry(
    string WorkspaceId,
    string SessionToken,
    IdeBridgeCapabilities Capabilities,
    DateTimeOffset GrantedAt);

/// <summary>Resolves permissions for IDE bridge sessions.</summary>
public sealed class IdeBridgePermissionService
{
    private const string SettingsSection = "ideBridge.permissions";
    private readonly ISettings _settings;
    private readonly IWindow _window;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates the permission service.</summary>
    public IdeBridgePermissionService(ISettings settings, IWindow window)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    /// <summary>Authorizes a session token or prompts for consent.</summary>
    public async Task<IdeBridgeWorkspacePermissionState?> AuthorizeAsync(string workspaceId, string? sessionToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Dictionary<string, IdeBridgeWorkspacePermissionState> states = await LoadStatesAsync(ct).ConfigureAwait(false);
            if (states.TryGetValue(workspaceId, out IdeBridgeWorkspacePermissionState? existing))
            {
                if (!string.IsNullOrWhiteSpace(sessionToken) && string.Equals(existing.SessionToken, sessionToken, StringComparison.Ordinal))
                {
                    return existing;
                }

                return null;
            }

            IdeBridgeWorkspacePermissionState? granted = await PromptForConsentAsync(ct).ConfigureAwait(false);
            if (granted is null)
            {
                return null;
            }

            states[workspaceId] = granted;
            await SaveStatesAsync(states, ct).ConfigureAwait(false);
            return granted;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Gets all allowlist entries.</summary>
    public async Task<IReadOnlyList<IdeBridgeWorkspacePermissionEntry>> GetPermissionsAsync(CancellationToken ct)
    {
        Dictionary<string, IdeBridgeWorkspacePermissionState> states = await LoadStatesAsync(ct).ConfigureAwait(false);
        List<IdeBridgeWorkspacePermissionEntry> results = new(states.Count);
        foreach (KeyValuePair<string, IdeBridgeWorkspacePermissionState> entry in states)
        {
            IdeBridgeWorkspacePermissionState state = entry.Value;
            results.Add(new IdeBridgeWorkspacePermissionEntry(entry.Key, state.SessionToken, state.Capabilities, state.GrantedAt));
        }

        return results;
    }

    /// <summary>Clears a single allowlist entry.</summary>
    public async Task ClearPermissionAsync(string workspaceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return;
        }

        Dictionary<string, IdeBridgeWorkspacePermissionState> states = await LoadStatesAsync(ct).ConfigureAwait(false);
        if (states.Remove(workspaceId))
        {
            await SaveStatesAsync(states, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Clears all allowlist entries.</summary>
    public Task ClearAllAsync(CancellationToken ct)
    {
        return SaveStatesAsync(new Dictionary<string, IdeBridgeWorkspacePermissionState>(StringComparer.Ordinal), ct);
    }

    private async Task<IdeBridgeWorkspacePermissionState?> PromptForConsentAsync(CancellationToken ct)
    {
        IReadOnlyList<QuickPickItem> items = new[]
        {
            new QuickPickItem("Allow read-only", "Read files and diagnostics", null),
            new QuickPickItem("Allow full access", "Read and write files, commands, terminals", null),
            new QuickPickItem("Deny", "Block this client", null)
        };

        QuickPickItem? choice = await _window.ShowQuickPickAsync(items, new QuickPickOptions("IDE Bridge access", false), ct).ConfigureAwait(false);
        if (choice is null)
        {
            return null;
        }

        if (string.Equals(choice.Label, "Deny", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        IdeBridgeCapabilities caps = string.Equals(choice.Label, "Allow full access", StringComparison.OrdinalIgnoreCase)
            ? CreateFullCapabilities()
            : CreateReadOnlyCapabilities();

        return new IdeBridgeWorkspacePermissionState(CreateToken(), caps, DateTimeOffset.UtcNow);
    }

    private static IdeBridgeCapabilities CreateReadOnlyCapabilities()
    {
        return new IdeBridgeCapabilities(
            Files: true,
            Commands: false,
            Diagnostics: true,
            Terminal: false,
            Ui: true,
            Documents: true,
            Selection: true,
            Workspace: true,
            Write: false);
    }

    private static IdeBridgeCapabilities CreateFullCapabilities()
    {
        return new IdeBridgeCapabilities(
            Files: true,
            Commands: true,
            Diagnostics: true,
            Terminal: true,
            Ui: true,
            Documents: true,
            Selection: true,
            Workspace: true,
            Write: true);
    }

    private Task<Dictionary<string, IdeBridgeWorkspacePermissionState>> LoadStatesAsync(CancellationToken ct)
    {
        Dictionary<string, IdeBridgeWorkspacePermissionState>? states = _settings
            .Get<Dictionary<string, IdeBridgeWorkspacePermissionState>>(SettingsSection);

        return Task.FromResult(states ?? new Dictionary<string, IdeBridgeWorkspacePermissionState>(StringComparer.Ordinal));
    }

    private Task SaveStatesAsync(Dictionary<string, IdeBridgeWorkspacePermissionState> states, CancellationToken ct)
    {
        return _settings.UpdateAsync(SettingsSection, states, SettingsTarget.Workspace, ct);
    }

    private static string CreateToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
