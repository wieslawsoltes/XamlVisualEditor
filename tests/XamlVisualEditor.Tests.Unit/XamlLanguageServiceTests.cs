using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Workspace;
using XamlVisualEditor.Xaml.Intellisense;
using XamlVisualEditor.Xaml.Language;
using XamlVisualEditor.Xaml.Parsing;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class XamlLanguageServiceTests
{
    [Fact]
    public async Task DiagnosticsReportXamlErrors()
    {
        CompletionProviderRegistry registry = CompletionProviderRegistry.CreateDefault();
        IXamlParsingService parser = new XamlParsingService();
        ITypeMetadataService metadataService = new TypeMetadataService();
        XamlLanguageService service = new(registry, parser, metadataService);

        string filePath = Path.Combine(Path.GetTempPath(), "Test.axaml");
        string text = "<UserControl><Grid></UserControl>";

        LanguageDocumentContext context = new()
        {
            FilePath = filePath,
            Text = text
        };

        IReadOnlyList<XamlVisualEditor.Core.LanguageDiagnostic> diagnostics =
            await service.GetDiagnosticsAsync(context);

        Assert.Contains(diagnostics, d => d.Severity == XamlVisualEditor.Core.DiagnosticSeverity.Error);
    }
}
