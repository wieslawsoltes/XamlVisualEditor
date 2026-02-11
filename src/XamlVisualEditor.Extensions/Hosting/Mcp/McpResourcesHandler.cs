using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.Extensions.Hosting.Mcp;

/// <summary>Handles MCP resource requests.</summary>
public sealed class McpResourcesHandler : IMcpRequestHandler
{
    private readonly IWorkspace _workspace;
    private readonly IWorkspaceInfo _workspaceInfo;
    private readonly IEditorServices _editor;

    public McpResourcesHandler(IWorkspace workspace, IWorkspaceInfo workspaceInfo, IEditorServices editor)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _workspaceInfo = workspaceInfo ?? throw new ArgumentNullException(nameof(workspaceInfo));
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public void Register(McpRequestRouter router)
    {
        router.Register(McpProtocol.ResourcesListMethod, (context, _, ct) => HandleListAsync(context, ct));
        router.Register(McpProtocol.ResourcesReadMethod, (context, paramsElement, ct) => HandleReadAsync(context, paramsElement, ct));
    }

    private Task<object?> HandleListAsync(McpRequestContext context, CancellationToken ct)
    {
        RequireSession(context);
        List<McpResource> resources = new();

        string? workspacePath = _workspaceInfo.WorkspacePath;
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            resources.Add(new McpResource("xve://workspace", "Workspace", workspacePath, "text/plain"));
        }

        foreach (IEditorDocument doc in _editor.GetOpenDocuments())
        {
            string uri = BuildDocumentUri(doc.FilePath);
            resources.Add(new McpResource(uri, Path.GetFileName(doc.FilePath), doc.FilePath, "text/plain"));
        }

        return Task.FromResult<object?>(new McpResourcesListResult(resources));
    }

    private async Task<object?> HandleReadAsync(McpRequestContext context, JsonElement? parameters, CancellationToken ct)
    {
        RequireSession(context);
        McpResourceReadParams request = Deserialize<McpResourceReadParams>(parameters);
        if (!Uri.TryCreate(request.Uri, UriKind.Absolute, out Uri? uri))
        {
            throw new McpJsonRpcException(-32602, "Invalid resource uri.");
        }

        if (uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            string path = uri.LocalPath;
            byte[] content = await _workspace.ReadFileAsync(path, ct).ConfigureAwait(false);
            string text = Encoding.UTF8.GetString(content);
            return new McpResourcesReadResult(new[] { new McpResourceContent(request.Uri, "text/plain", text) });
        }

        if (uri.Scheme.Equals("xve", StringComparison.OrdinalIgnoreCase))
        {
            if (uri.Host.Equals("workspace", StringComparison.OrdinalIgnoreCase))
            {
                string? workspacePath = _workspaceInfo.WorkspacePath;
                return new McpResourcesReadResult(new[] { new McpResourceContent(request.Uri, "text/plain", workspacePath ?? string.Empty) });
            }

            if (uri.Host.Equals("document", StringComparison.OrdinalIgnoreCase))
            {
                string path = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
                IEditorDocument? doc = _editor.GetOpenDocuments()
                    .FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
                if (doc is null)
                {
                    throw new McpJsonRpcException(-32000, "Document is not open.");
                }

                string text = await doc.GetTextAsync(ct).ConfigureAwait(false);
                return new McpResourcesReadResult(new[] { new McpResourceContent(request.Uri, "text/plain", text) });
            }
        }

        throw new McpJsonRpcException(-32601, "Resource not found.");
    }

    private static string BuildDocumentUri(string filePath)
    {
        return "xve://document/" + Uri.EscapeDataString(filePath);
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
