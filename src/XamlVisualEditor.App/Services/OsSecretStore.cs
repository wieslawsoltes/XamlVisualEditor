using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Acp;

namespace XamlVisualEditor.App.Services;

public sealed class OsSecretStore : ISecretStore
{
    private const string ServiceName = "XamlVisualEditor.ACP";

    public Task<string?> GetSecretAsync(string key, CancellationToken ct)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetMacSecretAsync(key, ct);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsSecretAsync(key, ct);
        }

        return GetLinuxSecretAsync(key, ct);
    }

    public Task SetSecretAsync(string key, string secret, CancellationToken ct)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return SetMacSecretAsync(key, secret, ct);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return SetWindowsSecretAsync(key, secret, ct);
        }

        return SetLinuxSecretAsync(key, secret, ct);
    }

    public Task RemoveSecretAsync(string key, CancellationToken ct)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RemoveMacSecretAsync(key, ct);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RemoveWindowsSecretAsync(key, ct);
        }

        return RemoveLinuxSecretAsync(key, ct);
    }

    private static async Task<string?> GetMacSecretAsync(string key, CancellationToken ct)
    {
        ProcessResult result = await RunProcessAsync(
            "security",
            new[] { "find-generic-password", "-a", key, "-s", ServiceName, "-w" },
            null,
            ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(result.Output) ? null : result.Output.Trim();
    }

    private static async Task SetMacSecretAsync(string key, string secret, CancellationToken ct)
    {
        await RunProcessAsync(
            "security",
            new[] { "add-generic-password", "-a", key, "-s", ServiceName, "-w", secret, "-U" },
            null,
            ct).ConfigureAwait(false);
    }

    private static async Task RemoveMacSecretAsync(string key, CancellationToken ct)
    {
        await RunProcessAsync(
            "security",
            new[] { "delete-generic-password", "-a", key, "-s", ServiceName },
            null,
            ct).ConfigureAwait(false);
    }

    [SupportedOSPlatform("windows")]
    private static Task<string?> GetWindowsSecretAsync(string key, CancellationToken ct)
    {
        string path = GetWindowsSecretPath(key);
        if (!File.Exists(path))
        {
            return Task.FromResult<string?>(null);
        }

        byte[] protectedBytes = File.ReadAllBytes(path);
        byte[] raw = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        string secret = Encoding.UTF8.GetString(raw);
        return Task.FromResult<string?>(secret);
    }

    [SupportedOSPlatform("windows")]
    private static Task SetWindowsSecretAsync(string key, string secret, CancellationToken ct)
    {
        string path = GetWindowsSecretPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        byte[] raw = Encoding.UTF8.GetBytes(secret);
        byte[] protectedBytes = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);
        return Task.CompletedTask;
    }

    [SupportedOSPlatform("windows")]
    private static Task RemoveWindowsSecretAsync(string key, CancellationToken ct)
    {
        string path = GetWindowsSecretPath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private static async Task<string?> GetLinuxSecretAsync(string key, CancellationToken ct)
    {
        ProcessResult result = await RunProcessAsync(
            "secret-tool",
            new[] { "lookup", "service", ServiceName, "account", key },
            null,
            ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(result.Output) ? null : result.Output.Trim();
    }

    private static async Task SetLinuxSecretAsync(string key, string secret, CancellationToken ct)
    {
        await RunProcessAsync(
            "secret-tool",
            new[] { "store", "--label=ACP API Key", "service", ServiceName, "account", key },
            secret,
            ct).ConfigureAwait(false);
    }

    private static async Task RemoveLinuxSecretAsync(string key, CancellationToken ct)
    {
        await RunProcessAsync(
            "secret-tool",
            new[] { "clear", "service", ServiceName, "account", key },
            null,
            ct).ConfigureAwait(false);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        string[] args,
        string? input,
        CancellationToken ct)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input is not null,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process process = new() { StartInfo = startInfo };
        process.Start();

        if (input is not null)
        {
            await process.StandardInput.WriteAsync(input).ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
            process.StandardInput.Close();
        }

        string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, output);
    }

    [SupportedOSPlatform("windows")]
    private static string GetWindowsSecretPath(string key)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, "XamlVisualEditor", "Secrets");
        string safeName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(dir, safeName + ".bin");
    }

    private readonly record struct ProcessResult(int ExitCode, string Output);
}
