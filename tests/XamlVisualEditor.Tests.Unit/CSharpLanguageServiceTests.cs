using XamlVisualEditor.CSharp.Language;
using XamlVisualEditor.Core.Interfaces;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class CSharpLanguageServiceTests
{
    [Fact]
    public async Task DiagnosticsReportErrors()
    {
        CSharpLanguageService service = new();
        string filePath = Path.Combine(Path.GetTempPath(), "Test.cs");
        string text = "using System; class C { void M() { int x = ; } }";

        LanguageDocumentContext context = new()
        {
            FilePath = filePath,
            Text = text
        };

        IReadOnlyList<XamlVisualEditor.Core.LanguageDiagnostic> diagnostics =
            await service.GetDiagnosticsAsync(context);

        Assert.Contains(diagnostics, d => d.Severity == XamlVisualEditor.Core.DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task HoverReturnsSymbolInfo()
    {
        CSharpLanguageService service = new();
        string filePath = Path.Combine(Path.GetTempPath(), "Test.cs");
        string text = "using System; class C { void M() { Console.WriteLine(1); } }";
        int offset = text.IndexOf("Console", StringComparison.Ordinal) + 1;

        LanguagePositionContext context = new()
        {
            FilePath = filePath,
            Text = text,
            Offset = offset
        };

        XamlVisualEditor.Core.LanguageHover? hover = await service.GetHoverAsync(context);

        Assert.NotNull(hover);
        Assert.Contains("Console", hover!.Contents, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompletionReturnsItems()
    {
        CSharpLanguageService service = new();
        string filePath = Path.Combine(Path.GetTempPath(), "Test.cs");
        string text = "using System; class C { void M() { Console. } }";
        int offset = text.IndexOf("Console.", StringComparison.Ordinal) + "Console.".Length;

        CompletionContext context = new()
        {
            FilePath = filePath,
            DocumentText = text,
            TextBefore = text[..offset],
            Offset = offset,
            Trigger = XamlVisualEditor.Core.CompletionTrigger.CharacterTyped,
            TriggerCharacter = '.'
        };

        IReadOnlyList<XamlVisualEditor.Core.Interfaces.CompletionItem> items =
            await service.GetCompletionsAsync(context);

        Assert.NotEmpty(items);
    }
}
