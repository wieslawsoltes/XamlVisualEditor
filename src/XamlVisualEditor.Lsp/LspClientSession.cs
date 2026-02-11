using System.Collections.Concurrent;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Lsp;
using XamlVisualEditor.Core.Interfaces;

namespace XamlVisualEditor.Lsp;

internal sealed class LspClientSession : ILanguageServiceSession
{
    private readonly LspServerConfiguration _configuration;
    private readonly ILspTransport _transport;
    private readonly LspJsonRpcClient _client;
    private readonly ILogger<LspClientSession> _logger;
    private readonly ConcurrentDictionary<Uri, IReadOnlyList<LspDiagnostic>> _diagnostics = new();
    private bool _initialized;
    private bool _disposed;
    private bool _isAlive;

    public LspClientSession(LspServerConfiguration configuration, ILoggerFactory? loggerFactory = null)
        : this(configuration, new ProcessLspTransport(configuration, loggerFactory), loggerFactory)
    {
    }

    internal LspClientSession(
        LspServerConfiguration configuration,
        ILspTransport transport,
        ILoggerFactory? loggerFactory = null)
    {
        _configuration = configuration;
        _transport = transport;
        _client = new LspJsonRpcClient(_transport.Input, _transport.Output);
        _client.NotificationReceived += OnNotificationReceived;
        _client.Disconnected += OnClientDisconnected;
        _logger = loggerFactory?.CreateLogger<LspClientSession>() ?? NullLogger<LspClientSession>.Instance;
    }

    public string LanguageId => _configuration.LanguageId;

    public LspServerCapabilities Capabilities { get; private set; } = new();

    public bool IsAlive => _isAlive && !_disposed;

    public event EventHandler<LspPublishDiagnosticsParams>? DiagnosticsPublished;
    public event EventHandler<LanguageServiceSessionFaultedEventArgs>? SessionFaulted;

    public async ValueTask InitializeAsync(LspInitializeParams options, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        _client.Start();
        JsonElement? result = await SendRequestSafeAsync("initialize", options, ct).ConfigureAwait(false);
        Capabilities = ParseCapabilities(result);
        await _client.SendNotificationAsync("initialized", new { }, ct).ConfigureAwait(false);
        _initialized = true;
        _isAlive = true;
    }

    public async ValueTask ShutdownAsync(CancellationToken ct = default)
    {
        if (_disposed)
        {
            return;
        }

        if (_initialized)
        {
            await SendRequestSafeAsync("shutdown", new { }, ct).ConfigureAwait(false);
            await _client.SendNotificationAsync("exit", new { }, ct).ConfigureAwait(false);
        }

        await _client.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
        _isAlive = false;
    }

    public ValueTask PublishDocumentAsync(LspTextDocumentItem document, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return new ValueTask(_client.SendNotificationAsync("textDocument/didOpen", new { textDocument = document }, ct));
    }

    public ValueTask ApplyDocumentChangesAsync(
        LspVersionedTextDocumentIdentifier documentId,
        IReadOnlyList<LspTextDocumentContentChangeEvent> changes,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        object payload = new
        {
            textDocument = documentId,
            contentChanges = changes
        };

        return new ValueTask(_client.SendNotificationAsync("textDocument/didChange", payload, ct));
    }

    public ValueTask<IReadOnlyList<LspDiagnostic>> GetDiagnosticsAsync(Uri documentUri, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (_diagnostics.TryGetValue(documentUri, out IReadOnlyList<LspDiagnostic>? diagnostics))
        {
            return new ValueTask<IReadOnlyList<LspDiagnostic>>(diagnostics);
        }

        return new ValueTask<IReadOnlyList<LspDiagnostic>>(Array.Empty<LspDiagnostic>());
    }

    public async ValueTask<IReadOnlyList<LspCompletionItem>> GetCompletionsAsync(
        LspCompletionParams parameters,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        JsonElement? result = await SendRequestSafeAsync("textDocument/completion", parameters, ct)
            .ConfigureAwait(false);

        return ParseCompletionResult(result);
    }

    public async ValueTask<LspHover?> GetHoverAsync(LspHoverParams parameters, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        JsonElement? result = await SendRequestSafeAsync("textDocument/hover", parameters, ct)
            .ConfigureAwait(false);

        return Deserialize<LspHover>(result);
    }

    public async ValueTask<LspSignatureHelp?> GetSignatureHelpAsync(
        LspSignatureHelpParams parameters,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        JsonElement? result = await SendRequestSafeAsync("textDocument/signatureHelp", parameters, ct)
            .ConfigureAwait(false);

        return Deserialize<LspSignatureHelp>(result);
    }

    public async ValueTask<IReadOnlyList<LspLocation>> GetDefinitionAsync(
        LspDefinitionParams parameters,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        JsonElement? result = await SendRequestSafeAsync("textDocument/definition", parameters, ct)
            .ConfigureAwait(false);

        return ParseLocationResult(result);
    }

    public async ValueTask<IReadOnlyList<LspLocation>> GetReferencesAsync(
        LspReferenceParams parameters,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        JsonElement? result = await SendRequestSafeAsync("textDocument/references", parameters, ct)
            .ConfigureAwait(false);

        return ParseLocationResult(result);
    }

    public async ValueTask<LspRange?> PrepareRenameAsync(
        LspPrepareRenameParams parameters,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        JsonElement? result = await SendRequestSafeAsync("textDocument/prepareRename", parameters, ct)
            .ConfigureAwait(false);

        return Deserialize<LspRange>(result);
    }

    public async ValueTask<LspWorkspaceEdit?> RenameAsync(
        LspRenameParams parameters,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        JsonElement? result = await SendRequestSafeAsync("textDocument/rename", parameters, ct)
            .ConfigureAwait(false);

        return Deserialize<LspWorkspaceEdit>(result);
    }

    public async ValueTask<IReadOnlyList<LspTextEdit>> GetFormattingAsync(
        LspDocumentFormattingParams parameters,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        JsonElement? result = await SendRequestSafeAsync("textDocument/formatting", parameters, ct)
            .ConfigureAwait(false);

        return Deserialize<IReadOnlyList<LspTextEdit>>(result) ?? Array.Empty<LspTextEdit>();
    }

    public async ValueTask<IReadOnlyList<LspCodeAction>> GetCodeActionsAsync(
        LspCodeActionParams parameters,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        JsonElement? result = await SendRequestSafeAsync("textDocument/codeAction", parameters, ct)
            .ConfigureAwait(false);

        return Deserialize<IReadOnlyList<LspCodeAction>>(result) ?? Array.Empty<LspCodeAction>();
    }

    public async ValueTask<IReadOnlyList<LspDocumentSymbol>> GetDocumentSymbolsAsync(
        LspDocumentSymbolParams parameters,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        JsonElement? result = await SendRequestSafeAsync("textDocument/documentSymbol", parameters, ct)
            .ConfigureAwait(false);

        return ParseDocumentSymbolResult(result);
    }

    public async ValueTask<IReadOnlyList<LspSymbolInformation>> GetWorkspaceSymbolsAsync(
        LspWorkspaceSymbolParams parameters,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        JsonElement? result = await SendRequestSafeAsync("workspace/symbol", parameters, ct)
            .ConfigureAwait(false);

        return Deserialize<IReadOnlyList<LspSymbolInformation>>(result) ?? Array.Empty<LspSymbolInformation>();
    }

    public async ValueTask<LspSemanticTokens?> GetSemanticTokensAsync(
        LspSemanticTokensParams parameters,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        JsonElement? result = await SendRequestSafeAsync("textDocument/semanticTokens/full", parameters, ct)
            .ConfigureAwait(false);

        return Deserialize<LspSemanticTokens>(result);
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
    }

    private void OnClientDisconnected(Exception ex)
    {
        if (_disposed)
        {
            return;
        }

        _isAlive = false;
        _logger.LogWarning(ex, "LSP session disconnected ({LanguageId}).", LanguageId);
        SessionFaulted?.Invoke(this, new LanguageServiceSessionFaultedEventArgs(LanguageId, ex));
    }

    private void OnNotificationReceived(string method, JsonElement payload)
    {
        try
        {
            if (!string.Equals(method, "textDocument/publishDiagnostics", StringComparison.Ordinal))
            {
                return;
            }

            LspPublishDiagnosticsParams? parameters = Deserialize<LspPublishDiagnosticsParams>(payload);
            if (parameters is null)
            {
                return;
            }

            _diagnostics[parameters.Uri] = parameters.Diagnostics;
            DiagnosticsPublished?.Invoke(this, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to handle LSP notification {Method} ({LanguageId}).", method, LanguageId);
        }
    }

    private async Task<JsonElement?> SendRequestSafeAsync(string method, object? parameters, CancellationToken ct)
    {
        try
        {
            return await _client.SendRequestAsync(method, parameters, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (LspJsonRpcException ex)
        {
            _logger.LogError(ex, "LSP JSON-RPC error for {Method} ({LanguageId}).", method, LanguageId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LSP request failed for {Method} ({LanguageId}).", method, LanguageId);
            return null;
        }
    }

    private static IReadOnlyList<LspCompletionItem> ParseCompletionResult(JsonElement? result)
    {
        if (result is null)
        {
            return Array.Empty<LspCompletionItem>();
        }

        JsonElement element = result.Value;
        if (element.ValueKind == JsonValueKind.Array)
        {
            return Deserialize<IReadOnlyList<LspCompletionItem>>(element) ?? Array.Empty<LspCompletionItem>();
        }

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("items", out JsonElement items))
        {
            return Deserialize<IReadOnlyList<LspCompletionItem>>(items) ?? Array.Empty<LspCompletionItem>();
        }

        return Array.Empty<LspCompletionItem>();
    }

    private static IReadOnlyList<LspLocation> ParseLocationResult(JsonElement? result)
    {
        if (result is null)
        {
            return Array.Empty<LspLocation>();
        }

        JsonElement element = result.Value;
        if (element.ValueKind == JsonValueKind.Array)
        {
            return Deserialize<IReadOnlyList<LspLocation>>(element) ?? Array.Empty<LspLocation>();
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            LspLocation? location = Deserialize<LspLocation>(element);
            if (location is not null)
            {
                return new[] { location };
            }
        }

        return Array.Empty<LspLocation>();
    }

    private static LspServerCapabilities ParseCapabilities(JsonElement? result)
    {
        if (result is null || result.Value.ValueKind != JsonValueKind.Object)
        {
            return new LspServerCapabilities();
        }

        if (!result.Value.TryGetProperty("capabilities", out JsonElement capsElement)
            || capsElement.ValueKind != JsonValueKind.Object)
        {
            return new LspServerCapabilities();
        }

        return new LspServerCapabilities
        {
            CompletionProvider = ReadCapability(capsElement, "completionProvider"),
            HoverProvider = ReadCapability(capsElement, "hoverProvider"),
            SignatureHelpProvider = ReadCapability(capsElement, "signatureHelpProvider"),
            DefinitionProvider = ReadCapability(capsElement, "definitionProvider"),
            DocumentFormattingProvider = ReadCapability(capsElement, "documentFormattingProvider"),
            CodeActionProvider = ReadCapability(capsElement, "codeActionProvider"),
            RenameProvider = ReadCapability(capsElement, "renameProvider"),
            DocumentSymbolProvider = ReadCapability(capsElement, "documentSymbolProvider"),
            WorkspaceSymbolProvider = ReadCapability(capsElement, "workspaceSymbolProvider"),
            ReferencesProvider = ReadCapability(capsElement, "referencesProvider"),
            SemanticTokensProvider = ReadCapability(capsElement, "semanticTokensProvider"),
            SemanticTokensLegend = ReadSemanticTokensLegend(capsElement)
        };
    }

    private static bool? ReadCapability(JsonElement capsElement, string property)
    {
        if (!capsElement.TryGetProperty(property, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => true,
            _ => null
        };
    }

    private static LspSemanticTokensLegend? ReadSemanticTokensLegend(JsonElement capsElement)
    {
        if (!capsElement.TryGetProperty("semanticTokensProvider", out JsonElement provider))
        {
            return null;
        }

        if (provider.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!provider.TryGetProperty("legend", out JsonElement legendElement)
            || legendElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        IReadOnlyList<string>? tokenTypes = null;
        if (legendElement.TryGetProperty("tokenTypes", out JsonElement typesElement)
            && typesElement.ValueKind == JsonValueKind.Array)
        {
            tokenTypes = Deserialize<IReadOnlyList<string>>(typesElement);
        }

        IReadOnlyList<string>? tokenModifiers = null;
        if (legendElement.TryGetProperty("tokenModifiers", out JsonElement modifiersElement)
            && modifiersElement.ValueKind == JsonValueKind.Array)
        {
            tokenModifiers = Deserialize<IReadOnlyList<string>>(modifiersElement);
        }

        if (tokenTypes is null && tokenModifiers is null)
        {
            return null;
        }

        return new LspSemanticTokensLegend
        {
            TokenTypes = tokenTypes ?? Array.Empty<string>(),
            TokenModifiers = tokenModifiers ?? Array.Empty<string>()
        };
    }

    private static IReadOnlyList<LspDocumentSymbol> ParseDocumentSymbolResult(JsonElement? result)
    {
        if (result is null)
        {
            return Array.Empty<LspDocumentSymbol>();
        }

        JsonElement element = result.Value;
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<LspDocumentSymbol>();
        }

        if (element.GetArrayLength() == 0)
        {
            return Array.Empty<LspDocumentSymbol>();
        }

        JsonElement first = element[0];
        if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("location", out _))
        {
            IReadOnlyList<LspSymbolInformation>? symbols = Deserialize<IReadOnlyList<LspSymbolInformation>>(element);
            if (symbols is null)
            {
                return Array.Empty<LspDocumentSymbol>();
            }

            return symbols.Select(info => new LspDocumentSymbol
            {
                Name = info.Name,
                Kind = info.Kind,
                Range = info.Location.Range,
                SelectionRange = info.Location.Range
            }).ToList();
        }

        return Deserialize<IReadOnlyList<LspDocumentSymbol>>(element) ?? Array.Empty<LspDocumentSymbol>();
    }

    private static T? Deserialize<T>(JsonElement? element)
    {
        if (element is null)
        {
            return default;
        }

        return element.Value.Deserialize<T>(LspMessageFraming.SerializerOptions);
    }

    private static T? Deserialize<T>(JsonElement element)
    {
        return element.Deserialize<T>(LspMessageFraming.SerializerOptions);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LspClientSession));
        }
    }
}
