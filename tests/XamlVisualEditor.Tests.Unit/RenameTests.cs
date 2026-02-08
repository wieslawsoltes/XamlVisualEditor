using System.Linq;
using XamlVisualEditor.CSharp.Language;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class RenameTests
{
    [Fact]
    public async Task CSharpRenameSingleFileUpdatesAllOccurrences()
    {
        CSharpLanguageService service = new();
        string filePath = Path.Combine(Path.GetTempPath(), "RenameSingle.cs");
        string text = "class C { void M() { int value = 1; value++; } }";
        int offset = text.IndexOf("value", StringComparison.Ordinal) + 1;

        LanguageRenameContext context = new()
        {
            FilePath = filePath,
            Text = text,
            Offset = offset,
            NewName = "count"
        };

        LanguageWorkspaceEdit? edit = await service.RenameSymbolAsync(context);
        Assert.NotNull(edit);
        Assert.NotEmpty(edit!.DocumentEdits);

        string updated = ApplyEdits(text, edit.DocumentEdits[0].Edits);
        Assert.Contains("count", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("value", updated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CSharpRenameUpdatesMultipleFiles()
    {
        CSharpLanguageService service = new();
        string fileA = Path.Combine(Path.GetTempPath(), "RenameA.cs");
        string fileB = Path.Combine(Path.GetTempPath(), "RenameB.cs");

        string textA = "public class C { public int Value; }";
        string textB = "class D { void M() { var c = new C(); c.Value = 3; } }";

        LanguageDocumentContext contextA = new()
        {
            FilePath = fileA,
            Text = textA
        };
        LanguageDocumentContext contextB = new()
        {
            FilePath = fileB,
            Text = textB
        };

        await service.GetDiagnosticsAsync(contextA);
        await service.GetDiagnosticsAsync(contextB);

        int offset = textA.IndexOf("Value", StringComparison.Ordinal) + 1;
        LanguageRenameContext renameContext = new()
        {
            FilePath = fileA,
            Text = textA,
            Offset = offset,
            NewName = "Amount"
        };

        LanguageWorkspaceEdit? edit = await service.RenameSymbolAsync(renameContext);
        Assert.NotNull(edit);
        Assert.Equal(2, edit!.DocumentEdits.Count);

        string updatedA = ApplyEdits(textA, edit.DocumentEdits.First(e => e.FilePath == fileA).Edits);
        string updatedB = ApplyEdits(textB, edit.DocumentEdits.First(e => e.FilePath == fileB).Edits);

        Assert.Contains("Amount", updatedA, StringComparison.Ordinal);
        Assert.Contains("Amount", updatedB, StringComparison.Ordinal);
        Assert.DoesNotContain("Value", updatedA, StringComparison.Ordinal);
        Assert.DoesNotContain("Value", updatedB, StringComparison.Ordinal);
    }

    [Fact]
    public void TextDocumentAppliesEditsInDescendingOrder()
    {
        TextDocumentViewModel doc = new("/tmp/RenameText.txt");
        doc.Document.Text = "abc123xyz";

        List<TextEdit> edits =
        [
            new TextEdit { Offset = 3, Length = 3, NewText = "DEF" },
            new TextEdit { Offset = 0, Length = 3, NewText = "AAA" }
        ];

        doc.ApplyTextEdits(edits);

        Assert.Equal("AAADEFxyz", doc.Document.Text);
    }

    private static string ApplyEdits(string text, IReadOnlyList<TextEdit> edits)
    {
        if (edits.Count == 0)
        {
            return text;
        }

        List<TextEdit> ordered = edits.OrderByDescending(e => e.Offset).ToList();
        foreach (TextEdit edit in ordered)
        {
            int offset = Math.Clamp(edit.Offset, 0, text.Length);
            int length = Math.Clamp(edit.Length, 0, text.Length - offset);
            text = text.Remove(offset, length).Insert(offset, edit.NewText ?? string.Empty);
        }

        return text;
    }
}
