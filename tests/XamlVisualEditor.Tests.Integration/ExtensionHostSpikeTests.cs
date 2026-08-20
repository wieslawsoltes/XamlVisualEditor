using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace XamlVisualEditor.Tests.Integration;

public sealed class ExtensionHostSpikeTests
{
#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    [Fact]
    public async Task HostAndExtensionCommunicate()
    {
        string repoRoot = FindRepoRoot();
        string hostPath = ResolveExecutablePath(repoRoot, Path.Combine(
            "tools", "ExtensionHostSpike", "ExtensionHostSpike.Host", "bin", BuildConfiguration, "net10.0", "ExtensionHostSpike.Host"));
        string extensionPath = ResolveExecutablePath(repoRoot, Path.Combine(
            "tools", "ExtensionHostSpike", "ExtensionHostSpike.Extension", "bin", BuildConfiguration, "net10.0", "ExtensionHostSpike.Extension"));

        Assert.True(File.Exists(hostPath), "Host executable not found: " + hostPath);
        Assert.True(File.Exists(extensionPath), "Extension executable not found: " + extensionPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = hostPath,
            Arguments = "--extension-path " + QuoteArg(extensionPath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        Task<string> outputTask = process!.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        Task exitTask = process.WaitForExitAsync();

        Task completed = await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(10)));
        if (completed != exitTask)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            Assert.Fail("Timeout waiting for extension host spike.");
        }

        string output = await outputTask;
        string error = await errorTask;

        Assert.True(process.ExitCode == 0, "Process failed: " + error);
        Assert.Contains("Registered command", output);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "XamlVisualEditor.slnx");
            if (File.Exists(candidate))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private static string ResolveExecutablePath(string repoRoot, string relativePath)
    {
        string path = Path.Combine(repoRoot, relativePath);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            path += ".exe";
        }

        return path;
    }

    private static string QuoteArg(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal) ? '"' + value + '"' : value;
    }
}
