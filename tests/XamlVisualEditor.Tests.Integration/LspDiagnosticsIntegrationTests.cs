using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Core.Lsp;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Lsp;
using Xunit;

namespace XamlVisualEditor.Tests.Integration;

public sealed class LspDiagnosticsIntegrationTests
{
    [Fact]
    public async Task LspServerPublishesDiagnostics()
    {
        string? serverPath = Environment.GetEnvironmentVariable("XVE_LSP_TEST_SERVER_PATH");
        if (string.IsNullOrWhiteSpace(serverPath))
        {
            return;
        }

        string[] args = SplitList(Environment.GetEnvironmentVariable("XVE_LSP_TEST_SERVER_ARGS"));
        string languageId = Environment.GetEnvironmentVariable("XVE_LSP_TEST_LANGUAGE_ID") ?? "csharp";
        string extension = Environment.GetEnvironmentVariable("XVE_LSP_TEST_FILE_EXT") ?? ".cs";
        string? workspacePath = Environment.GetEnvironmentVariable("XVE_LSP_TEST_WORKSPACE");
        string text = Environment.GetEnvironmentVariable("XVE_LSP_TEST_TEXT")
            ?? "class C { void M() { int x = ; } }";

        string rootPath = ResolveWorkspacePath(workspacePath);
        string filePath = Path.Combine(rootPath, "LspDiagnosticsTest" + NormalizeExtension(extension));
        Directory.CreateDirectory(rootPath);
        await File.WriteAllTextAsync(filePath, text);

        LspServerConfiguration config = new()
        {
            LanguageId = languageId,
            ServerPath = serverPath,
            Arguments = args,
            WorkingDirectory = rootPath,
            FileExtensions = new[] { NormalizeExtension(extension) }
        };

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
        LspLanguageServiceRouter router = new(new[] { config });
        ILanguageServiceSession? session = await router.GetSessionAsync(languageId, new LanguageWorkspaceInfo
        {
            RootPath = rootPath,
            Kind = WorkspaceKind.Folder
        }, cts.Token);

        if (session is null)
        {
            return;
        }

        await session.InitializeAsync(new LspInitializeParams
        {
            ProcessId = Environment.ProcessId,
            RootUri = new Uri(Path.GetFullPath(rootPath)).AbsoluteUri,
            ClientInfo = new LspClientInfo { Name = "XamlVisualEditor.Tests" },
            Capabilities = new { }
        }, cts.Token);

        await session.PublishDocumentAsync(new LspTextDocumentItem
        {
            Uri = new Uri(Path.GetFullPath(filePath)),
            LanguageId = languageId,
            Version = 1,
            Text = text
        }, cts.Token);

        IReadOnlyList<LspDiagnostic> diagnostics = await WaitForDiagnosticsAsync(session, new Uri(Path.GetFullPath(filePath)), cts.Token);

        await session.ShutdownAsync(cts.Token);

        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public async Task LspServerProvidesCompletionItems()
    {
        string? serverPath = Environment.GetEnvironmentVariable("XVE_LSP_TEST_SERVER_PATH");
        if (string.IsNullOrWhiteSpace(serverPath))
        {
            return;
        }

        if (!TryGetPosition("XVE_LSP_TEST_COMPLETION_LINE", "XVE_LSP_TEST_COMPLETION_COLUMN", out LspPosition position))
        {
            return;
        }

        (ILanguageServiceSession? session, Uri? documentUri, string rootPath, string languageId)
            = await StartSessionAsync(serverPath);
        if (session is null || documentUri is null)
        {
            return;
        }

        if (!session.Capabilities.Supports(LspFeature.Completion))
        {
            await session.ShutdownAsync(CancellationToken.None);
            return;
        }

        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
            IReadOnlyList<LspCompletionItem> items = await WaitForCompletionsAsync(session, documentUri, position, cts.Token);
            Assert.NotEmpty(items);
        }
        finally
        {
            await session.ShutdownAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task LspServerProvidesHover()
    {
        string? serverPath = Environment.GetEnvironmentVariable("XVE_LSP_TEST_SERVER_PATH");
        if (string.IsNullOrWhiteSpace(serverPath))
        {
            return;
        }

        if (!TryGetPosition("XVE_LSP_TEST_HOVER_LINE", "XVE_LSP_TEST_HOVER_COLUMN", out LspPosition position))
        {
            return;
        }

        (ILanguageServiceSession? session, Uri? documentUri, string rootPath, string languageId)
            = await StartSessionAsync(serverPath);
        if (session is null || documentUri is null)
        {
            return;
        }

        if (!session.Capabilities.Supports(LspFeature.Hover))
        {
            await session.ShutdownAsync(CancellationToken.None);
            return;
        }

        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
            LspHover? hover = await WaitForHoverAsync(session, documentUri, position, cts.Token);
            Assert.NotNull(hover);
            Assert.False(string.IsNullOrWhiteSpace(hover?.Contents));
        }
        finally
        {
            await session.ShutdownAsync(CancellationToken.None);
        }
    }

    private static async Task<IReadOnlyList<LspDiagnostic>> WaitForDiagnosticsAsync(
        ILanguageServiceSession session,
        Uri uri,
        CancellationToken ct)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            IReadOnlyList<LspDiagnostic> diagnostics = await session.GetDiagnosticsAsync(uri, ct);
            if (diagnostics.Count > 0)
            {
                return diagnostics;
            }

            await Task.Delay(250, ct);
        }

        return Array.Empty<LspDiagnostic>();
    }

    private static async Task<IReadOnlyList<LspCompletionItem>> WaitForCompletionsAsync(
        ILanguageServiceSession session,
        Uri uri,
        LspPosition position,
        CancellationToken ct)
    {
        LspCompletionParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = uri },
            Position = position
        };

        for (int attempt = 0; attempt < 20; attempt++)
        {
            IReadOnlyList<LspCompletionItem> items = await session.GetCompletionsAsync(parameters, ct);
            if (items.Count > 0)
            {
                return items;
            }

            await Task.Delay(250, ct);
        }

        return Array.Empty<LspCompletionItem>();
    }

    private static async Task<LspHover?> WaitForHoverAsync(
        ILanguageServiceSession session,
        Uri uri,
        LspPosition position,
        CancellationToken ct)
    {
        LspHoverParams parameters = new()
        {
            TextDocument = new LspTextDocumentIdentifier { Uri = uri },
            Position = position
        };

        for (int attempt = 0; attempt < 20; attempt++)
        {
            LspHover? hover = await session.GetHoverAsync(parameters, ct);
            if (hover is not null && !string.IsNullOrWhiteSpace(hover.Contents))
            {
                return hover;
            }

            await Task.Delay(250, ct);
        }

        return null;
    }

    private static async Task<(ILanguageServiceSession? Session, Uri? DocumentUri, string RootPath, string LanguageId)>
        StartSessionAsync(string serverPath)
    {
        string[] args = SplitList(Environment.GetEnvironmentVariable("XVE_LSP_TEST_SERVER_ARGS"));
        string languageId = Environment.GetEnvironmentVariable("XVE_LSP_TEST_LANGUAGE_ID") ?? "csharp";
        string extension = Environment.GetEnvironmentVariable("XVE_LSP_TEST_FILE_EXT") ?? ".cs";
        string? workspacePath = Environment.GetEnvironmentVariable("XVE_LSP_TEST_WORKSPACE");
        string text = Environment.GetEnvironmentVariable("XVE_LSP_TEST_TEXT")
            ?? "class C { void M() { int x = ; } }";

        string rootPath = ResolveWorkspacePath(workspacePath);
        string filePath = Path.Combine(rootPath, "LspCompletionsTest" + NormalizeExtension(extension));
        Directory.CreateDirectory(rootPath);
        await File.WriteAllTextAsync(filePath, text);

        LspServerConfiguration config = new()
        {
            LanguageId = languageId,
            ServerPath = serverPath,
            Arguments = args,
            WorkingDirectory = rootPath,
            FileExtensions = new[] { NormalizeExtension(extension) }
        };

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
        LspLanguageServiceRouter router = new(new[] { config });
        ILanguageServiceSession? session = await router.GetSessionAsync(languageId, new LanguageWorkspaceInfo
        {
            RootPath = rootPath,
            Kind = WorkspaceKind.Folder
        }, cts.Token);

        if (session is null)
        {
            return (null, null, rootPath, languageId);
        }

        await session.InitializeAsync(new LspInitializeParams
        {
            ProcessId = Environment.ProcessId,
            RootUri = new Uri(Path.GetFullPath(rootPath)).AbsoluteUri,
            ClientInfo = new LspClientInfo { Name = "XamlVisualEditor.Tests" },
            Capabilities = new { }
        }, cts.Token);

        Uri uri = new(Path.GetFullPath(filePath));
        await session.PublishDocumentAsync(new LspTextDocumentItem
        {
            Uri = uri,
            LanguageId = languageId,
            Version = 1,
            Text = text
        }, cts.Token);

        return (session, uri, rootPath, languageId);
    }

    private static bool TryGetPosition(string lineVar, string columnVar, out LspPosition position)
    {
        position = default;
        string? lineText = Environment.GetEnvironmentVariable(lineVar);
        string? columnText = Environment.GetEnvironmentVariable(columnVar);
        if (!int.TryParse(lineText, out int line) || !int.TryParse(columnText, out int column))
        {
            return false;
        }

        position = new LspPosition(line, column);
        return true;
    }

    private static string ResolveWorkspacePath(string? workspacePath)
    {
        if (!string.IsNullOrWhiteSpace(workspacePath))
        {
            return workspacePath;
        }

        return Path.Combine(Path.GetTempPath(), "XamlVisualEditor.LspTests");
    }

    private static string[] SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".cs";
        }

        return extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
    }
}
