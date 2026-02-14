using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Extensions.Hosting.Mcp;

/// <summary>Handles MCP prompt requests.</summary>
public sealed class McpPromptsHandler : IMcpRequestHandler
{
    private readonly IWorkspaceInfo _workspaceInfo;
    private readonly IEditorServices _editor;
    private readonly IDiagnosticsService _diagnostics;

    public McpPromptsHandler(IWorkspaceInfo workspaceInfo, IEditorServices editor, IDiagnosticsService diagnostics)
    {
        _workspaceInfo = workspaceInfo ?? throw new ArgumentNullException(nameof(workspaceInfo));
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public void Register(McpRequestRouter router)
    {
        router.Register(McpProtocol.PromptsListMethod, (context, _, ct) => HandleListAsync(context, ct));
        router.Register(McpProtocol.PromptsGetMethod, (context, paramsElement, ct) => HandleGetAsync(context, paramsElement, ct));
    }

    private Task<object?> HandleListAsync(McpRequestContext context, CancellationToken ct)
    {
        RequireSession(context);
        McpPrompt prompt = new(
            "ide-context",
            "Summarize workspace, open documents, and diagnostics.",
            new[]
            {
                new McpPromptArgument("includeDiagnostics", "Include diagnostics summary.", false, "true")
            });

        return Task.FromResult<object?>(new McpPromptsListResult(new[] { prompt }));
    }

    private async Task<object?> HandleGetAsync(McpRequestContext context, JsonElement? parameters, CancellationToken ct)
    {
        RequireSession(context);
        McpPromptGetParams request = Deserialize<McpPromptGetParams>(parameters);
        if (!string.Equals(request.Name, "ide-context", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpJsonRpcException(-32601, "Prompt not found.");
        }

        bool includeDiagnostics = true;
        if (request.Arguments is JsonElement argsElement && argsElement.ValueKind == JsonValueKind.Object
            && argsElement.TryGetProperty("includeDiagnostics", out JsonElement includeElement))
        {
            includeDiagnostics = includeElement.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => !string.Equals(includeElement.GetString(), "false", StringComparison.OrdinalIgnoreCase),
                _ => includeDiagnostics
            };
        }

        string text = await BuildContextAsync(includeDiagnostics, ct).ConfigureAwait(false);
        McpPromptMessage message = new("system", new[] { new McpContent("text", Text: text) });
        return new McpPromptsGetResult(new[] { message });
    }

    private async Task<string> BuildContextAsync(bool includeDiagnostics, CancellationToken ct)
    {
        StringBuilder builder = new();
        builder.AppendLine("Workspace: " + (_workspaceInfo.WorkspacePath ?? "(none)"));
        builder.AppendLine("Active document: " + (_editor.ActiveDocument?.FilePath ?? "(none)"));

        IReadOnlyList<IEditorDocument> openDocs = _editor.GetOpenDocuments();
        builder.AppendLine("Open documents:");
        foreach (IEditorDocument doc in openDocs)
        {
            builder.AppendLine("- " + doc.FilePath);
        }

        if (includeDiagnostics)
        {
            IReadOnlyList<LanguageDiagnostic> diagnostics = await _diagnostics
                .GetDiagnosticsAsync(new DiagnosticsQuery(null, null), ct)
                .ConfigureAwait(false);
            builder.AppendLine("Diagnostics: " + diagnostics.Count);
            foreach (LanguageDiagnostic diag in diagnostics.Take(20))
            {
                builder.AppendLine($"- {diag.Message} ({diag.Severity})");
            }
        }

        return builder.ToString();
    }

    private static void RequireSession(McpRequestContext context)
    {
        if (context.Session is null)
        {
            throw new McpJsonRpcException(-32000, "Session not initialized.");
        }
    }

    private static T Deserialize<T>(JsonElement? args)
    {
        if (args is null)
        {
            throw new McpJsonRpcException(-32602, "Invalid params.");
        }

        return JsonSerializer.Deserialize<T>(args.Value.GetRawText(), McpMessageFraming.SerializerOptions)
            ?? throw new McpJsonRpcException(-32602, "Invalid params.");
    }
}
