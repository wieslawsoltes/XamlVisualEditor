using System.Diagnostics;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;

namespace XamlVisualEditor.CSharp.Language;

/// <summary>
/// Roslyn-based C# language service.
/// </summary>
public sealed class CSharpLanguageService : ILanguageIntellisenseService, IDisposable
{
    private readonly CSharpWorkspaceManager _workspaceManager = new();

    public string LanguageId => "csharp";

    public bool CanHandle(string filePath, string? languageId)
    {
        if (string.Equals(languageId, "csharp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }

    public Task InitializeWorkspaceAsync(string workspacePath, CancellationToken ct = default)
    {
        return _workspaceManager.InitializeWorkspaceAsync(workspacePath, ct);
    }

    public Task ClearWorkspaceAsync(CancellationToken ct = default)
    {
        return _workspaceManager.ClearWorkspaceAsync(ct);
    }

    public async Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken ct = default)
    {
        if (context.DocumentText is null || context.FilePath is null)
        {
            return Array.Empty<CompletionItem>();
        }

        Document? document = await _workspaceManager
            .GetOrAddDocumentAsync(context.FilePath, context.DocumentText, ct)
            .ConfigureAwait(false);
        if (document is null)
        {
            return Array.Empty<CompletionItem>();
        }

        Microsoft.CodeAnalysis.Completion.CompletionService? service =
            Microsoft.CodeAnalysis.Completion.CompletionService.GetService(document);
        if (service is null)
        {
            return Array.Empty<CompletionItem>();
        }

        Microsoft.CodeAnalysis.Completion.CompletionTrigger trigger =
            context.Trigger == CompletionTrigger.CharacterTyped && context.TriggerCharacter.HasValue
                ? Microsoft.CodeAnalysis.Completion.CompletionTrigger.CreateInsertionTrigger(context.TriggerCharacter.Value)
                : Microsoft.CodeAnalysis.Completion.CompletionTrigger.Invoke;

        int position = Math.Clamp(context.Offset, 0, context.DocumentText.Length);
        Microsoft.CodeAnalysis.Completion.CompletionList? list = await service.GetCompletionsAsync(
            document,
            position,
                options: null,
                trigger: trigger,
                cancellationToken: ct)
            .ConfigureAwait(false);

        if (list is null || list.ItemsList.Count == 0)
        {
            return Array.Empty<CompletionItem>();
        }

        List<CompletionItem> results = new(list.ItemsList.Count);
        foreach (Microsoft.CodeAnalysis.Completion.CompletionItem item in list.ItemsList)
        {
            Microsoft.CodeAnalysis.Completion.CompletionChange change =
                await service.GetChangeAsync(document, item, cancellationToken: ct).ConfigureAwait(false);
            string insertText = change.TextChange.NewText ?? item.DisplayText;

            results.Add(new CompletionItem
            {
                DisplayText = item.DisplayText,
                InsertText = insertText,
                Description = item.InlineDescription,
                Documentation = item.FilterText,
                SortText = item.SortText,
                FilterText = item.FilterText,
                IsSnippet = item.Tags.Contains(WellKnownTags.Snippet),
                Kind = MapKind(item.Tags),
                Priority = 0,
                TextEdit = new TextEdit
                {
                    Offset = change.TextChange.Span.Start,
                    Length = change.TextChange.Span.Length,
                    NewText = insertText
                }
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(
        LanguageDocumentContext context,
        CancellationToken ct = default)
    {
        Document? document = await _workspaceManager
            .GetOrAddDocumentAsync(context.FilePath, context.Text, ct)
            .ConfigureAwait(false);
        if (document is null)
        {
            return Array.Empty<LanguageDiagnostic>();
        }

        Compilation? compilation = await document.Project.GetCompilationAsync(ct).ConfigureAwait(false);
        if (compilation is null)
        {
            return Array.Empty<LanguageDiagnostic>();
        }

        SyntaxTree? tree = await document.GetSyntaxTreeAsync(ct).ConfigureAwait(false);
        if (tree is null)
        {
            return Array.Empty<LanguageDiagnostic>();
        }

        List<LanguageDiagnostic> diagnostics = new();
        foreach (Diagnostic diag in compilation.GetDiagnostics(ct))
        {
            if (!diag.Location.IsInSource || diag.Location.SourceTree != tree)
            {
                continue;
            }

            FileLinePositionSpan span = diag.Location.GetLineSpan();
            LanguageTextPosition start = new(span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1);
            LanguageTextPosition end = new(span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1);

            diagnostics.Add(new LanguageDiagnostic
            {
                FilePath = context.FilePath,
                Message = diag.GetMessage(),
                Severity = MapSeverity(diag.Severity),
                Range = new LanguageTextRange(start, end),
                Code = diag.Id,
                Source = diag.Descriptor.Category
            });
        }

        return diagnostics;
    }

    public async Task<LanguageHover?> GetHoverAsync(LanguagePositionContext context, CancellationToken ct = default)
    {
        try
        {
            Document? document = await _workspaceManager
                .GetOrAddDocumentAsync(context.FilePath, context.Text, ct)
                .ConfigureAwait(false);
            if (document is null)
            {
                return null;
            }

            ISymbol? symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, context.Offset, ct)
                .ConfigureAwait(false);
            if (symbol is null)
            {
                return null;
            }

            string display = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            return new LanguageHover
            {
                Contents = display
            };
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<LanguageLocation>> FindDefinitionsAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        Document? document = await _workspaceManager
            .GetOrAddDocumentAsync(context.FilePath, context.Text, ct)
            .ConfigureAwait(false);
        if (document is null)
        {
            return Array.Empty<LanguageLocation>();
        }

        ISymbol? symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, context.Offset, ct)
            .ConfigureAwait(false);
        if (symbol is null)
        {
            return Array.Empty<LanguageLocation>();
        }

        ISymbol? definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, document.Project.Solution, ct)
            .ConfigureAwait(false);
        symbol = definition ?? symbol;

        List<LanguageLocation> locations = new();
        foreach (Location location in symbol.Locations)
        {
            LanguageLocation? mapped = MapLocation(location);
            if (mapped is not null)
            {
                locations.Add(mapped);
            }
        }

        return locations;
    }

    public async Task<IReadOnlyList<LanguageLocation>> FindReferencesAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        Document? document = await _workspaceManager
            .GetOrAddDocumentAsync(context.FilePath, context.Text, ct)
            .ConfigureAwait(false);
        if (document is null)
        {
            return Array.Empty<LanguageLocation>();
        }

        ISymbol? symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, context.Offset, ct)
            .ConfigureAwait(false);
        if (symbol is null)
        {
            return Array.Empty<LanguageLocation>();
        }

        List<LanguageLocation> results = new();
        IEnumerable<ReferencedSymbol> references = await SymbolFinder.FindReferencesAsync(
                symbol,
                document.Project.Solution,
                cancellationToken: ct)
            .ConfigureAwait(false);

        foreach (ReferencedSymbol reference in references)
        {
            foreach (ReferenceLocation location in reference.Locations)
            {
                LanguageLocation? mapped = MapLocation(location.Location);
                if (mapped is not null)
                {
                    results.Add(mapped);
                }
            }
        }

        return results;
    }

    public async Task<LanguageRenameInfo?> PrepareRenameAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        Document? document = await _workspaceManager
            .GetOrAddDocumentAsync(context.FilePath, context.Text, ct)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        ISymbol? symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, context.Offset, ct)
            .ConfigureAwait(false);
        if (symbol is null || symbol.IsImplicitlyDeclared)
        {
            return null;
        }

        Location? location = symbol.Locations.FirstOrDefault(loc =>
            loc.IsInSource &&
            string.Equals(loc.SourceTree?.FilePath, context.FilePath, StringComparison.OrdinalIgnoreCase));
        if (location is null)
        {
            return null;
        }

        LanguageTextRange? range = MapRange(location);
        if (range is null)
        {
            return null;
        }

        return new LanguageRenameInfo
        {
            Name = symbol.Name,
            Range = range.Value
        };
    }

    public async Task<LanguageWorkspaceEdit?> RenameSymbolAsync(
        LanguageRenameContext context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context.NewName))
        {
            return null;
        }

        Document? document = await _workspaceManager
            .GetOrAddDocumentAsync(context.FilePath, context.Text, ct)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        ISymbol? symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, context.Offset, ct)
            .ConfigureAwait(false);
        if (symbol is null || symbol.IsImplicitlyDeclared)
        {
            return null;
        }

        Solution solution = document.Project.Solution;
        SymbolRenameOptions options = new();
        Solution renamedSolution = await Renamer.RenameSymbolAsync(
                solution,
                symbol,
                options,
                context.NewName,
                ct)
            .ConfigureAwait(false);

        SolutionChanges changes = renamedSolution.GetChanges(solution);
        List<LanguageDocumentEdit> documentEdits = new();

        foreach (ProjectChanges projectChange in changes.GetProjectChanges())
        {
            foreach (DocumentId docId in projectChange.GetChangedDocuments())
            {
                Document? oldDoc = solution.GetDocument(docId);
                Document? newDoc = renamedSolution.GetDocument(docId);
                if (oldDoc is null || newDoc is null || string.IsNullOrWhiteSpace(oldDoc.FilePath))
                {
                    continue;
                }

                IReadOnlyList<TextChange> textChanges =
                    (await newDoc.GetTextChangesAsync(oldDoc, ct).ConfigureAwait(false)).ToList();
                if (textChanges.Count == 0)
                {
                    continue;
                }

                List<TextEdit> edits = new(textChanges.Count);
                foreach (TextChange change in textChanges)
                {
                    edits.Add(new TextEdit
                    {
                        Offset = change.Span.Start,
                        Length = change.Span.Length,
                        NewText = change.NewText ?? string.Empty
                    });
                }

                documentEdits.Add(new LanguageDocumentEdit
                {
                    FilePath = oldDoc.FilePath,
                    Edits = edits
                });
            }
        }

        if (documentEdits.Count == 0)
        {
            return null;
        }

        _workspaceManager.UpdateSolution(renamedSolution);

        return new LanguageWorkspaceEdit
        {
            DocumentEdits = documentEdits
        };
    }

    public Task<LanguageSignatureHelp?> GetSignatureHelpAsync(
        LanguagePositionContext context,
        CancellationToken ct = default)
    {
        return GetSignatureHelpCoreAsync(context, ct);
    }

    private async Task<LanguageSignatureHelp?> GetSignatureHelpCoreAsync(
        LanguagePositionContext context,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.FilePath) || context.Text is null)
        {
            return null;
        }

        Document? document = await _workspaceManager
            .GetOrAddDocumentAsync(context.FilePath, context.Text, ct)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        SyntaxTree? syntaxTree = await document.GetSyntaxTreeAsync(ct).ConfigureAwait(false);
        if (syntaxTree is null)
        {
            return null;
        }

        SyntaxNode root = await syntaxTree.GetRootAsync(ct).ConfigureAwait(false);
        int position = Math.Clamp(context.Offset, 0, context.Text.Length);
        SyntaxToken token = root.FindToken(position);
        BaseArgumentListSyntax? argumentList = token.Parent?
            .AncestorsAndSelf()
            .OfType<BaseArgumentListSyntax>()
            .FirstOrDefault(al => al.Span.Contains(position));
        if (argumentList is null)
        {
            return null;
        }

        SyntaxNode? invocationNode = argumentList.Parent;
        if (invocationNode is null)
        {
            return null;
        }

        SemanticModel? semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return null;
        }

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocationNode, ct);
        IReadOnlyList<IMethodSymbol> candidates = GetCandidateMethods(symbolInfo);
        if (candidates.Count == 0)
        {
            return null;
        }

        int activeParameter = GetActiveParameterIndex(argumentList, position);
        int activeSignature = GetActiveSignatureIndex(symbolInfo, candidates);

        List<LanguageSignature> signatures = new(candidates.Count);
        foreach (IMethodSymbol method in candidates)
        {
            signatures.Add(new LanguageSignature
            {
                Label = BuildSignatureLabel(method),
                Documentation = GetDocumentation(method),
                Parameters = method.Parameters.Select(BuildParameter).ToList()
            });
        }

        if (activeSignature < 0 || activeSignature >= signatures.Count)
        {
            activeSignature = 0;
        }

        if (activeParameter < 0)
        {
            activeParameter = 0;
        }

        return new LanguageSignatureHelp
        {
            Signatures = signatures,
            ActiveSignature = activeSignature,
            ActiveParameter = activeParameter
        };
    }

    private static IReadOnlyList<IMethodSymbol> GetCandidateMethods(SymbolInfo symbolInfo)
    {
        List<IMethodSymbol> methods = new();

        if (symbolInfo.Symbol is IMethodSymbol method)
        {
            methods.Add(method);
        }

        foreach (ISymbol candidate in symbolInfo.CandidateSymbols)
        {
            if (candidate is IMethodSymbol candidateMethod && !methods.Contains(candidateMethod))
            {
                methods.Add(candidateMethod);
            }
        }

        return methods;
    }

    private static int GetActiveSignatureIndex(SymbolInfo symbolInfo, IReadOnlyList<IMethodSymbol> candidates)
    {
        if (symbolInfo.Symbol is IMethodSymbol method)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (SymbolEqualityComparer.Default.Equals(candidates[i], method))
                {
                    return i;
                }
            }
        }

        return 0;
    }

    private static int GetActiveParameterIndex(BaseArgumentListSyntax argumentList, int position)
    {
        int index = 0;
        foreach (ArgumentSyntax arg in argumentList.Arguments)
        {
            if (position > arg.Span.End)
            {
                index++;
                continue;
            }

            break;
        }

        return index;
    }

    private static LanguageParameter BuildParameter(IParameterSymbol parameter)
    {
        return new LanguageParameter
        {
            Label = BuildParameterLabel(parameter),
            Documentation = GetDocumentation(parameter)
        };
    }

    private static string BuildSignatureLabel(IMethodSymbol method)
    {
        string name = method.MethodKind == MethodKind.Constructor
            ? method.ContainingType.Name
            : method.Name;

        string typeParameters = method.TypeParameters.Length > 0
            ? $"<{string.Join(", ", method.TypeParameters.Select(tp => tp.Name))}>"
            : string.Empty;

        string parameters = string.Join(", ", method.Parameters.Select(BuildParameterLabel));

        if (method.MethodKind == MethodKind.Constructor)
        {
            return $"{name}{typeParameters}({parameters})";
        }

        string returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        return $"{returnType} {name}{typeParameters}({parameters})";
    }

    private static string BuildParameterLabel(IParameterSymbol parameter)
    {
        string modifier = parameter.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            _ => string.Empty
        };

        if (parameter.IsParams)
        {
            modifier = "params " + modifier;
        }

        string type = parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        string label = $"{modifier}{type} {parameter.Name}";

        if (parameter.HasExplicitDefaultValue)
        {
            label += " = " + FormatDefaultValue(parameter.ExplicitDefaultValue);
        }

        return label;
    }

    private static string FormatDefaultValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is string str)
        {
            return "\"" + str.Replace("\"", "\\\"") + "\"";
        }

        if (value is char ch)
        {
            return "'" + ch.ToString(CultureInfo.InvariantCulture) + "'";
        }

        if (value is bool boolean)
        {
            return boolean ? "true" : "false";
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? string.Empty;
    }

    private static string? GetDocumentation(ISymbol symbol)
    {
        string? xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        return xml;
    }

    private static CompletionItemKind MapKind(IReadOnlyList<string> tags)
    {
        if (tags.Contains(WellKnownTags.Class)) return CompletionItemKind.Class;
        if (tags.Contains(WellKnownTags.Structure)) return CompletionItemKind.Struct;
        if (tags.Contains(WellKnownTags.Interface)) return CompletionItemKind.Interface;
        if (tags.Contains(WellKnownTags.Enum)) return CompletionItemKind.Enum;
        if (tags.Contains(WellKnownTags.Delegate)) return CompletionItemKind.Delegate;
        if (tags.Contains(WellKnownTags.Namespace)) return CompletionItemKind.NamespaceSymbol;
        if (tags.Contains(WellKnownTags.Method)) return CompletionItemKind.Method;
        if (tags.Contains(WellKnownTags.Property)) return CompletionItemKind.PropertySymbol;
        if (tags.Contains(WellKnownTags.Field)) return CompletionItemKind.Field;
        if (tags.Contains(WellKnownTags.Event)) return CompletionItemKind.Event;
        if (tags.Contains(WellKnownTags.Parameter)) return CompletionItemKind.Parameter;
        if (tags.Contains(WellKnownTags.Local)) return CompletionItemKind.Variable;
        if (tags.Contains(WellKnownTags.Keyword)) return CompletionItemKind.Keyword;
        if (tags.Contains(WellKnownTags.Snippet)) return CompletionItemKind.Snippet;

        return CompletionItemKind.Value;
    }

    private static XamlVisualEditor.Core.DiagnosticSeverity MapSeverity(Microsoft.CodeAnalysis.DiagnosticSeverity severity)
    {
        return severity switch
        {
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error => XamlVisualEditor.Core.DiagnosticSeverity.Error,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => XamlVisualEditor.Core.DiagnosticSeverity.Warning,
            _ => XamlVisualEditor.Core.DiagnosticSeverity.Info
        };
    }

    private static LanguageLocation? MapLocation(Location location)
    {
        if (!location.IsInSource)
        {
            return null;
        }

        FileLinePositionSpan span = location.GetLineSpan();
        if (string.IsNullOrWhiteSpace(span.Path))
        {
            return null;
        }

        LanguageTextPosition start = new(span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1);
        LanguageTextPosition end = new(span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1);
        return new LanguageLocation
        {
            FilePath = span.Path,
            Range = new LanguageTextRange(start, end)
        };
    }

    private static LanguageTextRange? MapRange(Location location)
    {
        if (!location.IsInSource)
        {
            return null;
        }

        FileLinePositionSpan span = location.GetLineSpan();
        if (string.IsNullOrWhiteSpace(span.Path))
        {
            return null;
        }

        LanguageTextPosition start = new(span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1);
        LanguageTextPosition end = new(span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1);
        return new LanguageTextRange(start, end);
    }

    public void Dispose()
    {
        _workspaceManager.Dispose();
    }
}
