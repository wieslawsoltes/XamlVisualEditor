using System;
using System.Collections.Generic;
using System.Linq;
using XamlVisualEditor.Lsp;

namespace XamlVisualEditor.App.Services;

public sealed class LspSettings : ILspSettings
{
    private const string CSharpPathEnv = "XVE_LSP_CSHARP_PATH";
    private const string CSharpArgsEnv = "XVE_LSP_CSHARP_ARGS";
    private const string CSharpWorkDirEnv = "XVE_LSP_CSHARP_WORKDIR";
    private const string CSharpExtEnv = "XVE_LSP_CSHARP_EXTENSIONS";

    private const string XamlPathEnv = "XVE_LSP_XAML_PATH";
    private const string XamlArgsEnv = "XVE_LSP_XAML_ARGS";
    private const string XamlWorkDirEnv = "XVE_LSP_XAML_WORKDIR";
    private const string XamlExtEnv = "XVE_LSP_XAML_EXTENSIONS";

    private readonly ILspSettingsStore _store;

    public LspSettings(ILspSettingsStore store)
    {
        _store = store;
        Servers = BuildServers();
    }

    public IReadOnlyList<LspServerConfiguration> Servers { get; }

    private IReadOnlyList<LspServerConfiguration> BuildServers()
    {
        List<LspServerConfiguration> servers = new();
        IReadOnlyList<LspServerConfiguration> fileServers = LoadFileServers();
        foreach (LspServerConfiguration server in fileServers)
        {
            servers.Add(server);
        }

        string? csharpPath = Environment.GetEnvironmentVariable(CSharpPathEnv);
        if (!string.IsNullOrWhiteSpace(csharpPath))
        {
            servers.Add(new LspServerConfiguration
            {
                LanguageId = "csharp",
                ServerPath = csharpPath,
                Arguments = SplitList(Environment.GetEnvironmentVariable(CSharpArgsEnv)),
                WorkingDirectory = Environment.GetEnvironmentVariable(CSharpWorkDirEnv),
                FileExtensions = ResolveExtensions(Environment.GetEnvironmentVariable(CSharpExtEnv), ".cs")
            });
        }

        string? xamlPath = Environment.GetEnvironmentVariable(XamlPathEnv);
        if (!string.IsNullOrWhiteSpace(xamlPath))
        {
            servers.Add(new LspServerConfiguration
            {
                LanguageId = "xaml",
                ServerPath = xamlPath,
                Arguments = SplitList(Environment.GetEnvironmentVariable(XamlArgsEnv)),
                WorkingDirectory = Environment.GetEnvironmentVariable(XamlWorkDirEnv),
                FileExtensions = ResolveExtensions(Environment.GetEnvironmentVariable(XamlExtEnv), ".xaml", ".axaml")
            });
        }

        return MergeByLanguageId(servers);
    }

    private IReadOnlyList<LspServerConfiguration> LoadFileServers()
    {
        try
        {
            return _store.LoadAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return Array.Empty<LspServerConfiguration>();
        }
    }

    private static IReadOnlyList<LspServerConfiguration> MergeByLanguageId(
        IReadOnlyList<LspServerConfiguration> servers)
    {
        Dictionary<string, LspServerConfiguration> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (LspServerConfiguration server in servers)
        {
            if (!map.ContainsKey(server.LanguageId))
            {
                map[server.LanguageId] = server;
            }
        }

        return map.Values.ToList();
    }

    private static IReadOnlyList<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<string> ResolveExtensions(string? value, params string[] defaults)
    {
        IReadOnlyList<string> parsed = SplitList(value);
        if (parsed.Count > 0)
        {
            return NormalizeExtensions(parsed);
        }

        return NormalizeExtensions(defaults);
    }

    private static IReadOnlyList<string> NormalizeExtensions(IEnumerable<string> extensions)
    {
        List<string> normalized = new();
        foreach (string extension in extensions)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                continue;
            }

            string value = extension.StartsWith(".", StringComparison.Ordinal)
                ? extension
                : "." + extension;

            normalized.Add(value);
        }

        return normalized;
    }
}
