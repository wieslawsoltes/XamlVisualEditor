using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Language;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class LanguageServiceRegistryTests
{
    [Fact]
    public void ResolvesMatchingService()
    {
        ILanguageIntellisenseService csharp = new StubLanguageService("csharp", ".cs");
        ILanguageIntellisenseService xml = new StubLanguageService("xml", ".xaml");

        LanguageServiceRegistry registry = new(new[] { csharp, xml });

        ILanguageIntellisenseService? resolved = registry.GetService("/tmp/Test.cs", "csharp");

        Assert.Same(csharp, resolved);
    }

    private sealed class StubLanguageService : ILanguageIntellisenseService
    {
        private readonly string _languageId;
        private readonly string _extension;

        public StubLanguageService(string languageId, string extension)
        {
            _languageId = languageId;
            _extension = extension;
        }

        public string LanguageId => _languageId;

        public bool CanHandle(string filePath, string? languageId)
        {
            return filePath.EndsWith(_extension, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(languageId, _languageId, StringComparison.OrdinalIgnoreCase);
        }

        public Task InitializeWorkspaceAsync(string workspacePath, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearWorkspaceAsync(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
            CompletionContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<CompletionItem>>(Array.Empty<CompletionItem>());
        }

        public Task<IReadOnlyList<XamlVisualEditor.Core.LanguageDiagnostic>> GetDiagnosticsAsync(
            LanguageDocumentContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<XamlVisualEditor.Core.LanguageDiagnostic>>(Array.Empty<XamlVisualEditor.Core.LanguageDiagnostic>());
        }

        public Task<XamlVisualEditor.Core.LanguageHover?> GetHoverAsync(
            LanguagePositionContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult<XamlVisualEditor.Core.LanguageHover?>(null);
        }

        public Task<IReadOnlyList<XamlVisualEditor.Core.LanguageLocation>> FindDefinitionsAsync(
            LanguagePositionContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<XamlVisualEditor.Core.LanguageLocation>>(Array.Empty<XamlVisualEditor.Core.LanguageLocation>());
        }

        public Task<IReadOnlyList<XamlVisualEditor.Core.LanguageLocation>> FindReferencesAsync(
            LanguagePositionContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<XamlVisualEditor.Core.LanguageLocation>>(Array.Empty<XamlVisualEditor.Core.LanguageLocation>());
        }

        public Task<XamlVisualEditor.Core.LanguageRenameInfo?> PrepareRenameAsync(
            LanguagePositionContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult<XamlVisualEditor.Core.LanguageRenameInfo?>(null);
        }

        public Task<XamlVisualEditor.Core.LanguageWorkspaceEdit?> RenameSymbolAsync(
            LanguageRenameContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult<XamlVisualEditor.Core.LanguageWorkspaceEdit?>(null);
        }

        public Task<XamlVisualEditor.Core.LanguageSignatureHelp?> GetSignatureHelpAsync(
            LanguagePositionContext context,
            CancellationToken ct = default)
        {
            return Task.FromResult<XamlVisualEditor.Core.LanguageSignatureHelp?>(null);
        }
    }
}
