using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Lsp;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>Shell-backed LSP settings host adapter.</summary>
public sealed class LspSettingsHostAdapter : ILspSettingsHost
{
    private readonly ILspSettingsStore? _store;

    public LspSettingsHostAdapter(ILspSettingsStore? store)
    {
        _store = store;
    }

    public string SettingsPath => _store?.SettingsPath ?? string.Empty;

    public event EventHandler<LspSettingsChangedEventArgs>? Changed;

    public async Task<IReadOnlyList<LspServerSettings>> LoadServersAsync(CancellationToken cancellationToken)
    {
        if (_store is null)
        {
            return Array.Empty<LspServerSettings>();
        }

        IReadOnlyList<LspServerConfiguration> servers = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        return servers.Select(MapServer).ToList();
    }

    public async Task SaveServersAsync(IReadOnlyList<LspServerSettings> servers, CancellationToken cancellationToken)
    {
        if (_store is null)
        {
            return;
        }

        List<LspServerConfiguration> mapped = new(servers.Count);
        foreach (LspServerSettings server in servers)
        {
            mapped.Add(new LspServerConfiguration
            {
                LanguageId = server.LanguageId.Trim(),
                ServerPath = server.ServerPath.Trim(),
                Arguments = SplitAndNormalize(server.Arguments),
                WorkingDirectory = string.IsNullOrWhiteSpace(server.WorkingDirectory)
                    ? null
                    : server.WorkingDirectory.Trim(),
                FileExtensions = NormalizeExtensions(server.FileExtensions)
            });
        }

        await _store.SaveAsync(mapped, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke(this, new LspSettingsChangedEventArgs(servers));
    }

    private static LspServerSettings MapServer(LspServerConfiguration source)
    {
        return new LspServerSettings
        {
            LanguageId = source.LanguageId,
            ServerPath = source.ServerPath,
            Arguments = source.Arguments,
            WorkingDirectory = source.WorkingDirectory,
            FileExtensions = source.FileExtensions
        };
    }

    private static IReadOnlyList<string> SplitAndNormalize(IReadOnlyList<string> items)
    {
        List<string> result = new();
        foreach (string item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            result.Add(item.Trim());
        }

        return result;
    }

    private static IReadOnlyList<string> NormalizeExtensions(IReadOnlyList<string> extensions)
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

            normalized.Add(value.Trim());
        }

        return normalized;
    }
}
