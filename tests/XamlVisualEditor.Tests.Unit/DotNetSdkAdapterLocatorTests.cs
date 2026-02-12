using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using XamlVisualEditor.Debugging.DotNetSdkExtension;

namespace XamlVisualEditor.Tests.Unit;

public sealed class DotNetSdkAdapterLocatorTests
{
    [Fact]
    public void ResolveAdapterPath_UsesDotnetCliHome()
    {
        string root = Path.Combine(Path.GetTempPath(), "xve-tests", Guid.NewGuid().ToString("N"));
        string fileName = GetVsdbgFileName();
        string vsdbgPath = Path.Combine(root, ".vsdbg", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(vsdbgPath)!);
        File.WriteAllText(vsdbgPath, string.Empty);

        try
        {
            Dictionary<string, string?> env = new()
            {
                ["DOTNET_CLI_HOME"] = root
            };
            DotNetSdkAdapterLocator locator = new(key => env.TryGetValue(key, out string? value) ? value : null, Path.Combine(root, "profile"));

            string? resolved = locator.ResolveAdapterPath();

            Assert.Equal(vsdbgPath, resolved);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void ResolveAdapterPath_UsesVscodeExtensionDebugger()
    {
        string root = Path.Combine(Path.GetTempPath(), "xve-tests", Guid.NewGuid().ToString("N"));
        string fileName = GetVsdbgFileName();
        string vsdbgPath = Path.Combine(
            root,
            ".vscode",
            "extensions",
            "ms-dotnettools.csharp-1.2.3",
            ".debugger",
            fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(vsdbgPath)!);
        File.WriteAllText(vsdbgPath, string.Empty);

        try
        {
            DotNetSdkAdapterLocator locator = new(_ => null, root);

            string? resolved = locator.ResolveAdapterPath();

            Assert.Equal(vsdbgPath, resolved);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static string GetVsdbgFileName()
    {
        return OperatingSystem.IsWindows() ? "vsdbg.exe" : "vsdbg";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
