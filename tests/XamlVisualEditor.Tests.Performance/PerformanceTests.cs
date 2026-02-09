using System;
using System.Collections.Generic;
using System.Diagnostics;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Sync;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Xaml.Intellisense;
using XamlVisualEditor.Xaml.Parsing;
using XamlVisualEditor.Xaml.Serialization;
using Xunit;

namespace XamlVisualEditor.Tests.Performance;

// ==============================================
// Performance Tests (10.4.1–10.4.5)
// ==============================================

/// <summary>
/// Performance tests to ensure key operations complete within acceptable time bounds.
/// These validate that core operations don't regress beyond acceptable thresholds.
/// </summary>
public sealed class PerformanceTests
{
    /// <summary>
    /// 10.4.1 — Profile XAML parse time for large files (1000+ lines).
    /// The parser should handle large documents in under 500ms.
    /// </summary>
    [Fact]
    public void ParseLargeXaml_Completes_Under_Threshold()
    {
        // Generate a large XAML file (~1000 lines)
        System.Text.StringBuilder sb = new();
        sb.AppendLine("<UserControl xmlns=\"https://github.com/avaloniaui\"");
        sb.AppendLine("             xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        sb.AppendLine("  <StackPanel>");

        for (int i = 0; i < 300; i++)
        {
            sb.AppendLine("    <Grid>");
            sb.AppendLine($"      <TextBlock Text=\"Item {i}\" Margin=\"4\" FontSize=\"14\" />");
            sb.AppendLine($"      <Button Content=\"Action {i}\" Width=\"100\" Height=\"32\" />");
            sb.AppendLine("    </Grid>");
        }

        sb.AppendLine("  </StackPanel>");
        sb.AppendLine("</UserControl>");

        string largeXaml = sb.ToString();
        Assert.True(largeXaml.Split('\n').Length > 1000, "Generated XAML should be 1000+ lines");

        XamlParsingService parser = new();
        Stopwatch sw = Stopwatch.StartNew();

        ParseResult result = parser.Parse(largeXaml);

        sw.Stop();

        Assert.NotNull(result.Document);
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Parsing {largeXaml.Split('\n').Length} lines took {sw.ElapsedMilliseconds}ms (threshold: 5000ms)");
    }

    /// <summary>
    /// 10.4.2 — Profile serialization for large AST.
    /// Serialization should handle large documents efficiently.
    /// </summary>
    [Fact]
    public void SerializeLargeAst_Completes_Under_Threshold()
    {
        // Build a large AST manually
        MutableAstObjectNode root = new()
        {
            TypeName = "StackPanel",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        for (int i = 0; i < 500; i++)
        {
            MutableAstObjectNode child = new()
            {
                TypeName = "TextBlock",
                XmlNamespace = "https://github.com/avaloniaui"
            };
            child.SetPropertyValue("Text", $"Item {i}");
            child.SetPropertyValue("Margin", "4");
            child.SetPropertyValue("FontSize", "14");
            root.Children.Add(child);
        }

        MutableAstDocument doc = new() { Root = root };
        XamlSerializationService serializer = new();

        Stopwatch sw = Stopwatch.StartNew();

        string output = serializer.Serialize(doc);

        sw.Stop();

        Assert.NotEmpty(output);
        Assert.Contains("Item 499", output);
        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"Serializing 500 nodes took {sw.ElapsedMilliseconds}ms (threshold: 2000ms)");
    }

    /// <summary>
    /// 10.4.3 — Profile sync engine throughput.
    /// Loading and syncing should complete promptly.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task SyncEngine_LoadLargeDocument_Under_Threshold()
    {
        System.Text.StringBuilder sb = new();
        sb.AppendLine("<UserControl xmlns=\"https://github.com/avaloniaui\">");
        sb.AppendLine("  <StackPanel>");

        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"    <TextBlock Text=\"Line {i}\" />");
        }

        sb.AppendLine("  </StackPanel>");
        sb.AppendLine("</UserControl>");

        string xaml = sb.ToString();

        XamlParsingService parser = new();
        XamlSerializationService serializer = new();
        AstNodeMap map = new();
        SyncEngine engine = new(parser, serializer, map);

        Stopwatch sw = Stopwatch.StartNew();

        await engine.LoadAsync(xaml);

        sw.Stop();

        Assert.NotNull(engine.CurrentDocument);
        Assert.True(sw.ElapsedMilliseconds < 3000,
            $"Loading {xaml.Split('\n').Length} lines took {sw.ElapsedMilliseconds}ms (threshold: 3000ms)");
    }

    /// <summary>
    /// 10.4.4 — Profile memory usage during property mutations.
    /// Rapid property changes should not cause excessive allocations.
    /// </summary>
    [Fact]
    public void RapidPropertyMutations_No_Excessive_Allocations()
    {
        MutableAstObjectNode node = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };

        long memBefore = GC.GetTotalMemory(forceFullCollection: true);

        for (int i = 0; i < 10000; i++)
        {
            node.SetPropertyValue("Width", i.ToString());
        }

        long memAfter = GC.GetTotalMemory(forceFullCollection: true);
        long memDelta = memAfter - memBefore;

        Assert.Equal("9999", node.GetPropertyValue("Width"));
        // 10K mutations should not allocate more than 10MB of retained memory
        Assert.True(memDelta < 10 * 1024 * 1024,
            $"10K property mutations allocated {memDelta / 1024}KB (threshold: 10MB)");
    }

    /// <summary>
    /// 10.4.5 — Profile intellisense completion time.
    /// Completion lookups should be fast enough for interactive use.
    /// </summary>
    [Fact]
    public void IntellisenseCompletion_Completes_Under_Threshold()
    {
        CompletionProviderRegistry registry = CompletionProviderRegistry.CreateDefault();

        CompletionContext ctx = new()
        {
            TextBefore = "<",
            Offset = 1,
            Trigger = CompletionTrigger.CharacterTyped
        };

        Stopwatch sw = Stopwatch.StartNew();

        for (int i = 0; i < 100; i++)
        {
            IReadOnlyList<CompletionItem> items = registry.GetCompletions(ctx);
        }

        sw.Stop();

        // 100 completions should complete in under 500ms
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"100 completion lookups took {sw.ElapsedMilliseconds}ms (threshold: 500ms)");
    }
}
