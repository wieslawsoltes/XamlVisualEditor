using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace XamlVisualEditor.CSharp.Language;

/// <summary>
/// Manages Roslyn workspaces and documents for C# language services.
/// </summary>
public sealed class CSharpWorkspaceManager : IDisposable
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, DocumentId> _documentIds =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<CSharpWorkspaceManager> _logger;
    private MSBuildWorkspace? _msbuildWorkspace;
    private AdhocWorkspace? _adhocWorkspace;
    private Workspace? _workspace;
    private Solution? _solution;
    private string? _workspacePath;
    private Task? _workspaceLoadTask;
    private CancellationTokenSource? _workspaceLoadCancellation;
    private int _workspaceGeneration;

    public CSharpWorkspaceManager(ILogger<CSharpWorkspaceManager>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CSharpWorkspaceManager>.Instance;
    }

    public Task InitializeWorkspaceAsync(string workspacePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return Task.CompletedTask;
        }

        MSBuildWorkspace? oldMsbuildWorkspace;
        AdhocWorkspace? oldAdhocWorkspace;
        CancellationTokenSource? oldLoadCancellation;
        lock (_gate)
        {
            if (string.Equals(_workspacePath, workspacePath, StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            oldMsbuildWorkspace = _msbuildWorkspace;
            oldAdhocWorkspace = _adhocWorkspace;
            oldLoadCancellation = _workspaceLoadCancellation;
            _msbuildWorkspace = null;
            _adhocWorkspace = null;
            _workspace = null;
            _solution = null;
            _workspacePath = workspacePath;
            _workspaceLoadTask = null;
            _workspaceLoadCancellation = null;
            _workspaceGeneration++;
            _documentIds.Clear();
        }

        oldLoadCancellation?.Cancel();
        oldLoadCancellation?.Dispose();
        oldMsbuildWorkspace?.Dispose();
        oldAdhocWorkspace?.Dispose();
        return Task.CompletedTask;
    }

    public Task ClearWorkspaceAsync(CancellationToken ct)
    {
        MSBuildWorkspace? oldMsbuildWorkspace;
        AdhocWorkspace? oldAdhocWorkspace;
        CancellationTokenSource? oldLoadCancellation;
        lock (_gate)
        {
            oldMsbuildWorkspace = _msbuildWorkspace;
            oldAdhocWorkspace = _adhocWorkspace;
            oldLoadCancellation = _workspaceLoadCancellation;
            _msbuildWorkspace = null;
            _adhocWorkspace = null;
            _workspacePath = null;
            _solution = null;
            _workspace = null;
            _workspaceLoadTask = null;
            _workspaceLoadCancellation = null;
            _workspaceGeneration++;
            _documentIds.Clear();
        }

        oldLoadCancellation?.Cancel();
        oldLoadCancellation?.Dispose();
        oldMsbuildWorkspace?.Dispose();
        oldAdhocWorkspace?.Dispose();

        return Task.CompletedTask;
    }

    public async Task<Document?> GetOrAddDocumentAsync(string filePath, string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        await EnsureMsbuildWorkspaceAsync(ct).ConfigureAwait(false);

        Solution? solution = _solution;
        if (solution is not null)
        {
            Document? existing = FindDocument(solution, filePath);
            if (existing is not null)
            {
                return await UpdateDocumentTextAsync(existing, text, ct).ConfigureAwait(false);
            }
        }

        EnsureAdhocWorkspace();
        return await AddOrUpdateAdhocDocumentAsync(filePath, text, ct).ConfigureAwait(false);
    }

    private async Task EnsureMsbuildWorkspaceAsync(CancellationToken ct)
    {
        Task loadTask;
        lock (_gate)
        {
            if (_solution is not null || string.IsNullOrWhiteSpace(_workspacePath))
            {
                return;
            }

            if (_workspaceLoadTask is null)
            {
                _workspaceLoadCancellation = new CancellationTokenSource();
                _workspaceLoadTask = LoadMsbuildWorkspaceAsync(
                    _workspacePath,
                    _workspaceGeneration,
                    _workspaceLoadCancellation.Token);
            }

            loadTask = _workspaceLoadTask;
        }

        await loadTask.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task LoadMsbuildWorkspaceAsync(string workspacePath, int generation, CancellationToken ct)
    {
        MSBuildWorkspace? workspace = null;
        try
        {
            workspace = MSBuildWorkspace.Create();
            workspace.RegisterWorkspaceFailedHandler(diagnostic =>
            {
                if (diagnostic.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                {
                    _logger.LogWarning("MSBuild workspace: {Message}", diagnostic.Diagnostic.Message);
                }
            });

            Solution? solution = null;
            if (workspacePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                workspacePath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                solution = await workspace.OpenSolutionAsync(workspacePath, cancellationToken: ct)
                    .ConfigureAwait(false);
            }
            else if (workspacePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                Project project = await workspace.OpenProjectAsync(workspacePath, cancellationToken: ct)
                    .ConfigureAwait(false);
                solution = project.Solution;
            }

            if (solution is null)
            {
                return;
            }

            bool accepted;
            lock (_gate)
            {
                accepted = generation == _workspaceGeneration
                    && string.Equals(_workspacePath, workspacePath, StringComparison.OrdinalIgnoreCase);
                if (accepted)
                {
                    _msbuildWorkspace = workspace;
                    _workspace = workspace;
                    _solution = solution;
                    workspace = null;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to load MSBuild workspace: {Message}", ex.Message);
        }
        finally
        {
            workspace?.Dispose();
        }
    }

    private Document? FindDocument(Solution solution, string filePath)
    {
        if (_documentIds.TryGetValue(filePath, out DocumentId? cachedId))
        {
            Document? cachedDoc = solution.GetDocument(cachedId);
            if (cachedDoc is not null)
            {
                return cachedDoc;
            }
        }

        foreach (DocumentId docId in solution.GetDocumentIdsWithFilePath(filePath))
        {
            Document? doc = solution.GetDocument(docId);
            if (doc is not null)
            {
                _documentIds[filePath] = docId;
                return doc;
            }
        }

        return null;
    }

    private async Task<Document> UpdateDocumentTextAsync(Document document, string text, CancellationToken ct)
    {
        SourceText sourceText = SourceText.From(text);
        Solution newSolution = document.Project.Solution.WithDocumentText(
            document.Id,
            sourceText,
            PreservationMode.PreserveValue);

        ApplySolution(newSolution);
        Document updated = newSolution.GetDocument(document.Id) ?? document;
        await updated.GetTextAsync(ct).ConfigureAwait(false);
        return updated;
    }

    private void EnsureAdhocWorkspace()
    {
        if (_adhocWorkspace is not null)
        {
            return;
        }

        AdhocWorkspace workspace = new();
        ProjectId projectId = ProjectId.CreateNewId("AdhocCSharp");
        ProjectInfo projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "AdhocCSharp",
            "AdhocCSharp",
            LanguageNames.CSharp,
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences: GetDefaultReferences());

        workspace.AddProject(projectInfo);
        _adhocWorkspace = workspace;
        _workspace = workspace;
        _solution = workspace.CurrentSolution;
    }

    private async Task<Document?> AddOrUpdateAdhocDocumentAsync(string filePath, string text, CancellationToken ct)
    {
        if (_workspace is not AdhocWorkspace adhoc || _solution is null)
        {
            return null;
        }

        if (_documentIds.TryGetValue(filePath, out DocumentId? existingId))
        {
            Document? existing = _solution.GetDocument(existingId);
            if (existing is not null)
            {
                return await UpdateDocumentTextAsync(existing, text, ct).ConfigureAwait(false);
            }
        }

        Project? project = _solution.Projects.FirstOrDefault();
        if (project is null)
        {
            return null;
        }

        SourceText sourceText = SourceText.From(text);
        DocumentId docId = DocumentId.CreateNewId(project.Id);
        DocumentInfo docInfo = DocumentInfo.Create(
            docId,
            Path.GetFileName(filePath),
            filePath: filePath,
            loader: TextLoader.From(TextAndVersion.Create(sourceText, VersionStamp.Create())));

        Solution newSolution = _solution.AddDocument(docInfo);
        ApplySolution(newSolution);
        _documentIds[filePath] = docId;
        Document? document = newSolution.GetDocument(docId);
        if (document is not null)
        {
            await document.GetTextAsync(ct).ConfigureAwait(false);
        }

        return document;
    }

    private void ApplySolution(Solution solution)
    {
        _solution = solution;
        if (_workspace is not null)
        {
            _workspace.TryApplyChanges(solution);
        }
    }

    public void UpdateSolution(Solution solution)
    {
        ApplySolution(solution);
    }

    private static IReadOnlyList<MetadataReference> GetDefaultReferences()
    {
        List<MetadataReference> references = new();
        HashSet<string> locations = new(StringComparer.OrdinalIgnoreCase);

        AddReferenceIfValid(references, locations, typeof(object).Assembly);
        AddReferenceIfValid(references, locations, typeof(System.Console).Assembly);
        AddReferenceIfValid(references, locations, typeof(System.Linq.Enumerable).Assembly);
        AddReferenceIfValid(references, locations, typeof(System.Threading.Tasks.Task).Assembly);

        foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            AddReferenceIfValid(references, locations, asm);
        }

        return references;
    }

    private static void AddReferenceIfValid(
        ICollection<MetadataReference> references,
        ISet<string> locations,
        System.Reflection.Assembly assembly)
    {
        if (assembly.IsDynamic)
        {
            return;
        }

        string location = assembly.Location;
        if (string.IsNullOrWhiteSpace(location))
        {
            return;
        }

        if (locations.Add(location))
        {
            references.Add(MetadataReference.CreateFromFile(location));
        }
    }

    public void Dispose()
    {
        ClearWorkspaceAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
