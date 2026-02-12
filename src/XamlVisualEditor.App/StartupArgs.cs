using System;
using System.IO;

namespace XamlVisualEditor.App;

public static class StartupArgs
{
    public static string? GetWorkspacePath(string[]? args)
    {
        if (args is null || args.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "--open", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--workspace", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }
        }

        if (args.Length == 1 && IsWorkspacePath(args[0]))
        {
            return args[0];
        }

        return null;
    }

    private static bool IsWorkspacePath(string value)
    {
        string extension = Path.GetExtension(value);
        return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase);
    }
}
