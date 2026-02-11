using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Core.Lsp;

namespace XamlVisualEditor.Lsp;

/// <summary>
/// LSP-backed language service that bridges editor requests to an LSP server.
/// </summary>
public sealed class LspLanguageIntellisenseService : ILanguageIntellisenseService, ILanguageDocumentSync, ILanguageDiagnosticsSource
{
    private static readonly string[] s_semanticTokenTypes =
    {
        "namespace",
        "type",
        "class",
        "enum",
        "interface",
        "struct",
        "typeParameter",
        "parameter",
        "variable",
        "property",
        "enumMember",
        "event",
        "function",
        "method",
        "macro",
        "keyword",
        "modifier",
        "comment",
        "string",
        "number",
        "regexp",
        "operator"
    };
    private static readonly string[] s_semanticTokenModifiers = Array.Empty<string>();

    private readonly ILanguageServiceRouter _router;
    private readonly IReadOnlyList<LspServerConfiguration> _servers;
    private readonly ILogger<LspLanguageIntellisenseService> _logger;
    private readonly Dictionary<string, ILanguageServiceSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _initialized = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DocumentState> _documents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _diagnosticDebounce = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _diagnosticLock = new();
    private static readonly TimeSpan DiagnosticDebounceDelay = TimeSpan.FromMilliseconds(200);
    private LanguageWorkspaceInfo? _workspaceInfo;

    public LspLanguageIntellisenseService(
        ILanguageServiceRouter router,
        ILspSettings settings,
        ILogger<LspLanguageIntellisenseService>? logger = null)
    {
        _router = router;
        _servers = settings.Servers;
        _logger = logger ?? NullLogger<LspLanguageIntellisenseService>.Instance;
    }

    public string LanguageId => "lsp";

    public event EventHandler<LanguageDiagnosticsChangedEventArgs>? DiagnosticsChanged;

    public bool CanHandle(string filePath, string? languageId)
    {
        if (_servers.Count == 0)
        {
            return false;
        }

        return GetServerForFilePath(filePath) is not null;
    }

    public Task InitializeWorkspaceAsync(string workspacePath, CancellationToken ct = default)
    {
        _workspaceInfo = BuildWorkspaceInfo(workspacePath);
        return Task.CompletedTask;
    }

    public async Task ClearWorkspaceAsync(CancellationToken ct = default)
    {
        lock (_diagnosticLock)
        {
            foreach (CancellationTokenSource token in _diagnosticDebounce.Values)
            {
                token.Cancel();
                token.Dispose();
            }

            _diagnosticDebounce.Clear();
        }

        foreach (ILanguageServiceSession session in _sessions.Values)
        {
            session.SessionFaulted -= HandleSessionFaulted;
            session.DiagnosticsPublished -= HandleDiagnosticsPublished;
            await session.ShutdownAsync(ct).ConfigureAwait(false);
        }

        _sessions.Clear();
        _initialized.Clear();
        _documents.Clear();
        _workspaceInfo = null;
    }

    public async Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken ct = default)
    {
        if (context.FilePath is null || context.DocumentText is null)
        {
            return Array.Empty<CompletionItem>();
        }

        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return Array.Empty<CompletionItem>();
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return Array.Empty<CompletionItem>();
        }

        if (!session.Capabilities.Supports(LspFeature.Completion))
        {
            return Array.Empty<CompletionItem>();
        }

        LspCompletionParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = CreateDocumentUri(context.FilePath) },
            Position = GetPosition(context.DocumentText, context.Offset)
        };

        IReadOnlyList<LspCompletionItem> items = await session.GetCompletionsAsync(parameters, ct).ConfigureAwait(false);
        List<CompletionItem> results = new(items.Count);

        foreach (LspCompletionItem item in items)
        {
            TextEdit? textEdit = null;
            if (item.TextEdit is not null)
            {
                textEdit = BuildTextEdit(item.TextEdit, context.DocumentText);
            }

            IReadOnlyList<char>? commitCharacters = null;
            if (item.CommitCharacters.Count > 0)
            {
                List<char> commits = new();
                foreach (string entry in item.CommitCharacters)
                {
                    if (!string.IsNullOrEmpty(entry))
                    {
                        commits.Add(entry[0]);
                    }
                }

                commitCharacters = commits.Count > 0 ? commits : null;
            }

            results.Add(new CompletionItem
            {
                DisplayText = item.Label,
                InsertText = item.InsertText ?? item.Label,
                Description = item.Detail,
                Documentation = item.Documentation,
                SortText = item.SortText,
                FilterText = item.FilterText,
                Kind = MapCompletionKind(item.Kind),
                TextEdit = textEdit,
                CommitCharacters = commitCharacters
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return Array.Empty<LanguageDiagnostic>();
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return Array.Empty<LanguageDiagnostic>();
        }

        Uri documentUri = CreateDocumentUri(context.FilePath);
        IReadOnlyList<LspDiagnostic> diagnostics = await session.GetDiagnosticsAsync(documentUri, ct).ConfigureAwait(false);
        return MapDiagnostics(context.FilePath, diagnostics);
    }

    public async Task<IReadOnlyList<LanguageSemanticToken>> GetSemanticTokensAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return Array.Empty<LanguageSemanticToken>();
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return Array.Empty<LanguageSemanticToken>();
        }

        if (!session.Capabilities.Supports(LspFeature.SemanticTokens))
        {
            return Array.Empty<LanguageSemanticToken>();
        }

        LspSemanticTokensLegend? legend = session.Capabilities.SemanticTokensLegend;
        if (legend is null || legend.TokenTypes.Count == 0)
        {
            return Array.Empty<LanguageSemanticToken>();
        }

        LspSemanticTokensParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = CreateDocumentUri(context.FilePath) }
        };

        LspSemanticTokens? tokens = await session.GetSemanticTokensAsync(parameters, ct).ConfigureAwait(false);
        if (tokens is null || tokens.Data.Count == 0)
        {
            return Array.Empty<LanguageSemanticToken>();
        }

        return DecodeSemanticTokens(tokens.Data, legend.TokenTypes);
    }

    public async Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return Array.Empty<TextEdit>();
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return Array.Empty<TextEdit>();
        }

        if (!session.Capabilities.Supports(LspFeature.Formatting))
        {
            return Array.Empty<TextEdit>();
        }

        LspDocumentFormattingParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = CreateDocumentUri(context.FilePath) },
            Options = new LspFormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            }
        };

        IReadOnlyList<LspTextEdit> edits = await session.GetFormattingAsync(parameters, ct).ConfigureAwait(false);
        return MapTextEdits(context.Text, edits);
    }

    public async Task<LanguageHover?> GetHoverAsync(LanguagePositionContext context, CancellationToken ct = default)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return null;
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        if (!session.Capabilities.Supports(LspFeature.Hover))
        {
            return null;
        }

        LspHoverParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = CreateDocumentUri(context.FilePath) },
            Position = GetPosition(context.Text, context.Offset)
        };

        LspHover? hover = await session.GetHoverAsync(parameters, ct).ConfigureAwait(false);
        if (hover is null)
        {
            return null;
        }

        return new LanguageHover
        {
            Contents = hover.Contents,
            Range = MapRange(hover.Range)
        };
    }

    public async Task<IReadOnlyList<LanguageLocation>> FindDefinitionsAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return Array.Empty<LanguageLocation>();
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return Array.Empty<LanguageLocation>();
        }

        if (!session.Capabilities.Supports(LspFeature.Definition))
        {
            return Array.Empty<LanguageLocation>();
        }

        LspDefinitionParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = CreateDocumentUri(context.FilePath) },
            Position = GetPosition(context.Text, context.Offset)
        };

        IReadOnlyList<LspLocation> locations = await session.GetDefinitionAsync(parameters, ct).ConfigureAwait(false);
        return MapLocations(locations);
    }

    public async Task<IReadOnlyList<LanguageLocation>> FindReferencesAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return Array.Empty<LanguageLocation>();
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return Array.Empty<LanguageLocation>();
        }

        if (!session.Capabilities.Supports(LspFeature.References))
        {
            return Array.Empty<LanguageLocation>();
        }

        LspReferenceParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = CreateDocumentUri(context.FilePath) },
            Position = GetPosition(context.Text, context.Offset),
            Context = new LspReferenceContext
            {
                IncludeDeclaration = true
            }
        };

        IReadOnlyList<LspLocation> locations = await session.GetReferencesAsync(parameters, ct).ConfigureAwait(false);
        return MapLocations(locations);
    }

    public Task<LanguageRenameInfo?> PrepareRenameAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        return PrepareRenameCoreAsync(context, ct);
    }

    public Task<LanguageWorkspaceEdit?> RenameSymbolAsync(
        LanguageRenameContext context,
        CancellationToken ct = default)
    {
        return RenameSymbolCoreAsync(context, ct);
    }

    public Task<LanguageSignatureHelp?> GetSignatureHelpAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        return GetSignatureHelpCoreAsync(context, ct);
    }

    private async Task<LanguageSignatureHelp?> GetSignatureHelpCoreAsync(
        LanguagePositionContext context,
        CancellationToken ct)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return null;
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        if (!session.Capabilities.Supports(LspFeature.SignatureHelp))
        {
            return null;
        }

        LspSignatureHelpParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = CreateDocumentUri(context.FilePath) },
            Position = GetPosition(context.Text, context.Offset)
        };

        LspSignatureHelp? help = await session.GetSignatureHelpAsync(parameters, ct).ConfigureAwait(false);
        return help is null ? null : MapSignatureHelp(help);
    }

    public async Task<IReadOnlyList<LanguageCodeAction>> GetCodeActionsAsync(
        LanguageCodeActionContext context,
        CancellationToken ct = default)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return Array.Empty<LanguageCodeAction>();
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return Array.Empty<LanguageCodeAction>();
        }

        if (!session.Capabilities.Supports(LspFeature.CodeAction))
        {
            return Array.Empty<LanguageCodeAction>();
        }

        int endOffset = Math.Clamp(context.Offset + Math.Max(0, context.Length), 0, context.Text.Length);
        LspRange range = new(GetPosition(context.Text, context.Offset), GetPosition(context.Text, endOffset));

        LspCodeActionParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = CreateDocumentUri(context.FilePath) },
            Range = range,
            Context = new LspCodeActionContext()
        };

        IReadOnlyList<LspCodeAction> actions = await session.GetCodeActionsAsync(parameters, ct).ConfigureAwait(false);
        return MapCodeActions(actions, context.FilePath, context.Text);
    }

    public async Task<IReadOnlyList<LanguageSymbol>> GetDocumentSymbolsAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return Array.Empty<LanguageSymbol>();
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return Array.Empty<LanguageSymbol>();
        }

        if (!session.Capabilities.Supports(LspFeature.DocumentSymbols))
        {
            return Array.Empty<LanguageSymbol>();
        }

        LspDocumentSymbolParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = CreateDocumentUri(context.FilePath) }
        };

        IReadOnlyList<LspDocumentSymbol> symbols = await session.GetDocumentSymbolsAsync(parameters, ct)
            .ConfigureAwait(false);
        return MapDocumentSymbols(symbols, context.FilePath);
    }

    public async Task<IReadOnlyList<LanguageSymbol>> GetWorkspaceSymbolsAsync(
        LanguageSymbolQuery query,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.Query))
        {
            return Array.Empty<LanguageSymbol>();
        }

        LspServerConfiguration? server = _servers.FirstOrDefault();
        if (server is null)
        {
            return Array.Empty<LanguageSymbol>();
        }

        string workspacePath = _workspaceInfo?.RootPath
            ?? server.WorkingDirectory
            ?? Environment.CurrentDirectory;

        ILanguageServiceSession? session = await EnsureSessionAsync(server, workspacePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return Array.Empty<LanguageSymbol>();
        }

        if (!session.Capabilities.Supports(LspFeature.WorkspaceSymbols))
        {
            return Array.Empty<LanguageSymbol>();
        }

        LspWorkspaceSymbolParams parameters = new()
        {
            Query = query.Query
        };

        IReadOnlyList<LspSymbolInformation> symbols = await session.GetWorkspaceSymbolsAsync(parameters, ct)
            .ConfigureAwait(false);
        return MapWorkspaceSymbols(symbols);
    }

    public async Task DocumentOpenedAsync(LanguageDocumentContext context, CancellationToken ct = default)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return;
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return;
        }

        DocumentState state = GetOrCreateDocumentState(context.FilePath, server.LanguageId);
        if (state.IsOpen)
        {
            await DocumentChangedAsync(context, ct).ConfigureAwait(false);
            return;
        }

        state.Version++;
        state.IsOpen = true;

        await session.PublishDocumentAsync(new LspTextDocumentItem
        {
            Uri = CreateDocumentUri(context.FilePath),
            LanguageId = server.LanguageId,
            Version = state.Version,
            Text = context.Text
        }, ct).ConfigureAwait(false);
    }

    public async Task DocumentChangedAsync(LanguageDocumentContext context, CancellationToken ct = default)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return;
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return;
        }

        DocumentState state = GetOrCreateDocumentState(context.FilePath, server.LanguageId);
        if (!state.IsOpen)
        {
            await DocumentOpenedAsync(context, ct).ConfigureAwait(false);
            return;
        }

        state.Version++;
        await session.ApplyDocumentChangesAsync(new LspVersionedTextDocumentIdentifier
        {
            Uri = CreateDocumentUri(context.FilePath),
            Version = state.Version
        }, new[]
        {
            new LspTextDocumentContentChangeEvent
            {
                Text = context.Text
            }
        }, ct).ConfigureAwait(false);
    }

    private DocumentState GetOrCreateDocumentState(string filePath, string languageId)
    {
        if (_documents.TryGetValue(filePath, out DocumentState? state))
        {
            return state;
        }

        state = new DocumentState(languageId);
        _documents[filePath] = state;
        return state;
    }

    private async Task<ILanguageServiceSession?> EnsureSessionAsync(
        LspServerConfiguration server,
        string filePath,
        CancellationToken ct)
    {
        if (_sessions.TryGetValue(server.LanguageId, out ILanguageServiceSession? session))
        {
            if (session.IsAlive)
            {
                return session;
            }

            await RemoveSessionAsync(server.LanguageId, session, ct).ConfigureAwait(false);
        }

        LanguageWorkspaceInfo workspace = _workspaceInfo ?? BuildWorkspaceInfo(Path.GetDirectoryName(filePath) ?? filePath);
        session = await _router.GetSessionAsync(server.LanguageId, workspace, ct).ConfigureAwait(false);
        if (session is null)
        {
            _logger.LogWarning("No LSP session available for {LanguageId}.", server.LanguageId);
            return null;
        }

        _sessions[server.LanguageId] = session;
        session.DiagnosticsPublished += HandleDiagnosticsPublished;
        session.SessionFaulted += HandleSessionFaulted;

        if (!_initialized.ContainsKey(server.LanguageId))
        {
            using CancellationTokenSource initCts = new(TimeSpan.FromSeconds(10));
            await session.InitializeAsync(new LspInitializeParams
            {
                ProcessId = Environment.ProcessId,
                RootUri = new Uri(Path.GetFullPath(workspace.RootPath)).AbsoluteUri,
                ClientInfo = new LspClientInfo { Name = "XamlVisualEditor" },
                Capabilities = new
                {
                    textDocument = new
                    {
                        completion = new
                        {
                            completionItem = new
                            {
                                commitCharactersSupport = true
                            }
                        },
                        hover = new
                        {
                            contentFormat = new[] { "markdown", "plaintext" }
                        },
                        signatureHelp = new
                        {
                            contextSupport = false,
                            signatureInformation = new
                            {
                                documentationFormat = new[] { "markdown", "plaintext" }
                            }
                        },
                        semanticTokens = new
                        {
                            requests = new
                            {
                                full = true
                            },
                            tokenTypes = s_semanticTokenTypes,
                            tokenModifiers = s_semanticTokenModifiers,
                            formats = new[] { "relative" }
                        },
                        documentSymbol = new { }
                    },
                    workspace = new
                    {
                        symbol = new { }
                    }
                }
            }, initCts.Token).ConfigureAwait(false);

            _initialized[server.LanguageId] = true;
        }

        return session;
    }

    private void HandleDiagnosticsPublished(object? sender, LspPublishDiagnosticsParams e)
    {
        string filePath = e.Uri.LocalPath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        CancellationTokenSource token;
        lock (_diagnosticLock)
        {
            if (_diagnosticDebounce.TryGetValue(filePath, out CancellationTokenSource? existing))
            {
                existing.Cancel();
                existing.Dispose();
            }

            token = new CancellationTokenSource();
            _diagnosticDebounce[filePath] = token;
        }

        _ = DebounceDiagnosticsAsync(filePath, token.Token);
    }

    private void HandleSessionFaulted(object? sender, LanguageServiceSessionFaultedEventArgs e)
    {
        if (sender is not ILanguageServiceSession session)
        {
            return;
        }

        _ = HandleSessionFaultedAsync(session, e);
    }

    private async Task HandleSessionFaultedAsync(ILanguageServiceSession session, LanguageServiceSessionFaultedEventArgs e)
    {
        try
        {
            _logger.LogWarning(e.Error, "LSP session faulted for {LanguageId}.", e.LanguageId);
            if (_sessions.TryGetValue(e.LanguageId, out ILanguageServiceSession? existing)
                && ReferenceEquals(existing, session))
            {
                await RemoveSessionAsync(e.LanguageId, session, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to handle LSP session fault for {LanguageId}.", e.LanguageId);
        }
    }

    private async Task RemoveSessionAsync(string languageId, ILanguageServiceSession session, CancellationToken ct)
    {
        session.DiagnosticsPublished -= HandleDiagnosticsPublished;
        session.SessionFaulted -= HandleSessionFaulted;
        _sessions.Remove(languageId);
        _initialized.Remove(languageId);
        ResetDocumentStates(languageId);

        try
        {
            await session.ShutdownAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to shutdown LSP session for {LanguageId}.", languageId);
        }
    }

    private void ResetDocumentStates(string languageId)
    {
        foreach (DocumentState state in _documents.Values)
        {
            if (string.Equals(state.LanguageId, languageId, StringComparison.OrdinalIgnoreCase))
            {
                state.IsOpen = false;
                state.Version = 0;
            }
        }
    }

    private async Task DebounceDiagnosticsAsync(string filePath, CancellationToken ct)
    {
        try
        {
            await Task.Delay(DiagnosticDebounceDelay, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        DiagnosticsChanged?.Invoke(this, new LanguageDiagnosticsChangedEventArgs
        {
            FilePath = filePath
        });
    }

    private LspServerConfiguration? GetServerForFilePath(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        foreach (LspServerConfiguration server in _servers)
        {
            if (server.FileExtensions.Any(ext => string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase)))
            {
                return server;
            }
        }

        return null;
    }

    private static Uri CreateDocumentUri(string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        return new Uri(fullPath);
    }

    private static LanguageWorkspaceInfo BuildWorkspaceInfo(string workspacePath)
    {
        string fullPath = Path.GetFullPath(workspacePath);
        WorkspaceKind kind = WorkspaceKind.Folder;
        string? solutionPath = null;
        string? projectPath = null;

        if (File.Exists(fullPath))
        {
            string ext = Path.GetExtension(fullPath);
            if (string.Equals(ext, ".sln", StringComparison.OrdinalIgnoreCase))
            {
                kind = WorkspaceKind.Solution;
                solutionPath = fullPath;
                fullPath = Path.GetDirectoryName(fullPath) ?? fullPath;
            }
            else if (string.Equals(ext, ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                kind = WorkspaceKind.Project;
                projectPath = fullPath;
                fullPath = Path.GetDirectoryName(fullPath) ?? fullPath;
            }
            else
            {
                kind = WorkspaceKind.File;
            }
        }

        return new LanguageWorkspaceInfo
        {
            RootPath = fullPath,
            SolutionPath = solutionPath,
            ProjectPath = projectPath,
            Kind = kind
        };
    }

    private static LspPosition GetPosition(string text, int offset)
    {
        int clamped = Math.Clamp(offset, 0, text.Length);
        int line = 0;
        int character = 0;

        for (int i = 0; i < clamped; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                character = 0;
            }
            else
            {
                character++;
            }
        }

        return new LspPosition(line, character);
    }

    private static IReadOnlyList<LanguageDiagnostic> MapDiagnostics(
        string filePath,
        IReadOnlyList<LspDiagnostic> diagnostics)
    {
        List<LanguageDiagnostic> results = new(diagnostics.Count);
        foreach (LspDiagnostic diagnostic in diagnostics)
        {
            results.Add(new LanguageDiagnostic
            {
                FilePath = filePath,
                Message = diagnostic.Message,
                Severity = MapSeverity(diagnostic.Severity),
                Range = new LanguageTextRange(
                    new LanguageTextPosition(diagnostic.Range.Start.Line + 1, diagnostic.Range.Start.Character + 1),
                    new LanguageTextPosition(diagnostic.Range.End.Line + 1, diagnostic.Range.End.Character + 1)),
                Code = diagnostic.Code,
                Source = diagnostic.Source
            });
        }

        return results;
    }

    private static DiagnosticSeverity MapSeverity(LspDiagnosticSeverity? severity)
    {
        return severity switch
        {
            LspDiagnosticSeverity.Error => DiagnosticSeverity.Error,
            LspDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
            LspDiagnosticSeverity.Information => DiagnosticSeverity.Info,
            LspDiagnosticSeverity.Hint => DiagnosticSeverity.Info,
            _ => DiagnosticSeverity.Info
        };
    }

    private static CompletionItemKind MapCompletionKind(LspCompletionItemKind? kind)
    {
        return kind switch
        {
            LspCompletionItemKind.Method => CompletionItemKind.Method,
            LspCompletionItemKind.Function => CompletionItemKind.Method,
            LspCompletionItemKind.Constructor => CompletionItemKind.Method,
            LspCompletionItemKind.Field => CompletionItemKind.Field,
            LspCompletionItemKind.Variable => CompletionItemKind.Variable,
            LspCompletionItemKind.Class => CompletionItemKind.Class,
            LspCompletionItemKind.Interface => CompletionItemKind.Interface,
            LspCompletionItemKind.Module => CompletionItemKind.NamespaceSymbol,
            LspCompletionItemKind.Property => CompletionItemKind.PropertySymbol,
            LspCompletionItemKind.Enum => CompletionItemKind.Enum,
            LspCompletionItemKind.EnumMember => CompletionItemKind.Value,
            LspCompletionItemKind.Keyword => CompletionItemKind.Keyword,
            LspCompletionItemKind.Snippet => CompletionItemKind.Snippet,
            LspCompletionItemKind.Struct => CompletionItemKind.Struct,
            LspCompletionItemKind.Event => CompletionItemKind.Event,
            _ => CompletionItemKind.Value
        };
    }

    private static TextEdit? BuildTextEdit(LspTextEdit edit, string text)
    {
        int start = GetOffsetFromPosition(text, edit.Range.Start);
        int end = GetOffsetFromPosition(text, edit.Range.End);
        if (start < 0 || end < start)
        {
            return null;
        }

        return new TextEdit
        {
            Offset = start,
            Length = end - start,
            NewText = edit.NewText
        };
    }

    private static int GetOffsetFromPosition(string text, LspPosition position)
    {
        int targetLine = Math.Max(0, position.Line);
        int targetChar = Math.Max(0, position.Character);
        int line = 0;
        int index = 0;

        while (index < text.Length && line < targetLine)
        {
            char current = text[index++];
            if (current == '\n')
            {
                line++;
            }
        }

        if (line < targetLine)
        {
            return text.Length;
        }

        int offset = index + targetChar;
        return Math.Clamp(offset, 0, text.Length);
    }

    private async Task<LanguageRenameInfo?> PrepareRenameCoreAsync(
        LanguagePositionContext context,
        CancellationToken ct)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return null;
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        if (!session.Capabilities.Supports(LspFeature.Rename))
        {
            return null;
        }

        LspPrepareRenameParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = CreateDocumentUri(context.FilePath) },
            Position = GetPosition(context.Text, context.Offset)
        };

        LspRange? range = await session.PrepareRenameAsync(parameters, ct).ConfigureAwait(false);
        if (range is null)
        {
            return null;
        }

        LanguageTextRange mapped = new(
            new LanguageTextPosition(range.Value.Start.Line + 1, range.Value.Start.Character + 1),
            new LanguageTextPosition(range.Value.End.Line + 1, range.Value.End.Character + 1));

        string name = ExtractTextRange(context.Text, mapped);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new LanguageRenameInfo
        {
            Name = name,
            Range = mapped
        };
    }

    private async Task<LanguageWorkspaceEdit?> RenameSymbolCoreAsync(
        LanguageRenameContext context,
        CancellationToken ct)
    {
        LspServerConfiguration? server = GetServerForFilePath(context.FilePath);
        if (server is null)
        {
            return null;
        }

        ILanguageServiceSession? session = await EnsureSessionAsync(server, context.FilePath, ct).ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        if (!session.Capabilities.Supports(LspFeature.Rename))
        {
            return null;
        }

        LspRenameParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = CreateDocumentUri(context.FilePath) },
            Position = GetPosition(context.Text, context.Offset),
            NewName = context.NewName
        };

        LspWorkspaceEdit? edit = await session.RenameAsync(parameters, ct).ConfigureAwait(false);
        if (edit is null)
        {
            return null;
        }

        return MapWorkspaceEdit(edit, context.FilePath, context.Text);
    }

    private static LanguageTextRange? MapRange(LspRange? range)
    {
        if (range is null)
        {
            return null;
        }

        return new LanguageTextRange(
            new LanguageTextPosition(range.Value.Start.Line + 1, range.Value.Start.Character + 1),
            new LanguageTextPosition(range.Value.End.Line + 1, range.Value.End.Character + 1));
    }

    private static LanguageSignatureHelp MapSignatureHelp(LspSignatureHelp help)
    {
        List<LanguageSignature> signatures = new(help.Signatures.Count);
        foreach (LspSignatureInformation signature in help.Signatures)
        {
            List<LanguageParameter> parameters = new(signature.Parameters.Count);
            foreach (LspParameterInformation parameter in signature.Parameters)
            {
                parameters.Add(new LanguageParameter
                {
                    Label = parameter.Label,
                    Documentation = parameter.Documentation
                });
            }

            signatures.Add(new LanguageSignature
            {
                Label = signature.Label,
                Documentation = signature.Documentation,
                Parameters = parameters
            });
        }

        return new LanguageSignatureHelp
        {
            Signatures = signatures,
            ActiveSignature = help.ActiveSignature,
            ActiveParameter = help.ActiveParameter
        };
    }

    private static IReadOnlyList<LanguageSemanticToken> DecodeSemanticTokens(
        IReadOnlyList<int> data,
        IReadOnlyList<string> tokenTypes)
    {
        if (data.Count < 5 || tokenTypes.Count == 0)
        {
            return Array.Empty<LanguageSemanticToken>();
        }

        List<LanguageSemanticToken> results = new(data.Count / 5);
        int line = 0;
        int character = 0;

        for (int i = 0; i + 4 < data.Count; i += 5)
        {
            int deltaLine = data[i];
            int deltaStart = data[i + 1];
            int length = data[i + 2];
            int tokenTypeIndex = data[i + 3];

            if (deltaLine == 0)
            {
                character += deltaStart;
            }
            else
            {
                line += deltaLine;
                character = deltaStart;
            }

            if (length <= 0)
            {
                continue;
            }

            string type = tokenTypeIndex >= 0 && tokenTypeIndex < tokenTypes.Count
                ? tokenTypes[tokenTypeIndex]
                : "unknown";

            LanguageTextRange range = new(
                new LanguageTextPosition(line + 1, character + 1),
                new LanguageTextPosition(line + 1, character + 1 + length));

            results.Add(new LanguageSemanticToken
            {
                Range = range,
                Type = type
            });
        }

        return results;
    }

    private static IReadOnlyList<TextEdit> MapTextEdits(string text, IReadOnlyList<LspTextEdit> edits)
    {
        if (edits.Count == 0)
        {
            return Array.Empty<TextEdit>();
        }

        List<TextEdit> results = new(edits.Count);
        foreach (LspTextEdit edit in edits)
        {
            TextEdit? mapped = BuildTextEdit(edit, text);
            if (mapped is not null)
            {
                results.Add(mapped);
            }
        }

        return results;
    }

    private static LanguageWorkspaceEdit? MapWorkspaceEdit(
        LspWorkspaceEdit edit,
        string currentFilePath,
        string currentText)
    {
        List<LanguageDocumentEdit> documentEdits = new();
        foreach (LspTextDocumentEdit change in edit.DocumentChanges)
        {
            string? filePath = change.TextDocument.Uri.LocalPath;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                continue;
            }

            string? text = string.Equals(filePath, currentFilePath, StringComparison.OrdinalIgnoreCase)
                ? currentText
                : File.Exists(filePath) ? File.ReadAllText(filePath) : null;
            if (text is null)
            {
                continue;
            }

            IReadOnlyList<TextEdit> mappedEdits = MapTextEdits(text, change.Edits);
            if (mappedEdits.Count == 0)
            {
                continue;
            }

            documentEdits.Add(new LanguageDocumentEdit
            {
                FilePath = filePath,
                Edits = mappedEdits
            });
        }

        if (documentEdits.Count == 0)
        {
            return null;
        }

        return new LanguageWorkspaceEdit
        {
            DocumentEdits = documentEdits
        };
    }

    private static IReadOnlyList<LanguageCodeAction> MapCodeActions(
        IReadOnlyList<LspCodeAction> actions,
        string filePath,
        string text)
    {
        if (actions.Count == 0)
        {
            return Array.Empty<LanguageCodeAction>();
        }

        List<LanguageCodeAction> results = new();
        foreach (LspCodeAction action in actions)
        {
            if (action.Edit is null)
            {
                continue;
            }

            LanguageWorkspaceEdit? edit = MapWorkspaceEdit(action.Edit, filePath, text);
            if (edit is null)
            {
                continue;
            }

            results.Add(new LanguageCodeAction
            {
                Title = action.Title,
                Kind = action.Kind,
                IsPreferred = false,
                Edit = edit
            });
        }

        return results;
    }

    private static IReadOnlyList<LanguageSymbol> MapDocumentSymbols(
        IReadOnlyList<LspDocumentSymbol> symbols,
        string filePath)
    {
        List<LanguageSymbol> results = new();
        foreach (LspDocumentSymbol symbol in symbols)
        {
            AddDocumentSymbol(results, symbol, filePath);
        }

        return results;
    }

    private static void AddDocumentSymbol(
        List<LanguageSymbol> results,
        LspDocumentSymbol symbol,
        string filePath)
    {
        results.Add(new LanguageSymbol
        {
            Name = symbol.Name,
            Kind = MapSymbolKind(symbol.Kind),
            FilePath = filePath,
            Range = new LanguageTextRange(
                new LanguageTextPosition(symbol.SelectionRange.Start.Line + 1, symbol.SelectionRange.Start.Character + 1),
                new LanguageTextPosition(symbol.SelectionRange.End.Line + 1, symbol.SelectionRange.End.Character + 1))
        });

        foreach (LspDocumentSymbol child in symbol.Children)
        {
            AddDocumentSymbol(results, child, filePath);
        }
    }

    private static IReadOnlyList<LanguageSymbol> MapWorkspaceSymbols(IReadOnlyList<LspSymbolInformation> symbols)
    {
        if (symbols.Count == 0)
        {
            return Array.Empty<LanguageSymbol>();
        }

        List<LanguageSymbol> results = new(symbols.Count);
        foreach (LspSymbolInformation symbol in symbols)
        {
            results.Add(new LanguageSymbol
            {
                Name = symbol.Name,
                Kind = MapSymbolKind(symbol.Kind),
                FilePath = symbol.Location.Uri.LocalPath,
                Range = new LanguageTextRange(
                    new LanguageTextPosition(symbol.Location.Range.Start.Line + 1, symbol.Location.Range.Start.Character + 1),
                    new LanguageTextPosition(symbol.Location.Range.End.Line + 1, symbol.Location.Range.End.Character + 1))
            });
        }

        return results;
    }

    private static LanguageSymbolKind MapSymbolKind(LspSymbolKind kind)
    {
        return kind switch
        {
            LspSymbolKind.File => LanguageSymbolKind.File,
            LspSymbolKind.Module => LanguageSymbolKind.Module,
            LspSymbolKind.Namespace => LanguageSymbolKind.Namespace,
            LspSymbolKind.Package => LanguageSymbolKind.Package,
            LspSymbolKind.Class => LanguageSymbolKind.Class,
            LspSymbolKind.Method => LanguageSymbolKind.Method,
            LspSymbolKind.Property => LanguageSymbolKind.Property,
            LspSymbolKind.Field => LanguageSymbolKind.Field,
            LspSymbolKind.Constructor => LanguageSymbolKind.Constructor,
            LspSymbolKind.Enum => LanguageSymbolKind.Enum,
            LspSymbolKind.Interface => LanguageSymbolKind.Interface,
            LspSymbolKind.Function => LanguageSymbolKind.Function,
            LspSymbolKind.Variable => LanguageSymbolKind.Variable,
            LspSymbolKind.Constant => LanguageSymbolKind.Constant,
            LspSymbolKind.String => LanguageSymbolKind.String,
            LspSymbolKind.Number => LanguageSymbolKind.Number,
            LspSymbolKind.Boolean => LanguageSymbolKind.Boolean,
            LspSymbolKind.Array => LanguageSymbolKind.Array,
            LspSymbolKind.Object => LanguageSymbolKind.Object,
            LspSymbolKind.Key => LanguageSymbolKind.Key,
            LspSymbolKind.Null => LanguageSymbolKind.Null,
            LspSymbolKind.EnumMember => LanguageSymbolKind.EnumMember,
            LspSymbolKind.Struct => LanguageSymbolKind.Struct,
            LspSymbolKind.Event => LanguageSymbolKind.Event,
            LspSymbolKind.Operator => LanguageSymbolKind.Operator,
            LspSymbolKind.TypeParameter => LanguageSymbolKind.TypeParameter,
            _ => LanguageSymbolKind.Object
        };
    }

    private static string ExtractTextRange(string text, LanguageTextRange range)
    {
        int start = GetOffsetForPosition(text, range.Start);
        int end = GetOffsetForPosition(text, range.End);
        if (end <= start)
        {
            return string.Empty;
        }

        return text.Substring(start, Math.Min(text.Length, end) - start).Trim();
    }

    private static int GetOffsetForPosition(string text, LanguageTextPosition position)
    {
        int lineTarget = Math.Max(1, position.Line);
        int colTarget = Math.Max(1, position.Column);
        int line = 1;
        int index = 0;

        while (index < text.Length && line < lineTarget)
        {
            if (text[index++] == '\n')
            {
                line++;
            }
        }

        int offset = index + colTarget - 1;
        return Math.Clamp(offset, 0, text.Length);
    }

    private static IReadOnlyList<LanguageLocation> MapLocations(IReadOnlyList<LspLocation> locations)
    {
        List<LanguageLocation> results = new(locations.Count);
        foreach (LspLocation location in locations)
        {
            results.Add(new LanguageLocation
            {
                FilePath = location.Uri.LocalPath,
                Range = new LanguageTextRange(
                    new LanguageTextPosition(location.Range.Start.Line + 1, location.Range.Start.Character + 1),
                    new LanguageTextPosition(location.Range.End.Line + 1, location.Range.End.Character + 1))
            });
        }

        return results;
    }

    private sealed class DocumentState
    {
        public DocumentState(string languageId)
        {
            LanguageId = languageId;
        }

        public string LanguageId { get; }

        public int Version { get; set; }

        public bool IsOpen { get; set; }
    }
}
