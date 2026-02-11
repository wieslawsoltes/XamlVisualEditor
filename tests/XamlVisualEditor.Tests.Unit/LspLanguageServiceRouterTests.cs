using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Core.Lsp;
using XamlVisualEditor.Lsp;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class LspLanguageServiceRouterTests
{
    [Fact]
    public async Task ReturnsSessionWhenLanguageMatches()
    {
        LspServerConfiguration config = new()
        {
            LanguageId = "csharp",
            ServerPath = "unused"
        };

        StubSession session = new();
        LspLanguageServiceRouter router = new(
            new[] { config },
            null,
            (_, _) => session);

        LanguageWorkspaceInfo workspace = new()
        {
            RootPath = "/tmp"
        };

        ILanguageServiceSession? resolved = await router.GetSessionAsync("csharp", workspace);

        Assert.Same(session, resolved);
    }

    [Fact]
    public async Task ReturnsNullForUnknownLanguage()
    {
        LspServerConfiguration config = new()
        {
            LanguageId = "csharp",
            ServerPath = "unused"
        };

        LspLanguageServiceRouter router = new(new[] { config }, null);

        LanguageWorkspaceInfo workspace = new()
        {
            RootPath = "/tmp"
        };

        ILanguageServiceSession? resolved = await router.GetSessionAsync("python", workspace);

        Assert.Null(resolved);
    }

    #pragma warning disable CS0067
    private sealed class StubSession : ILanguageServiceSession
    {
        public string LanguageId => "csharp";

        public LspServerCapabilities Capabilities { get; } = new();

        public bool IsAlive => true;

        public event EventHandler<LspPublishDiagnosticsParams>? DiagnosticsPublished;

        public event EventHandler<LanguageServiceSessionFaultedEventArgs>? SessionFaulted;

        public ValueTask InitializeAsync(LspInitializeParams options, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask ShutdownAsync(CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishDocumentAsync(LspTextDocumentItem document, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask ApplyDocumentChangesAsync(
            LspVersionedTextDocumentIdentifier documentId,
            IReadOnlyList<LspTextDocumentContentChangeEvent> changes,
            CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<LspDiagnostic>> GetDiagnosticsAsync(Uri documentUri, CancellationToken ct = default)
        {
            return new ValueTask<IReadOnlyList<LspDiagnostic>>(Array.Empty<LspDiagnostic>());
        }

        public ValueTask<IReadOnlyList<LspCompletionItem>> GetCompletionsAsync(
            LspCompletionParams parameters,
            CancellationToken ct = default)
        {
            return new ValueTask<IReadOnlyList<LspCompletionItem>>(Array.Empty<LspCompletionItem>());
        }

        public ValueTask<LspHover?> GetHoverAsync(LspHoverParams parameters, CancellationToken ct = default)
        {
            return new ValueTask<LspHover?>((LspHover?)null);
        }

        public ValueTask<LspSignatureHelp?> GetSignatureHelpAsync(
            LspSignatureHelpParams parameters,
            CancellationToken ct = default)
        {
            return new ValueTask<LspSignatureHelp?>((LspSignatureHelp?)null);
        }

        public ValueTask<IReadOnlyList<LspLocation>> GetDefinitionAsync(
            LspDefinitionParams parameters,
            CancellationToken ct = default)
        {
            return new ValueTask<IReadOnlyList<LspLocation>>(Array.Empty<LspLocation>());
        }

        public ValueTask<IReadOnlyList<LspTextEdit>> GetFormattingAsync(
            LspDocumentFormattingParams parameters,
            CancellationToken ct = default)
        {
            return new ValueTask<IReadOnlyList<LspTextEdit>>(Array.Empty<LspTextEdit>());
        }

        public ValueTask<IReadOnlyList<LspCodeAction>> GetCodeActionsAsync(
            LspCodeActionParams parameters,
            CancellationToken ct = default)
        {
            return new ValueTask<IReadOnlyList<LspCodeAction>>(Array.Empty<LspCodeAction>());
        }

        public ValueTask<IReadOnlyList<LspLocation>> GetReferencesAsync(
            LspReferenceParams parameters,
            CancellationToken ct = default)
        {
            return new ValueTask<IReadOnlyList<LspLocation>>(Array.Empty<LspLocation>());
        }

        public ValueTask<LspRange?> PrepareRenameAsync(
            LspPrepareRenameParams parameters,
            CancellationToken ct = default)
        {
            return new ValueTask<LspRange?>((LspRange?)null);
        }

        public ValueTask<LspWorkspaceEdit?> RenameAsync(
            LspRenameParams parameters,
            CancellationToken ct = default)
        {
            return new ValueTask<LspWorkspaceEdit?>((LspWorkspaceEdit?)null);
        }

        public ValueTask<IReadOnlyList<LspDocumentSymbol>> GetDocumentSymbolsAsync(
            LspDocumentSymbolParams parameters,
            CancellationToken ct = default)
        {
            return new ValueTask<IReadOnlyList<LspDocumentSymbol>>(Array.Empty<LspDocumentSymbol>());
        }

        public ValueTask<IReadOnlyList<LspSymbolInformation>> GetWorkspaceSymbolsAsync(
            LspWorkspaceSymbolParams parameters,
            CancellationToken ct = default)
        {
            return new ValueTask<IReadOnlyList<LspSymbolInformation>>(Array.Empty<LspSymbolInformation>());
        }

        public ValueTask<LspSemanticTokens?> GetSemanticTokensAsync(
            LspSemanticTokensParams parameters,
            CancellationToken ct = default)
        {
            return new ValueTask<LspSemanticTokens?>((LspSemanticTokens?)null);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
    #pragma warning restore CS0067
}
