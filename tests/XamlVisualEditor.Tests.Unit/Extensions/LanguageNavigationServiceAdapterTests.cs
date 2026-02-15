using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

#pragma warning disable CS0067

public sealed class LanguageNavigationServiceAdapterTests
{
    [Fact]
    public async Task FindImplementationsAsync_UsesDefinitionsFallback()
    {
        TestLanguageService service = new();
        service.Definitions = new[]
        {
            new LanguageLocation
            {
                FilePath = "/repo/impl.cs",
                Range = new LanguageTextRange(new LanguageTextPosition(4, 2), new LanguageTextPosition(4, 2))
            }
        };

        TestEditorDocument document = new("/repo/file.cs", "csharp");
        TestEditorServices editor = new(document);
        TestLanguageRegistry registry = new(service);
        registry.Resolver = (_, _) => service;
        LanguageNavigationServiceAdapter adapter = new(registry, editor);

        IReadOnlyList<LanguageLocation> results = await adapter.FindImplementationsAsync(
            new LanguagePositionContext
            {
                FilePath = "/repo/file.cs",
                Text = "class C {}",
                Offset = 3
            },
            CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(1, service.DefinitionCalls);
    }

    [Fact]
    public async Task GetWorkspaceSymbolsAsync_AggregatesAndDeduplicates()
    {
        TestLanguageService serviceA = new();
        serviceA.WorkspaceSymbols = new[]
        {
            CreateSymbol("/repo/a.cs", "A", 1, 1),
            CreateSymbol("/repo/shared.cs", "Shared", 10, 2)
        };
        TestLanguageService serviceB = new();
        serviceB.WorkspaceSymbols = new[]
        {
            CreateSymbol("/repo/shared.cs", "Shared", 10, 2),
            CreateSymbol("/repo/b.cs", "B", 3, 5)
        };

        TestLanguageRegistry registry = new(serviceA, serviceB);
        TestEditorServices editor = new();
        LanguageNavigationServiceAdapter adapter = new(registry, editor);

        IReadOnlyList<LanguageSymbol> results = await adapter.GetWorkspaceSymbolsAsync(
            new LanguageSymbolQuery { Query = "S" },
            CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.Equal(1, serviceA.WorkspaceSymbolCalls);
        Assert.Equal(1, serviceB.WorkspaceSymbolCalls);
    }

    [Fact]
    public async Task ApplyCodeActionAsync_AppliesEditToOpenDocument()
    {
        TestEditorDocument document = new("/repo/a.cs", "csharp");
        TestEditorServices editor = new(document);
        TestLanguageRegistry registry = new();
        LanguageNavigationServiceAdapter adapter = new(registry, editor);
        LanguageCodeAction action = new()
        {
            Title = "Fix",
            Edit = new LanguageWorkspaceEdit
            {
                DocumentEdits = new[]
                {
                    new LanguageDocumentEdit
                    {
                        FilePath = "/repo/a.cs",
                        Edits = new[]
                        {
                            new TextEdit { Offset = 0, Length = 0, NewText = "using System;\n" }
                        }
                    }
                }
            }
        };

        bool applied = await adapter.ApplyCodeActionAsync(action, CancellationToken.None);

        Assert.True(applied);
        Assert.Equal(1, document.ApplyCalls);
        Assert.Empty(editor.OpenWithBehaviorCalls);
    }

    [Fact]
    public async Task ApplyCodeActionAsync_OpensMissingDocumentInDocumentOnlyMode()
    {
        TestEditorDocument openedDocument = new("/repo/missing.cs", "csharp");
        TestEditorServices editor = new();
        editor.OpenDocumentResult = openedDocument;
        TestLanguageRegistry registry = new();
        LanguageNavigationServiceAdapter adapter = new(registry, editor);
        LanguageCodeAction action = new()
        {
            Title = "Fix",
            Edit = new LanguageWorkspaceEdit
            {
                DocumentEdits = new[]
                {
                    new LanguageDocumentEdit
                    {
                        FilePath = "/repo/missing.cs",
                        Edits = new[]
                        {
                            new TextEdit { Offset = 0, Length = 0, NewText = "namespace N;" }
                        }
                    }
                }
            }
        };

        bool applied = await adapter.ApplyCodeActionAsync(action, CancellationToken.None);

        Assert.True(applied);
        Assert.Single(editor.OpenWithBehaviorCalls);
        Assert.Equal(EditorDocumentOpenBehavior.DocumentOnly, editor.OpenWithBehaviorCalls[0].Behavior);
        Assert.Equal(1, openedDocument.ApplyCalls);
    }

    private static LanguageSymbol CreateSymbol(string filePath, string name, int line, int column)
    {
        return new LanguageSymbol
        {
            FilePath = filePath,
            Name = name,
            Kind = LanguageSymbolKind.Class,
            Range = new LanguageTextRange(new LanguageTextPosition(line, column), new LanguageTextPosition(line, column))
        };
    }

    private sealed class TestLanguageRegistry : ILanguageIntellisenseRegistry
    {
        public TestLanguageRegistry(params ILanguageIntellisenseService[] services)
        {
            Services = services;
        }

        public Func<string, string?, ILanguageIntellisenseService?>? Resolver { get; set; }

        public ILanguageIntellisenseService? GetService(string filePath, string? languageId)
        {
            if (Resolver is not null)
            {
                return Resolver(filePath, languageId);
            }

            return Services.FirstOrDefault();
        }

        public IReadOnlyList<ILanguageIntellisenseService> Services { get; }
    }

    private sealed class TestLanguageService : ILanguageIntellisenseService
    {
        public string LanguageId => "csharp";

        public IReadOnlyList<LanguageLocation> Definitions { get; set; } = Array.Empty<LanguageLocation>();
        public IReadOnlyList<LanguageSymbol> WorkspaceSymbols { get; set; } = Array.Empty<LanguageSymbol>();

        public int DefinitionCalls { get; private set; }
        public int WorkspaceSymbolCalls { get; private set; }

        public bool CanHandle(string filePath, string? languageId) => true;

        public Task InitializeWorkspaceAsync(string workspacePath, CancellationToken ct = default) => Task.CompletedTask;

        public Task ClearWorkspaceAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(CompletionContext context, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CompletionItem>>(Array.Empty<CompletionItem>());

        public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(LanguageDocumentContext context, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());

        public Task<IReadOnlyList<LanguageSemanticToken>> GetSemanticTokensAsync(LanguageDocumentContext context, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LanguageSemanticToken>>(Array.Empty<LanguageSemanticToken>());

        public Task<IReadOnlyList<TextEdit>> GetFormattingEditsAsync(LanguageDocumentContext context, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TextEdit>>(Array.Empty<TextEdit>());

        public Task<LanguageHover?> GetHoverAsync(LanguagePositionContext context, CancellationToken ct = default)
            => Task.FromResult<LanguageHover?>(null);

        public Task<IReadOnlyList<LanguageLocation>> FindDefinitionsAsync(LanguagePositionContext context, CancellationToken ct = default)
        {
            DefinitionCalls++;
            return Task.FromResult(Definitions);
        }

        public Task<IReadOnlyList<LanguageLocation>> FindReferencesAsync(LanguagePositionContext context, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LanguageLocation>>(Array.Empty<LanguageLocation>());

        public Task<LanguageRenameInfo?> PrepareRenameAsync(LanguagePositionContext context, CancellationToken ct = default)
            => Task.FromResult<LanguageRenameInfo?>(null);

        public Task<LanguageWorkspaceEdit?> RenameSymbolAsync(LanguageRenameContext context, CancellationToken ct = default)
            => Task.FromResult<LanguageWorkspaceEdit?>(null);

        public Task<LanguageSignatureHelp?> GetSignatureHelpAsync(LanguagePositionContext context, CancellationToken ct = default)
            => Task.FromResult<LanguageSignatureHelp?>(null);

        public Task<IReadOnlyList<LanguageCodeAction>> GetCodeActionsAsync(LanguageCodeActionContext context, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LanguageCodeAction>>(Array.Empty<LanguageCodeAction>());

        public Task<IReadOnlyList<LanguageSymbol>> GetDocumentSymbolsAsync(LanguageDocumentContext context, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LanguageSymbol>>(Array.Empty<LanguageSymbol>());

        public Task<IReadOnlyList<LanguageSymbol>> GetWorkspaceSymbolsAsync(LanguageSymbolQuery query, CancellationToken ct = default)
        {
            WorkspaceSymbolCalls++;
            return Task.FromResult(WorkspaceSymbols);
        }
    }

    private sealed class TestEditorServices : IEditorServices
    {
        private readonly List<IEditorDocument> _documents = new();

        public TestEditorServices()
        {
        }

        public TestEditorServices(params IEditorDocument[] documents)
        {
            _documents.AddRange(documents);
        }

        public IEditorDocument? OpenDocumentResult { get; set; }

        public List<(string FilePath, EditorDocumentOpenBehavior Behavior)> OpenWithBehaviorCalls { get; } = new();

        public IEditorDocument? ActiveDocument => _documents.FirstOrDefault();

        public IReadOnlyList<IEditorDocument> GetOpenDocuments() => _documents;

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, CancellationToken ct)
        {
            return Task.FromResult(OpenDocumentResult);
        }

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, EditorDocumentOpenBehavior behavior, CancellationToken ct)
        {
            OpenWithBehaviorCalls.Add((filePath, behavior));
            if (OpenDocumentResult is not null && !_documents.Contains(OpenDocumentResult))
            {
                _documents.Add(OpenDocumentResult);
            }

            return Task.FromResult(OpenDocumentResult);
        }

        public Task<bool> OpenLocationAsync(LanguageLocation location, CancellationToken ct)
        {
            return Task.FromResult(true);
        }

        public event EventHandler<EditorActiveDocumentChangedEventArgs>? ActiveDocumentChanged;
    }

    private sealed class TestEditorDocument : IEditorDocument
    {
        public TestEditorDocument(string filePath, string? languageId)
        {
            FilePath = filePath;
            LanguageId = languageId;
        }

        public int ApplyCalls { get; private set; }

        public string FilePath { get; }

        public string? LanguageId { get; }

        public int CaretOffset { get; set; }

        public int SelectionStart { get; set; }

        public int SelectionLength { get; set; }

        public Task<string> GetTextAsync(CancellationToken ct) => Task.FromResult(string.Empty);

        public Task ApplyEditsAsync(IReadOnlyList<TextEdit> edits, CancellationToken ct)
        {
            ApplyCalls++;
            return Task.CompletedTask;
        }

        public event EventHandler<EditorDocumentChangedEventArgs>? Changed;

        public event EventHandler<EditorSelectionChangedEventArgs>? SelectionChanged;
    }
}
