using System;
using System.IO;

namespace XamlVisualEditor.Terminal;

public static class TerminalCaptureArgs
{
    public const string CaptureArg = "--terminal-capture";

    public static string? ResolveCapturePath(string[] args, string baseDirectory, Func<DateTime>? clock = null)
    {
        if (args is null || args.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], CaptureArg, StringComparison.Ordinal))
            {
                continue;
            }

            string? next = i + 1 < args.Length ? args[i + 1] : null;
            if (string.IsNullOrWhiteSpace(next) || next.StartsWith("--", StringComparison.Ordinal))
            {
                DateTime now = (clock ?? (() => DateTime.UtcNow)).Invoke();
                string fileName = $"terminal-{now:yyyyMMdd-HHmmss}.xve.log";
                return Path.Combine(baseDirectory, "Captures", fileName);
            }

            return next;
        }

        return null;
    }
}
