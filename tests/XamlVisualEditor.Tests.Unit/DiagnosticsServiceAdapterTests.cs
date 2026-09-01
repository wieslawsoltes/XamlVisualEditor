using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using XamlVisualEditor.Core;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class DiagnosticsServiceAdapterTests
{
    [AvaloniaFact]
    public async Task GetDiagnosticsAsync_MapsDesignerDocumentDiagnostics()
    {
        const string filePath = @"C:\work\Sample.axaml";
        using MainWindowViewModel viewModel = new();
        using DesignerDocumentViewModel designer = new(filePath);
        designer.CodeEditor.Diagnostics.Add(new XamlDiagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Message = "Broken element",
            Line = 4,
            Column = 3,
            Length = 2
        });
        viewModel.Documents.Add(designer);

        using DiagnosticsServiceAdapter adapter = new(viewModel);
        IReadOnlyList<LanguageDiagnostic> diagnostics =
            await adapter.GetDiagnosticsAsync(filePath, CancellationToken.None);

        LanguageDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Broken element", diagnostic.Message);
        Assert.Equal(filePath, diagnostic.FilePath);
        Assert.Equal(4, diagnostic.Range.Start.Line);
        Assert.Equal(3, diagnostic.Range.Start.Column);
        Assert.Equal(5, diagnostic.Range.End.Column);
        Assert.Equal("XAML", diagnostic.Source);
    }

    [AvaloniaFact]
    public async Task GetDiagnosticsAsync_FindsOpenTextDocumentByPath()
    {
        const string filePath = @"C:\work\Sample.xml";
        using MainWindowViewModel viewModel = new();
        using TextDocumentViewModel text = new(filePath);
        text.Diagnostics.Add(new LanguageDiagnostic
        {
            Severity = DiagnosticSeverity.Warning,
            Message = "Suspicious attribute",
            FilePath = filePath,
            Range = new LanguageTextRange(new LanguageTextPosition(1, 2), new LanguageTextPosition(1, 5))
        });
        viewModel.Documents.Add(text);

        using DiagnosticsServiceAdapter adapter = new(viewModel);
        IReadOnlyList<LanguageDiagnostic> diagnostics =
            await adapter.GetDiagnosticsAsync(filePath, CancellationToken.None);

        LanguageDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("Suspicious attribute", diagnostic.Message);
    }
}
