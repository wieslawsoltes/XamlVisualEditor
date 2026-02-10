using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Acp;

namespace XamlVisualEditor.App.Services;

public sealed class AcpProfileStore : IAcpProfileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public async Task<IReadOnlyList<AcpProfile>> LoadAsync(CancellationToken ct)
    {
        string path = GetProfilesPath();
        if (!File.Exists(path))
        {
            return new List<AcpProfile> { AcpProfile.CreateCodexProfile() };
        }

        try
        {
            await using FileStream stream = File.OpenRead(path);
            List<AcpProfile>? profiles = await JsonSerializer.DeserializeAsync<List<AcpProfile>>(stream, SerializerOptions, ct)
                .ConfigureAwait(false);
            List<AcpProfile> result = profiles ?? new List<AcpProfile>();
            EnsureCodexProfile(result);
            return result;
        }
        catch
        {
            return new List<AcpProfile> { AcpProfile.CreateCodexProfile() };
        }
    }

    public async Task SaveAsync(IReadOnlyList<AcpProfile> profiles, CancellationToken ct)
    {
        string path = GetProfilesPath();
        List<AcpProfile> snapshot = profiles.Select(CloneProfile).ToList();
        EnsureCodexProfile(snapshot);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, ct).ConfigureAwait(false);
    }

    private static void EnsureCodexProfile(List<AcpProfile> profiles)
    {
        if (profiles.Any(profile => string.Equals(profile.Id, "codex", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        profiles.Insert(0, AcpProfile.CreateCodexProfile());
    }

    private static string GetProfilesPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, "XamlVisualEditor");
        return Path.Combine(dir, "acp-profiles.json");
    }

    private static AcpProfile CloneProfile(AcpProfile profile)
    {
        return new AcpProfile
        {
            Id = profile.Id,
            Name = profile.Name,
            Description = profile.Description,
            Command = profile.Command,
            Arguments = new List<string>(profile.Arguments),
            WorkingDirectory = profile.WorkingDirectory,
            Environment = new Dictionary<string, string>(profile.Environment, StringComparer.Ordinal),
            Model = profile.Model,
            ModelEnvVar = profile.ModelEnvVar,
            ApiKeyEnvVar = profile.ApiKeyEnvVar,
            OAuthClientId = profile.OAuthClientId,
            OAuthScopes = profile.OAuthScopes,
            OAuthDeviceCodeUrl = profile.OAuthDeviceCodeUrl,
            OAuthTokenUrl = profile.OAuthTokenUrl,
            UseKeychain = profile.UseKeychain,
            IsBuiltIn = profile.IsBuiltIn
        };
    }
}
