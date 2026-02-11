namespace XamlVisualEditor.Extensions.Hosting.VscodeCompat;

/// <summary>Resolves VS Code compatibility host paths.</summary>
public static class VscodeCompatHostPaths
{
    /// <summary>Gets the default VS Code extensions root.</summary>
    public static string GetDefaultExtensionsRoot()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".vscode", "extensions");
    }

    /// <summary>Resolves the host script path.</summary>
    public static string? LocateHostScriptPath()
    {
        string baseDir = AppContext.BaseDirectory;
        string? candidate = TryGetHostScript(baseDir);
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        string? repoRoot = TryFindRepoRoot(baseDir);
        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            string repoCandidate = Path.Combine(repoRoot, "tools", "VscodeExtensionHost", "host.js");
            if (File.Exists(repoCandidate))
            {
                return repoCandidate;
            }
        }

        return null;
    }

    /// <summary>Gets the node module search path for the host.</summary>
    public static string? GetNodeModulePath(string hostScriptPath)
    {
        if (string.IsNullOrWhiteSpace(hostScriptPath))
        {
            return null;
        }

        string? hostDir = Path.GetDirectoryName(hostScriptPath);
        if (string.IsNullOrWhiteSpace(hostDir))
        {
            return null;
        }

        string nodeModules = Path.Combine(hostDir, "node_modules");
        if (Directory.Exists(nodeModules))
        {
            return nodeModules;
        }

        return hostDir;
    }

    private static string? TryGetHostScript(string baseDir)
    {
        string candidate = Path.Combine(baseDir, "vscode-compat-host", "host.js");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        candidate = Path.Combine(baseDir, "tools", "VscodeExtensionHost", "host.js");
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? TryFindRepoRoot(string startDir)
    {
        var current = new DirectoryInfo(startDir);
        while (current is not null)
        {
            string marker = Path.Combine(current.FullName, "XamlVisualEditor.slnx");
            if (File.Exists(marker))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
