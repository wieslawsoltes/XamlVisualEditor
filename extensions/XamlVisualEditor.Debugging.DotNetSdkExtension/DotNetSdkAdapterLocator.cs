using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XamlVisualEditor.Extensions.Debugging;

namespace XamlVisualEditor.Debugging.DotNetSdkExtension;

/// <summary>Resolves a VSDBG adapter path for .NET SDK debugging.</summary>
public sealed class DotNetSdkAdapterLocator : IDebuggerAdapterLocator
{
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly string _userProfilePath;

    public DotNetSdkAdapterLocator()
        : this(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
    {
    }

    internal DotNetSdkAdapterLocator(Func<string, string?> getEnvironmentVariable, string userProfilePath)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _userProfilePath = userProfilePath ?? string.Empty;
    }

    public string? ResolveAdapterPath()
    {
        string fileName = GetVsdbgFileName();

        string? fromEnv = GetFromEnvironment(fileName);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        string? fromDotnetCliHome = GetFromDotnetCliHome(fileName);
        if (!string.IsNullOrWhiteSpace(fromDotnetCliHome))
        {
            return fromDotnetCliHome;
        }

        string? fromHome = GetFromHome(fileName);
        if (!string.IsNullOrWhiteSpace(fromHome))
        {
            return fromHome;
        }

        string? fromVscode = GetFromVscodeExtensions(fileName);
        if (!string.IsNullOrWhiteSpace(fromVscode))
        {
            return fromVscode;
        }

        string? fromVisualStudio = GetFromVisualStudioInstall(fileName);
        if (!string.IsNullOrWhiteSpace(fromVisualStudio))
        {
            return fromVisualStudio;
        }

        return ResolveFromPath(fileName);
    }

    private string? GetFromEnvironment(string fileName)
    {
        return ResolveFromEnvironmentVariable("XVE_VSDBG_PATH", fileName)
               ?? ResolveFromEnvironmentVariable("VSDBG_PATH", fileName);
    }

    private string? GetFromDotnetCliHome(string fileName)
    {
        string? dotnetCliHome = _getEnvironmentVariable("DOTNET_CLI_HOME");
        if (string.IsNullOrWhiteSpace(dotnetCliHome))
        {
            return null;
        }

        string candidate = Path.Combine(dotnetCliHome, ".vsdbg", fileName);
        return IsValid(candidate) ? candidate : null;
    }

    private string? GetFromHome(string fileName)
    {
        if (string.IsNullOrWhiteSpace(_userProfilePath))
        {
            return null;
        }

        string candidate = Path.Combine(_userProfilePath, ".vsdbg", fileName);
        return IsValid(candidate) ? candidate : null;
    }

    private string? GetFromVscodeExtensions(string fileName)
    {
        foreach (string root in GetVscodeExtensionRoots())
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> extensionDirs;
            try
            {
                extensionDirs = Directory.EnumerateDirectories(root, "ms-dotnettools.csharp*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (string extensionDir in extensionDirs.OrderByDescending(path => path))
            {
                string? candidate = FindVsdbgInExtension(extensionDir, fileName);
                if (IsValid(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string? FindVsdbgInExtension(string extensionDir, string fileName)
    {
        string debuggerDir = Path.Combine(extensionDir, ".debugger");
        if (!Directory.Exists(debuggerDir))
        {
            return null;
        }

        try
        {
            string? candidate = Directory.EnumerateFiles(debuggerDir, fileName, SearchOption.AllDirectories)
                .FirstOrDefault();
            return candidate;
        }
        catch
        {
            return null;
        }
    }

    private IEnumerable<string> GetVscodeExtensionRoots()
    {
        if (string.IsNullOrWhiteSpace(_userProfilePath))
        {
            yield break;
        }

        yield return Path.Combine(_userProfilePath, ".vscode", "extensions");
        yield return Path.Combine(_userProfilePath, ".vscode-insiders", "extensions");
        yield return Path.Combine(_userProfilePath, ".vscode-oss", "extensions");
        yield return Path.Combine(_userProfilePath, ".vscode-server", "extensions");
        yield return Path.Combine(_userProfilePath, ".vscode-server-insiders", "extensions");
        yield return Path.Combine(_userProfilePath, ".vscodium", "extensions");

        if (OperatingSystem.IsLinux())
        {
            yield return Path.Combine(_userProfilePath, ".var", "app", "com.visualstudio.code", "data", "vscode", "extensions");
            yield return Path.Combine(_userProfilePath, ".var", "app", "com.visualstudio.code-insiders", "data", "vscode", "extensions");
            yield return Path.Combine(_userProfilePath, ".var", "app", "com.vscodium.codium", "data", "vscode", "extensions");
        }
    }

    private string? GetFromVisualStudioInstall(string fileName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        string? installDir = _getEnvironmentVariable("VSINSTALLDIR");
        if (!string.IsNullOrWhiteSpace(installDir))
        {
            string candidate = Path.Combine(
                installDir,
                "Common7",
                "IDE",
                "CommonExtensions",
                "Microsoft",
                "Debugger",
                fileName);
            if (IsValid(candidate))
            {
                return candidate;
            }
        }

        string? programFilesX86 = _getEnvironmentVariable("ProgramFiles(x86)");
        string? programFiles = _getEnvironmentVariable("ProgramFiles");
        foreach (string? root in new string?[] { programFilesX86, programFiles })
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string vsRoot = Path.Combine(root, "Microsoft Visual Studio");
            if (!Directory.Exists(vsRoot))
            {
                continue;
            }

            try
            {
                foreach (string versionDir in Directory.EnumerateDirectories(vsRoot))
                {
                    foreach (string editionDir in Directory.EnumerateDirectories(versionDir))
                    {
                        string candidate = Path.Combine(
                            editionDir,
                            "Common7",
                            "IDE",
                            "CommonExtensions",
                            "Microsoft",
                            "Debugger",
                            fileName);
                        if (IsValid(candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private string? ResolveFromPath(string fileName)
    {
        string? pathVar = _getEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVar))
        {
            return null;
        }

        foreach (string dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string? candidate = ResolveCandidate(dir, fileName);
                if (IsValid(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static bool IsValid(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    private string? ResolveFromEnvironmentVariable(string variableName, string fileName)
    {
        string? candidate = _getEnvironmentVariable(variableName);
        return ResolveCandidate(candidate, fileName);
    }

    private static string? ResolveCandidate(string? candidate, string fileName)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        if (File.Exists(candidate))
        {
            return candidate;
        }

        if (Directory.Exists(candidate))
        {
            string fileCandidate = Path.Combine(candidate, fileName);
            return File.Exists(fileCandidate) ? fileCandidate : null;
        }

        return null;
    }

    private static string GetVsdbgFileName()
    {
        return OperatingSystem.IsWindows() ? "vsdbg.exe" : "vsdbg";
    }
}
