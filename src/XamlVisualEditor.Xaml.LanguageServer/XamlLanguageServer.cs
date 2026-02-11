using System.IO;
using System.Linq;
using System.Text.Json;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Core.Lsp;
using XamlVisualEditor.Workspace;
using XamlVisualEditor.Xaml.Intellisense;
using XamlVisualEditor.Xaml.Language;
using XamlVisualEditor.Xaml.Parsing;

namespace XamlVisualEditor.Xaml.LanguageServer;

public sealed class XamlLanguageServer
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
    private readonly XamlLanguageService _languageService;
    private readonly Dictionary<Uri, string> _documents = new();
    private bool _shutdownRequested;

    public XamlLanguageServer()
    {
        CompletionProviderRegistry completionRegistry = CompletionProviderRegistry.CreateDefault();
        IXamlParsingService parser = new XamlParsingService();
        ITypeMetadataService metadata = new TypeMetadataService();
        _languageService = new XamlLanguageService(completionRegistry, parser, metadata);
    }

    public async Task RunAsync(Stream input, Stream output, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            JsonDocument message = await LspMessageFraming.ReadMessageAsync(input, ct).ConfigureAwait(false);
            using (message)
            {
                if (!message.RootElement.TryGetProperty("method", out JsonElement methodElement))
                {
                    continue;
                }

                string? method = methodElement.GetString();
                if (string.IsNullOrWhiteSpace(method))
                {
                    continue;
                }

                bool hasId = message.RootElement.TryGetProperty("id", out JsonElement idElement);
                if (hasId)
                {
                    await HandleRequestAsync(method, idElement, message.RootElement, output, ct).ConfigureAwait(false);
                    if (_shutdownRequested && string.Equals(method, "shutdown", StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                else
                {
                    await HandleNotificationAsync(method, message.RootElement, output, ct).ConfigureAwait(false);
                }

                if (_shutdownRequested && string.Equals(method, "exit", StringComparison.Ordinal))
                {
                    break;
                }
            }
        }
    }

    private async Task HandleRequestAsync(
        string method,
        JsonElement id,
        JsonElement payload,
        Stream output,
        CancellationToken ct)
    {
        if (!payload.TryGetProperty("params", out JsonElement paramsElement))
        {
            paramsElement = default;
        }

        switch (method)
        {
            case "initialize":
                await SendResponseAsync(id, BuildInitializeResult(), output, ct).ConfigureAwait(false);
                return;
            case "textDocument/completion":
                await HandleCompletionAsync(id, paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/hover":
                await HandleHoverAsync(id, paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/signatureHelp":
                await HandleSignatureHelpAsync(id, paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/definition":
                await HandleDefinitionAsync(id, paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/references":
                await HandleReferencesAsync(id, paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/prepareRename":
                await HandlePrepareRenameAsync(id, paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/rename":
                await HandleRenameAsync(id, paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/formatting":
                await HandleFormattingAsync(id, paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/codeAction":
                await HandleCodeActionsAsync(id, paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/documentSymbol":
                await HandleDocumentSymbolsAsync(id, paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "workspace/symbol":
                await HandleWorkspaceSymbolsAsync(id, paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/semanticTokens/full":
                await HandleSemanticTokensAsync(id, paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "shutdown":
                _shutdownRequested = true;
                await SendResponseAsync(id, null, output, ct).ConfigureAwait(false);
                return;
            default:
                await SendResponseAsync(id, null, output, ct).ConfigureAwait(false);
                return;
        }
    }

    private async Task HandleNotificationAsync(
        string method,
        JsonElement payload,
        Stream output,
        CancellationToken ct)
    {
        if (!payload.TryGetProperty("params", out JsonElement paramsElement))
        {
            paramsElement = default;
        }

        switch (method)
        {
            case "textDocument/didOpen":
                await HandleDidOpenAsync(paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/didChange":
                await HandleDidChangeAsync(paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/didClose":
                await HandleDidCloseAsync(paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "textDocument/didSave":
                await HandleDidSaveAsync(paramsElement, output, ct).ConfigureAwait(false);
                return;
            case "initialized":
                return;
            case "exit":
                _shutdownRequested = true;
                return;
            default:
                return;
        }
    }

    private static object BuildInitializeResult()
    {
        return new
        {
            capabilities = new
            {
                textDocumentSync = 1,
                completionProvider = new
                {
                    triggerCharacters = new[] { "<", ":", ".", "{", "\"", "'" }
                },
                hoverProvider = true,
                signatureHelpProvider = new
                {
                    triggerCharacters = new[] { "(", "," }
                },
                definitionProvider = true,
                referencesProvider = true,
                renameProvider = new { prepareProvider = true },
                documentFormattingProvider = true,
                codeActionProvider = true,
                documentSymbolProvider = true,
                workspaceSymbolProvider = true,
                semanticTokensProvider = new
                {
                    legend = new
                    {
                        tokenTypes = s_semanticTokenTypes,
                        tokenModifiers = s_semanticTokenModifiers
                    },
                    full = true
                }
            }
        };
    }

    private async Task HandleDidOpenAsync(JsonElement paramsElement, Stream output, CancellationToken ct)
    {
        DidOpenParams? parameters = Deserialize<DidOpenParams>(paramsElement);
        if (parameters?.TextDocument is null)
        {
            return;
        }

        _documents[parameters.TextDocument.Uri] = parameters.TextDocument.Text;
        await PublishDiagnosticsAsync(parameters.TextDocument.Uri, parameters.TextDocument.Text, output, ct)
            .ConfigureAwait(false);
    }

    private async Task HandleDidChangeAsync(JsonElement paramsElement, Stream output, CancellationToken ct)
    {
        DidChangeParams? parameters = Deserialize<DidChangeParams>(paramsElement);
        if (parameters?.TextDocument is null || parameters.ContentChanges.Count == 0)
        {
            return;
        }

        LspTextDocumentContentChangeEvent change = parameters.ContentChanges[^1];
        _documents[parameters.TextDocument.Uri] = change.Text;
        await PublishDiagnosticsAsync(parameters.TextDocument.Uri, change.Text, output, ct).ConfigureAwait(false);
    }

    private async Task HandleDidCloseAsync(JsonElement paramsElement, Stream output, CancellationToken ct)
    {
        DidCloseParams? parameters = Deserialize<DidCloseParams>(paramsElement);
        if (parameters?.TextDocument is null)
        {
            return;
        }

        _documents.Remove(parameters.TextDocument.Uri);

        object payload = new
        {
            jsonrpc = "2.0",
            method = "textDocument/publishDiagnostics",
            @params = new LspPublishDiagnosticsParams
            {
                Uri = parameters.TextDocument.Uri,
                Diagnostics = Array.Empty<LspDiagnostic>()
            }
        };

        await LspMessageFraming.WriteMessageAsync(output, payload, ct).ConfigureAwait(false);
    }

    private async Task HandleDidSaveAsync(JsonElement paramsElement, Stream output, CancellationToken ct)
    {
        DidSaveParams? parameters = Deserialize<DidSaveParams>(paramsElement);
        if (parameters?.TextDocument is null)
        {
            return;
        }

        if (_documents.TryGetValue(parameters.TextDocument.Uri, out string? text))
        {
            await PublishDiagnosticsAsync(parameters.TextDocument.Uri, text, output, ct).ConfigureAwait(false);
        }
    }

    private async Task HandleCompletionAsync(
        JsonElement id,
        JsonElement paramsElement,
        Stream output,
        CancellationToken ct)
    {
        LspCompletionParams? parameters = Deserialize<LspCompletionParams>(paramsElement);
        if (parameters is null)
        {
            await SendResponseAsync(id, Array.Empty<LspCompletionItem>(), output, ct).ConfigureAwait(false);
            return;
        }

        if (!_documents.TryGetValue(parameters.TextDocument.Uri, out string? text))
        {
            text = string.Empty;
        }

        int offset = GetOffsetFromPosition(text, parameters.Position);
        CompletionContext context = new()
        {
            FilePath = parameters.TextDocument.Uri.LocalPath,
            DocumentText = text,
            TextBefore = text.Length > 0 ? text.Substring(0, Math.Clamp(offset, 0, text.Length)) : string.Empty,
            Offset = offset,
            Trigger = CompletionTrigger.Invoked
        };

        IReadOnlyList<CompletionItem> items = await _languageService.GetCompletionsAsync(context, ct)
            .ConfigureAwait(false);
        List<LspCompletionItem> results = new(items.Count);
        foreach (CompletionItem item in items)
        {
            results.Add(MapCompletion(item, text));
        }

        await SendResponseAsync(id, results, output, ct).ConfigureAwait(false);
    }

    private async Task HandleHoverAsync(
        JsonElement id,
        JsonElement paramsElement,
        Stream output,
        CancellationToken ct)
    {
        LspHoverParams? parameters = Deserialize<LspHoverParams>(paramsElement);
        if (parameters is null)
        {
            await SendResponseAsync(id, null, output, ct).ConfigureAwait(false);
            return;
        }

        if (!_documents.TryGetValue(parameters.TextDocument.Uri, out string? text))
        {
            text = string.Empty;
        }

        int offset = GetOffsetFromPosition(text, parameters.Position);
        LanguagePositionContext context = new()
        {
            FilePath = parameters.TextDocument.Uri.LocalPath,
            Text = text,
            Offset = offset
        };

        LanguageHover? hover = await _languageService.GetHoverAsync(context, ct).ConfigureAwait(false);
        if (hover is null)
        {
            await SendResponseAsync(id, null, output, ct).ConfigureAwait(false);
            return;
        }

        LspHover result = new()
        {
            Contents = hover.Contents,
            Range = MapRange(hover.Range)
        };

        await SendResponseAsync(id, result, output, ct).ConfigureAwait(false);
    }

    private async Task HandleSignatureHelpAsync(
        JsonElement id,
        JsonElement paramsElement,
        Stream output,
        CancellationToken ct)
    {
        LspSignatureHelpParams? parameters = Deserialize<LspSignatureHelpParams>(paramsElement);
        if (parameters is null)
        {
            await SendResponseAsync(id, null, output, ct).ConfigureAwait(false);
            return;
        }

        if (!_documents.TryGetValue(parameters.TextDocument.Uri, out string? text))
        {
            text = string.Empty;
        }

        int offset = GetOffsetFromPosition(text, parameters.Position);
        LanguagePositionContext context = new()
        {
            FilePath = parameters.TextDocument.Uri.LocalPath,
            Text = text,
            Offset = offset
        };

        LanguageSignatureHelp? help = await _languageService.GetSignatureHelpAsync(context, ct).ConfigureAwait(false);
        if (help is null)
        {
            await SendResponseAsync(id, null, output, ct).ConfigureAwait(false);
            return;
        }

        LspSignatureHelp result = MapSignatureHelp(help);
        await SendResponseAsync(id, result, output, ct).ConfigureAwait(false);
    }

    private async Task HandleDefinitionAsync(
        JsonElement id,
        JsonElement paramsElement,
        Stream output,
        CancellationToken ct)
    {
        LspDefinitionParams? parameters = Deserialize<LspDefinitionParams>(paramsElement);
        if (parameters is null)
        {
            await SendResponseAsync(id, Array.Empty<LspLocation>(), output, ct).ConfigureAwait(false);
            return;
        }

        if (!_documents.TryGetValue(parameters.TextDocument.Uri, out string? text))
        {
            text = string.Empty;
        }

        int offset = GetOffsetFromPosition(text, parameters.Position);
        LanguagePositionContext context = new()
        {
            FilePath = parameters.TextDocument.Uri.LocalPath,
            Text = text,
            Offset = offset
        };

        IReadOnlyList<LanguageLocation> locations = await _languageService.FindDefinitionsAsync(context, ct)
            .ConfigureAwait(false);
        IReadOnlyList<LspLocation> mapped = MapLocations(locations);
        await SendResponseAsync(id, mapped, output, ct).ConfigureAwait(false);
    }

    private async Task HandleReferencesAsync(
        JsonElement id,
        JsonElement paramsElement,
        Stream output,
        CancellationToken ct)
    {
        LspReferenceParams? parameters = Deserialize<LspReferenceParams>(paramsElement);
        if (parameters is null)
        {
            await SendResponseAsync(id, Array.Empty<LspLocation>(), output, ct).ConfigureAwait(false);
            return;
        }

        if (!_documents.TryGetValue(parameters.TextDocument.Uri, out string? text))
        {
            text = string.Empty;
        }

        int offset = GetOffsetFromPosition(text, parameters.Position);
        LanguagePositionContext context = new()
        {
            FilePath = parameters.TextDocument.Uri.LocalPath,
            Text = text,
            Offset = offset
        };

        IReadOnlyList<LanguageLocation> locations = await _languageService.FindReferencesAsync(context, ct)
            .ConfigureAwait(false);
        IReadOnlyList<LspLocation> mapped = MapLocations(locations);
        await SendResponseAsync(id, mapped, output, ct).ConfigureAwait(false);
    }

    private async Task HandlePrepareRenameAsync(
        JsonElement id,
        JsonElement paramsElement,
        Stream output,
        CancellationToken ct)
    {
        LspPrepareRenameParams? parameters = Deserialize<LspPrepareRenameParams>(paramsElement);
        if (parameters is null)
        {
            await SendResponseAsync(id, null, output, ct).ConfigureAwait(false);
            return;
        }

        if (!_documents.TryGetValue(parameters.TextDocument.Uri, out string? text))
        {
            text = string.Empty;
        }

        int offset = GetOffsetFromPosition(text, parameters.Position);
        LanguagePositionContext context = new()
        {
            FilePath = parameters.TextDocument.Uri.LocalPath,
            Text = text,
            Offset = offset
        };

        LanguageRenameInfo? renameInfo = await _languageService.PrepareRenameAsync(context, ct)
            .ConfigureAwait(false);
        if (renameInfo is null)
        {
            await SendResponseAsync(id, null, output, ct).ConfigureAwait(false);
            return;
        }

        object result = new
        {
            range = MapRange(renameInfo.Range),
            placeholder = renameInfo.Name
        };

        await SendResponseAsync(id, result, output, ct).ConfigureAwait(false);
    }

    private async Task HandleRenameAsync(
        JsonElement id,
        JsonElement paramsElement,
        Stream output,
        CancellationToken ct)
    {
        LspRenameParams? parameters = Deserialize<LspRenameParams>(paramsElement);
        if (parameters is null)
        {
            await SendResponseAsync(id, null, output, ct).ConfigureAwait(false);
            return;
        }

        if (!_documents.TryGetValue(parameters.TextDocument.Uri, out string? text))
        {
            text = string.Empty;
        }

        int offset = GetOffsetFromPosition(text, parameters.Position);
        LanguageRenameContext context = new()
        {
            FilePath = parameters.TextDocument.Uri.LocalPath,
            Text = text,
            Offset = offset,
            NewName = parameters.NewName
        };

        LanguageWorkspaceEdit? edit = await _languageService.RenameSymbolAsync(context, ct).ConfigureAwait(false);
        LspWorkspaceEdit? mapped = edit is null ? null : MapWorkspaceEdit(edit, text);
        await SendResponseAsync(id, mapped, output, ct).ConfigureAwait(false);
    }

    private async Task HandleFormattingAsync(
        JsonElement id,
        JsonElement paramsElement,
        Stream output,
        CancellationToken ct)
    {
        LspDocumentFormattingParams? parameters = Deserialize<LspDocumentFormattingParams>(paramsElement);
        if (parameters is null)
        {
            await SendResponseAsync(id, Array.Empty<LspTextEdit>(), output, ct).ConfigureAwait(false);
            return;
        }

        if (!_documents.TryGetValue(parameters.TextDocument.Uri, out string? text))
        {
            text = string.Empty;
        }

        LanguageDocumentContext context = new()
        {
            FilePath = parameters.TextDocument.Uri.LocalPath,
            Text = text
        };

        IReadOnlyList<TextEdit> edits = await _languageService.GetFormattingEditsAsync(context, ct)
            .ConfigureAwait(false);
        IReadOnlyList<LspTextEdit> mapped = MapTextEdits(edits, text);
        await SendResponseAsync(id, mapped, output, ct).ConfigureAwait(false);
    }

    private async Task HandleCodeActionsAsync(
        JsonElement id,
        JsonElement paramsElement,
        Stream output,
        CancellationToken ct)
    {
        LspCodeActionParams? parameters = Deserialize<LspCodeActionParams>(paramsElement);
        if (parameters is null)
        {
            await SendResponseAsync(id, Array.Empty<LspCodeAction>(), output, ct).ConfigureAwait(false);
            return;
        }

        if (!_documents.TryGetValue(parameters.TextDocument.Uri, out string? text))
        {
            text = string.Empty;
        }

        int startOffset = GetOffsetFromPosition(text, parameters.Range.Start);
        int endOffset = GetOffsetFromPosition(text, parameters.Range.End);
        int length = Math.Max(0, endOffset - startOffset);
        LanguageCodeActionContext context = new()
        {
            FilePath = parameters.TextDocument.Uri.LocalPath,
            Text = text,
            Offset = startOffset,
            Length = length
        };

        IReadOnlyList<LanguageCodeAction> actions = await _languageService.GetCodeActionsAsync(context, ct)
            .ConfigureAwait(false);
        IReadOnlyList<LspCodeAction> mapped = MapCodeActions(actions, text);
        await SendResponseAsync(id, mapped, output, ct).ConfigureAwait(false);
    }

    private async Task HandleDocumentSymbolsAsync(
        JsonElement id,
        JsonElement paramsElement,
        Stream output,
        CancellationToken ct)
    {
        LspDocumentSymbolParams? parameters = Deserialize<LspDocumentSymbolParams>(paramsElement);
        if (parameters is null)
        {
            await SendResponseAsync(id, Array.Empty<LspDocumentSymbol>(), output, ct).ConfigureAwait(false);
            return;
        }

        if (!_documents.TryGetValue(parameters.TextDocument.Uri, out string? text))
        {
            text = string.Empty;
        }

        LanguageDocumentContext context = new()
        {
            FilePath = parameters.TextDocument.Uri.LocalPath,
            Text = text
        };

        IReadOnlyList<LanguageSymbol> symbols = await _languageService.GetDocumentSymbolsAsync(context, ct)
            .ConfigureAwait(false);
        IReadOnlyList<LspDocumentSymbol> mapped = MapDocumentSymbols(symbols);
        await SendResponseAsync(id, mapped, output, ct).ConfigureAwait(false);
    }

    private async Task HandleWorkspaceSymbolsAsync(
        JsonElement id,
        JsonElement paramsElement,
        Stream output,
        CancellationToken ct)
    {
        LspWorkspaceSymbolParams? parameters = Deserialize<LspWorkspaceSymbolParams>(paramsElement);
        if (parameters is null)
        {
            await SendResponseAsync(id, Array.Empty<LspSymbolInformation>(), output, ct).ConfigureAwait(false);
            return;
        }

        LanguageSymbolQuery query = new()
        {
            Query = parameters.Query
        };

        IReadOnlyList<LanguageSymbol> symbols = await _languageService.GetWorkspaceSymbolsAsync(query, ct)
            .ConfigureAwait(false);
        IReadOnlyList<LspSymbolInformation> mapped = MapWorkspaceSymbols(symbols);
        await SendResponseAsync(id, mapped, output, ct).ConfigureAwait(false);
    }

    private async Task HandleSemanticTokensAsync(
        JsonElement id,
        JsonElement paramsElement,
        Stream output,
        CancellationToken ct)
    {
        LspSemanticTokensParams? parameters = Deserialize<LspSemanticTokensParams>(paramsElement);
        if (parameters is null)
        {
            await SendResponseAsync(id, null, output, ct).ConfigureAwait(false);
            return;
        }

        if (!_documents.TryGetValue(parameters.TextDocument.Uri, out string? text))
        {
            text = string.Empty;
        }

        LanguageDocumentContext context = new()
        {
            FilePath = parameters.TextDocument.Uri.LocalPath,
            Text = text
        };

        IReadOnlyList<LanguageSemanticToken> tokens = await _languageService.GetSemanticTokensAsync(context, ct)
            .ConfigureAwait(false);
        LspSemanticTokens result = EncodeSemanticTokens(tokens);
        await SendResponseAsync(id, result, output, ct).ConfigureAwait(false);
    }

    private async Task PublishDiagnosticsAsync(Uri uri, string text, Stream output, CancellationToken ct)
    {
        LanguageDocumentContext context = new()
        {
            FilePath = uri.LocalPath,
            Text = text
        };

        IReadOnlyList<LanguageDiagnostic> diagnostics = await _languageService.GetDiagnosticsAsync(context, ct)
            .ConfigureAwait(false);
        IReadOnlyList<LspDiagnostic> mapped = MapDiagnostics(diagnostics);

        object payload = new
        {
            jsonrpc = "2.0",
            method = "textDocument/publishDiagnostics",
            @params = new LspPublishDiagnosticsParams
            {
                Uri = uri,
                Diagnostics = mapped
            }
        };

        await LspMessageFraming.WriteMessageAsync(output, payload, ct).ConfigureAwait(false);
    }

    private static LspCompletionItem MapCompletion(CompletionItem item, string text)
    {
        LspTextEdit? edit = null;
        if (item.TextEdit is not null)
        {
            LspRange range = new(
                GetPositionFromOffset(text, item.TextEdit.Offset),
                GetPositionFromOffset(text, item.TextEdit.Offset + item.TextEdit.Length));
            edit = new LspTextEdit
            {
                Range = range,
                NewText = item.TextEdit.NewText
            };
        }

        return new LspCompletionItem
        {
            Label = item.DisplayText,
            InsertText = item.InsertText,
            Detail = item.Description,
            Documentation = item.Documentation,
            SortText = item.SortText,
            FilterText = item.FilterText,
            Kind = MapCompletionKind(item.Kind),
            TextEdit = edit,
            CommitCharacters = item.CommitCharacters is null
                ? Array.Empty<string>()
                : item.CommitCharacters.Select(c => c.ToString()).ToArray()
        };
    }

    private static LspCompletionItemKind MapCompletionKind(CompletionItemKind kind)
    {
        return kind switch
        {
            CompletionItemKind.Element => LspCompletionItemKind.Class,
            CompletionItemKind.Property => LspCompletionItemKind.Property,
            CompletionItemKind.Value => LspCompletionItemKind.Value,
            CompletionItemKind.Namespace => LspCompletionItemKind.Module,
            CompletionItemKind.MarkupExtension => LspCompletionItemKind.Snippet,
            CompletionItemKind.ClosingTag => LspCompletionItemKind.Keyword,
            _ => LspCompletionItemKind.Text
        };
    }

    private static IReadOnlyList<LspLocation> MapLocations(IReadOnlyList<LanguageLocation> locations)
    {
        if (locations.Count == 0)
        {
            return Array.Empty<LspLocation>();
        }

        List<LspLocation> results = new(locations.Count);
        foreach (LanguageLocation location in locations)
        {
            results.Add(new LspLocation
            {
                Uri = new Uri(Path.GetFullPath(location.FilePath)),
                Range = new LspRange(
                    new LspPosition(location.Range.Start.Line - 1, location.Range.Start.Column - 1),
                    new LspPosition(location.Range.End.Line - 1, location.Range.End.Column - 1))
            });
        }

        return results;
    }

    private static IReadOnlyList<LspTextEdit> MapTextEdits(IReadOnlyList<TextEdit> edits, string text)
    {
        if (edits.Count == 0)
        {
            return Array.Empty<LspTextEdit>();
        }

        List<LspTextEdit> results = new(edits.Count);
        foreach (TextEdit edit in edits)
        {
            results.Add(new LspTextEdit
            {
                Range = new LspRange(
                    GetPositionFromOffset(text, edit.Offset),
                    GetPositionFromOffset(text, edit.Offset + edit.Length)),
                NewText = edit.NewText
            });
        }

        return results;
    }

    private IReadOnlyList<LspCodeAction> MapCodeActions(
        IReadOnlyList<LanguageCodeAction> actions,
        string text)
    {
        if (actions.Count == 0)
        {
            return Array.Empty<LspCodeAction>();
        }

        List<LspCodeAction> results = new(actions.Count);
        foreach (LanguageCodeAction action in actions)
        {
            results.Add(new LspCodeAction
            {
                Title = action.Title,
                Kind = action.Kind,
                Edit = action.Edit is null ? null : MapWorkspaceEdit(action.Edit, text)
            });
        }

        return results;
    }

    private LspWorkspaceEdit MapWorkspaceEdit(LanguageWorkspaceEdit edit, string text)
    {
        List<LspTextDocumentEdit> documentEdits = new(edit.DocumentEdits.Count);
        foreach (LanguageDocumentEdit documentEdit in edit.DocumentEdits)
        {
            string documentText = GetDocumentText(documentEdit.FilePath, text);
            documentEdits.Add(new LspTextDocumentEdit
            {
                TextDocument = new LspVersionedTextDocumentIdentifier
                {
                    Uri = new Uri(Path.GetFullPath(documentEdit.FilePath)),
                    Version = null
                },
                Edits = MapTextEdits(documentEdit.Edits, documentText)
            });
        }

        return new LspWorkspaceEdit
        {
            DocumentChanges = documentEdits
        };
    }

    private string GetDocumentText(string filePath, string fallbackText)
    {
        Uri uri = new(Path.GetFullPath(filePath));
        if (_documents.TryGetValue(uri, out string? text))
        {
            return text;
        }

        return fallbackText;
    }

    private static IReadOnlyList<LspDocumentSymbol> MapDocumentSymbols(IReadOnlyList<LanguageSymbol> symbols)
    {
        if (symbols.Count == 0)
        {
            return Array.Empty<LspDocumentSymbol>();
        }

        List<LspDocumentSymbol> results = new(symbols.Count);
        foreach (LanguageSymbol symbol in symbols)
        {
            LspRange range = new(
                new LspPosition(symbol.Range.Start.Line - 1, symbol.Range.Start.Column - 1),
                new LspPosition(symbol.Range.End.Line - 1, symbol.Range.End.Column - 1));
            results.Add(new LspDocumentSymbol
            {
                Name = symbol.Name,
                Kind = MapSymbolKind(symbol.Kind),
                Range = range,
                SelectionRange = range
            });
        }

        return results;
    }

    private static LspSignatureHelp MapSignatureHelp(LanguageSignatureHelp help)
    {
        List<LspSignatureInformation> signatures = new(help.Signatures.Count);
        foreach (LanguageSignature signature in help.Signatures)
        {
            List<LspParameterInformation> parameters = new(signature.Parameters.Count);
            foreach (LanguageParameter parameter in signature.Parameters)
            {
                parameters.Add(new LspParameterInformation
                {
                    Label = parameter.Label,
                    Documentation = parameter.Documentation
                });
            }

            signatures.Add(new LspSignatureInformation
            {
                Label = signature.Label,
                Documentation = signature.Documentation,
                Parameters = parameters
            });
        }

        return new LspSignatureHelp
        {
            Signatures = signatures,
            ActiveSignature = help.ActiveSignature,
            ActiveParameter = help.ActiveParameter
        };
    }

    private static IReadOnlyList<LspSymbolInformation> MapWorkspaceSymbols(IReadOnlyList<LanguageSymbol> symbols)
    {
        if (symbols.Count == 0)
        {
            return Array.Empty<LspSymbolInformation>();
        }

        List<LspSymbolInformation> results = new(symbols.Count);
        foreach (LanguageSymbol symbol in symbols)
        {
            results.Add(new LspSymbolInformation
            {
                Name = symbol.Name,
                Kind = MapSymbolKind(symbol.Kind),
                Location = new LspLocation
                {
                    Uri = new Uri(Path.GetFullPath(symbol.FilePath)),
                    Range = new LspRange(
                        new LspPosition(symbol.Range.Start.Line - 1, symbol.Range.Start.Column - 1),
                        new LspPosition(symbol.Range.End.Line - 1, symbol.Range.End.Column - 1))
                }
            });
        }

        return results;
    }

    private static LspSymbolKind MapSymbolKind(LanguageSymbolKind kind)
    {
        return kind switch
        {
            LanguageSymbolKind.File => LspSymbolKind.File,
            LanguageSymbolKind.Module => LspSymbolKind.Module,
            LanguageSymbolKind.Namespace => LspSymbolKind.Namespace,
            LanguageSymbolKind.Package => LspSymbolKind.Package,
            LanguageSymbolKind.Class => LspSymbolKind.Class,
            LanguageSymbolKind.Method => LspSymbolKind.Method,
            LanguageSymbolKind.Property => LspSymbolKind.Property,
            LanguageSymbolKind.Field => LspSymbolKind.Field,
            LanguageSymbolKind.Constructor => LspSymbolKind.Constructor,
            LanguageSymbolKind.Enum => LspSymbolKind.Enum,
            LanguageSymbolKind.Interface => LspSymbolKind.Interface,
            LanguageSymbolKind.Function => LspSymbolKind.Function,
            LanguageSymbolKind.Variable => LspSymbolKind.Variable,
            LanguageSymbolKind.Constant => LspSymbolKind.Constant,
            LanguageSymbolKind.String => LspSymbolKind.String,
            LanguageSymbolKind.Number => LspSymbolKind.Number,
            LanguageSymbolKind.Boolean => LspSymbolKind.Boolean,
            LanguageSymbolKind.Array => LspSymbolKind.Array,
            LanguageSymbolKind.Object => LspSymbolKind.Object,
            LanguageSymbolKind.Key => LspSymbolKind.Key,
            LanguageSymbolKind.Null => LspSymbolKind.Null,
            LanguageSymbolKind.EnumMember => LspSymbolKind.EnumMember,
            LanguageSymbolKind.Struct => LspSymbolKind.Struct,
            LanguageSymbolKind.Event => LspSymbolKind.Event,
            LanguageSymbolKind.Operator => LspSymbolKind.Operator,
            LanguageSymbolKind.TypeParameter => LspSymbolKind.TypeParameter,
            _ => LspSymbolKind.Property
        };
    }

    private static LspSemanticTokens EncodeSemanticTokens(IReadOnlyList<LanguageSemanticToken> tokens)
    {
        if (tokens.Count == 0)
        {
            return new LspSemanticTokens();
        }

        List<LanguageSemanticToken> sorted = tokens
            .OrderBy(token => token.Range.Start.Line)
            .ThenBy(token => token.Range.Start.Column)
            .ToList();

        List<int> data = new(sorted.Count * 5);
        int lastLine = 0;
        int lastChar = 0;

        foreach (LanguageSemanticToken token in sorted)
        {
            int line = Math.Max(0, token.Range.Start.Line - 1);
            int startChar = Math.Max(0, token.Range.Start.Column - 1);
            int endLine = Math.Max(0, token.Range.End.Line - 1);
            int endChar = Math.Max(0, token.Range.End.Column - 1);

            int deltaLine = line - lastLine;
            int deltaStart = deltaLine == 0 ? startChar - lastChar : startChar;
            int length = endLine == line ? Math.Max(1, endChar - startChar) : 1;

            int tokenType = Array.IndexOf(s_semanticTokenTypes, token.Type);
            if (tokenType < 0)
            {
                tokenType = 0;
            }

            data.Add(deltaLine);
            data.Add(deltaStart);
            data.Add(length);
            data.Add(tokenType);
            data.Add(0);

            lastLine = line;
            lastChar = startChar;
        }

        return new LspSemanticTokens
        {
            Data = data
        };
    }

    private static IReadOnlyList<LspDiagnostic> MapDiagnostics(IReadOnlyList<LanguageDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return Array.Empty<LspDiagnostic>();
        }

        List<LspDiagnostic> results = new(diagnostics.Count);
        foreach (LanguageDiagnostic diagnostic in diagnostics)
        {
            results.Add(new LspDiagnostic
            {
                Message = diagnostic.Message,
                Severity = MapSeverity(diagnostic.Severity),
                Range = new LspRange(
                    new LspPosition(diagnostic.Range.Start.Line - 1, diagnostic.Range.Start.Column - 1),
                    new LspPosition(diagnostic.Range.End.Line - 1, diagnostic.Range.End.Column - 1)),
                Source = diagnostic.Source,
                Code = diagnostic.Code
            });
        }

        return results;
    }

    private static LspDiagnosticSeverity MapSeverity(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Error => LspDiagnosticSeverity.Error,
            DiagnosticSeverity.Warning => LspDiagnosticSeverity.Warning,
            DiagnosticSeverity.Info => LspDiagnosticSeverity.Information,
            _ => LspDiagnosticSeverity.Information
        };
    }

    private static LspRange? MapRange(LanguageTextRange? range)
    {
        if (range is null)
        {
            return null;
        }

        return new LspRange(
            new LspPosition(range.Value.Start.Line - 1, range.Value.Start.Column - 1),
            new LspPosition(range.Value.End.Line - 1, range.Value.End.Column - 1));
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

    private static LspPosition GetPositionFromOffset(string text, int offset)
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

    private static T? Deserialize<T>(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return default;
        }

        return element.Deserialize<T>(LspMessageFraming.SerializerOptions);
    }

    private static async Task SendResponseAsync(
        JsonElement id,
        object? result,
        Stream output,
        CancellationToken ct)
    {
        object? responseId = id.ValueKind switch
        {
            JsonValueKind.String => id.GetString(),
            JsonValueKind.Number => id.TryGetInt64(out long value) ? value : null,
            _ => null
        };

        object payload = new
        {
            jsonrpc = "2.0",
            id = responseId,
            result
        };

        await LspMessageFraming.WriteMessageAsync(output, payload, ct).ConfigureAwait(false);
    }

    private sealed class DidOpenParams
    {
        public LspTextDocumentItem? TextDocument { get; init; }
    }

    private sealed class DidChangeParams
    {
        public LspVersionedTextDocumentIdentifier? TextDocument { get; init; }
        public IReadOnlyList<LspTextDocumentContentChangeEvent> ContentChanges { get; init; } = Array.Empty<LspTextDocumentContentChangeEvent>();
    }

    private sealed class DidCloseParams
    {
        public LspTextDocumentIdentifier? TextDocument { get; init; }
    }

    private sealed class DidSaveParams
    {
        public LspTextDocumentIdentifier? TextDocument { get; init; }
    }
}
