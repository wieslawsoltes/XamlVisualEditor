using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Formats.Tar;
using XamlVisualEditor.Core.Debugging;

namespace XamlVisualEditor.App.Services;

public sealed class DebugToolInstaller : IDebugToolInstaller
{
    private const string ToolId = "netcoredbg";
    private const string ToolVersion = "3.1.3-1062";
    private const string ToolFileName = "netcoredbg";
    private static readonly string ToolFileNameMac = ToolFileName;
    private static readonly string ToolArchiveName = "netcoredbg-osx-amd64.tar.gz";
    private static readonly string ToolDownloadUrl =
        "https://github.com/Samsung/netcoredbg/releases/download/3.1.3-1062/netcoredbg-osx-amd64.tar.gz";

    private readonly HttpClient _httpClient = new();
    private readonly string _toolsRoot;
    private readonly string _consentPath;
    private readonly HashSet<string> _consents = new(StringComparer.OrdinalIgnoreCase);
    private bool _consentLoaded;

    public DebugToolInstaller()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _toolsRoot = Path.Combine(appData, "XamlVisualEditor", "tools");
        _consentPath = Path.Combine(appData, "XamlVisualEditor", "debug-tools-consent.json");
    }

    public string? GetNetcoredbgPath()
    {
        string installDir = GetInstallDir();
        string binary = Path.Combine(installDir, "netcoredbg", ToolFileNameMac);
        return File.Exists(binary) ? binary : null;
    }

    public async Task<string?> EnsureNetcoredbgAsync(Func<DebugToolConsentRequest, Task<bool>> confirmAsync, CancellationToken ct = default)
    {
        string? existing = GetNetcoredbgPath();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        LoadConsent();
        if (!_consents.Contains(ToolId))
        {
            DebugToolConsentRequest request = new(
                ToolId,
                ToolVersion,
                ToolDownloadUrl,
                GetInstallDir(),
                "The debugger requires netcoredbg to be downloaded (~3.5 MB). Allow download?");
            bool allowed = await confirmAsync(request).ConfigureAwait(false);
            if (!allowed)
            {
                return null;
            }

            _consents.Add(ToolId);
            SaveConsent();
        }

        Directory.CreateDirectory(_toolsRoot);
        string tempArchive = Path.Combine(Path.GetTempPath(), ToolArchiveName);

        using (HttpResponseMessage response = await _httpClient.GetAsync(ToolDownloadUrl, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            await using FileStream target = File.Create(tempArchive);
            await response.Content.CopyToAsync(target, ct).ConfigureAwait(false);
        }

        string installDir = GetInstallDir();
        if (Directory.Exists(installDir))
        {
            Directory.Delete(installDir, recursive: true);
        }
        Directory.CreateDirectory(installDir);

        await ExtractTarGzAsync(tempArchive, installDir, ct).ConfigureAwait(false);

        string? binary = GetNetcoredbgPath();
        if (binary is not null)
        {
            TryMakeExecutable(binary);
        }

        return binary;
    }

    private string GetInstallDir()
    {
        return Path.Combine(_toolsRoot, ToolId, ToolVersion);
    }

    private void LoadConsent()
    {
        if (_consentLoaded)
        {
            return;
        }

        _consentLoaded = true;
        if (!File.Exists(_consentPath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(_consentPath);
            string[]? tools = JsonSerializer.Deserialize<string[]>(json);
            if (tools is null)
            {
                return;
            }

            foreach (string tool in tools)
            {
                _consents.Add(tool);
            }
        }
        catch
        {
        }
    }

    private void SaveConsent()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_consentPath) ?? _toolsRoot);
            string json = JsonSerializer.Serialize(_consents, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_consentPath, json);
        }
        catch
        {
        }
    }

    private static async Task ExtractTarGzAsync(string archivePath, string outputDir, CancellationToken ct)
    {
        await using FileStream fileStream = File.OpenRead(archivePath);
        await using GZipStream gzip = new(fileStream, CompressionMode.Decompress);
        TarReader reader = new(gzip);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.EntryType is TarEntryType.Directory)
            {
                string dirPath = Path.Combine(outputDir, entry.Name);
                Directory.CreateDirectory(dirPath);
                continue;
            }

            if (entry.DataStream is null)
            {
                continue;
            }

            string path = Path.Combine(outputDir, entry.Name);
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using FileStream outStream = File.Create(path);
            await entry.DataStream.CopyToAsync(outStream, ct).ConfigureAwait(false);
        }
    }

    private static void TryMakeExecutable(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
        }
    }
}
