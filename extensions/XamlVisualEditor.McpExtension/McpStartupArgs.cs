using System;
using System.Collections.Generic;
using XamlVisualEditor.Extensions.Hosting.Mcp;

namespace XamlVisualEditor.McpExtension;

/// <summary>Applies MCP command-line overrides for the current XVE process.</summary>
public static class McpStartupArgs
{
    /// <summary>
    /// Applies <c>--mcp</c>, <c>--mcp-transport</c>, <c>--mcp-port</c>, and
    /// <c>--mcp-path</c> arguments to the supplied settings.
    /// </summary>
    public static McpSettings Apply(McpSettings settings, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(args);

        bool enabled = settings.Enabled;
        string? transport = settings.Transport;
        int port = settings.HttpPort;
        string? path = settings.HttpPath;

        for (int i = 0; i < args.Count; i++)
        {
            string argument = args[i];
            if (string.Equals(argument, "--mcp", StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                transport = "http";
                continue;
            }

            if (argument.StartsWith("--mcp=", StringComparison.OrdinalIgnoreCase))
            {
                string value = argument.Substring("--mcp=".Length);
                if (IsTransport(value))
                {
                    enabled = true;
                    transport = value.ToLowerInvariant();
                }
                continue;
            }

            if (TryReadValue(args, ref i, argument, "--mcp-transport", out string? transportValue)
                && IsTransport(transportValue))
            {
                enabled = true;
                transport = transportValue!.ToLowerInvariant();
                continue;
            }

            if (TryReadValue(args, ref i, argument, "--mcp-port", out string? portValue)
                && int.TryParse(portValue, out int parsedPort)
                && parsedPort is > 0 and <= 65535)
            {
                enabled = true;
                port = parsedPort;
                continue;
            }

            if (TryReadValue(args, ref i, argument, "--mcp-path", out string? pathValue)
                && !string.IsNullOrWhiteSpace(pathValue))
            {
                enabled = true;
                path = NormalizePath(pathValue);
            }
        }

        return new McpSettings(enabled, transport, port, path);
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        string argument,
        string option,
        out string? value)
    {
        value = null;
        if (!string.Equals(argument, option, StringComparison.OrdinalIgnoreCase) || index + 1 >= args.Count)
        {
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool IsTransport(string? value)
    {
        return string.Equals(value, "stdio", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "http", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "both", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        string normalized = path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
        return normalized.EndsWith("/", StringComparison.Ordinal) ? normalized : normalized + "/";
    }
}
