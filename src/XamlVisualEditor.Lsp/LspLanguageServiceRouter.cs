using Microsoft.Extensions.Logging;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;

namespace XamlVisualEditor.Lsp;

/// <summary>
/// Resolves LSP sessions based on language identifiers.
/// </summary>
public sealed class LspLanguageServiceRouter : ILanguageServiceRouter
{
    private readonly IReadOnlyDictionary<string, LspServerConfiguration> _servers;
    private readonly Func<LspServerConfiguration, LanguageWorkspaceInfo, ILanguageServiceSession> _sessionFactory;

    public LspLanguageServiceRouter(
        IEnumerable<LspServerConfiguration> servers,
        ILoggerFactory? loggerFactory = null,
        Func<LspServerConfiguration, LanguageWorkspaceInfo, ILanguageServiceSession>? sessionFactory = null)
    {
        Dictionary<string, LspServerConfiguration> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (LspServerConfiguration server in servers)
        {
            if (!map.ContainsKey(server.LanguageId))
            {
                map[server.LanguageId] = server;
            }
        }

        _servers = map;
        _sessionFactory = sessionFactory ?? ((config, _) => new LspClientSession(config, loggerFactory));
    }

    /// <summary>Gets the registered servers.</summary>
    public IReadOnlyList<LspServerConfiguration> Servers => _servers.Values.ToList();

    /// <inheritdoc />
    public ValueTask<ILanguageServiceSession?> GetSessionAsync(
        string languageId,
        LanguageWorkspaceInfo workspace,
        CancellationToken ct = default)
    {
        if (!_servers.TryGetValue(languageId, out LspServerConfiguration? config))
        {
            return new ValueTask<ILanguageServiceSession?>((ILanguageServiceSession?)null);
        }

        ILanguageServiceSession session = _sessionFactory(config, workspace);
        return new ValueTask<ILanguageServiceSession?>(session);
    }
}
