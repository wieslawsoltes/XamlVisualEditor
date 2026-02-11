using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using XamlVisualEditor.Lsp;

namespace XamlVisualEditor.App.Services;

public sealed class LspSettingsStore : ILspSettingsStore
{
    private const string SettingsFileName = "lsp-servers.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public string SettingsPath { get; }

    public LspSettingsStore()
    {
        SettingsPath = ResolveSettingsPath();
    }

    public async Task<IReadOnlyList<LspServerConfiguration>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(SettingsPath))
        {
            return GetDefaultServers();
        }

        await using FileStream stream = File.OpenRead(SettingsPath);
        LspSettingsFile? file = await JsonSerializer.DeserializeAsync<LspSettingsFile>(stream, SerializerOptions, ct)
            .ConfigureAwait(false);

        return file?.Servers ?? Array.Empty<LspServerConfiguration>();
    }

    private static IReadOnlyList<LspServerConfiguration> GetDefaultServers()
    {
        return Array.Empty<LspServerConfiguration>();
    }

    public async Task SaveAsync(IReadOnlyList<LspServerConfiguration> servers, CancellationToken ct = default)
    {
        string? directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        LspSettingsFile file = new()
        {
            Servers = servers
        };

        await using FileStream stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, file, SerializerOptions, ct).ConfigureAwait(false);
    }

    private static string ResolveSettingsPath()
    {
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            baseDir = AppContext.BaseDirectory;
        }

        return Path.Combine(baseDir, "XamlVisualEditor", SettingsFileName);
    }

    private sealed class LspSettingsFile
    {
        public int Version { get; init; } = 1;

        public IReadOnlyList<LspServerConfiguration> Servers { get; init; } = Array.Empty<LspServerConfiguration>();
    }
}
