using System.IO.Compression;
using System.Text.Json;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Loads extension packages from NuGet archives.</summary>
public sealed class ExtensionPackageLoader
{
    private const string ManifestFileName = "xve.extension.json";

    /// <summary>Loads the manifest from a NuGet package.</summary>
    public async Task<ExtensionPackageInfo> LoadAsync(string packagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new ArgumentException("Package path is required.", nameof(packagePath));
        }

        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("Package not found.", packagePath);
        }

        await using FileStream stream = File.OpenRead(packagePath);
        using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: false);
        ZipArchiveEntry? entry = archive.GetEntry(ManifestFileName);
        if (entry is null)
        {
            throw new InvalidOperationException("Package is missing " + ManifestFileName + ".");
        }

        await using Stream entryStream = entry.Open();
        using StreamReader reader = new(entryStream);
        string json = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

        ExtensionManifest manifest = ParseManifest(json);
        return new ExtensionPackageInfo(packagePath, manifest);
    }

    private static ExtensionManifest ParseManifest(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string name = GetRequiredString(root, "name");
        string publisher = GetRequiredString(root, "publisher");
        string version = GetRequiredString(root, "version");
        string? displayName = GetOptionalString(root, "displayName");
        string? main = GetOptionalString(root, "main");

        IReadOnlyList<string> activationEvents = GetStringArray(root, "activationEvents");
        JsonElement? engines = GetOptionalElement(root, "engines");
        JsonElement? contributes = GetOptionalElement(root, "contributes");

        return new ExtensionManifest
        {
            Name = name,
            Publisher = publisher,
            Version = version,
            DisplayName = displayName,
            Main = main,
            ActivationEvents = activationEvents,
            Engines = engines,
            Contributes = contributes
        };
    }

    private static string GetRequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Manifest is missing required property: " + name);
        }

        return value.GetString() ?? string.Empty;
    }

    private static string? GetOptionalString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        List<string> results = new();
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    results.Add(text);
                }
            }
        }

        return results.Count == 0 ? Array.Empty<string>() : results;
    }

    private static JsonElement? GetOptionalElement(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        return value.Clone();
    }
}
