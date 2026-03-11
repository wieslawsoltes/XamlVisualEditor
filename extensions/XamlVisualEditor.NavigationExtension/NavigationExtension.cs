using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.NavigationExtension;

public sealed class NavigationExtension : IXveExtension
{
    private const string ReferencesViewId = "references.panel";
    private const string FindReferencesCommandId = "navigation.findReferences";
    private const string FindInFilesCommandId = "navigation.findInFiles";
    private const string GoToDefinitionCommandId = "navigation.goToDefinition";
    private const string QuickOpenCommandId = "navigation.quickOpen";
    private const string GoToLineCommandId = "navigation.goToLine";
    private const string NavigateBackCommandId = "navigation.history.back";
    private const string NavigateForwardCommandId = "navigation.history.forward";
    private const string ToggleReferencesCommandId = "navigation.references.toggleView";
    private const int FindInFilesMaxFiles = 2500;
    private const int FindInFilesMaxMatches = 500;
    private const int FindInFilesMaxFileBytes = 1_048_576;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly string[] QuickOpenExcludedSegments = ["/bin/", "/obj/", "/.git/", "/.vs/"];
    private static readonly string[] QuickOpenExtensions =
    [
        ".axaml",
        ".xaml",
        ".cs",
        ".csx",
        ".fs",
        ".vb",
        ".csproj",
        ".fsproj",
        ".vbproj",
        ".sln",
        ".slnx",
        ".json",
        ".xml",
        ".props",
        ".targets",
        ".md",
        ".txt"
    ];
    private static readonly string[] FindInFilesExtensions =
    [
        ".axaml",
        ".xaml",
        ".cs",
        ".csx",
        ".fs",
        ".vb",
        ".csproj",
        ".fsproj",
        ".vbproj",
        ".sln",
        ".slnx",
        ".json",
        ".xml",
        ".props",
        ".targets",
        ".md",
        ".txt"
    ];

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        ReferencesPanelViewModel viewModel = new(context.Navigation, context.Editor, context.Window);

        context.Subscriptions.Add(context.Commands.Register(
            FindReferencesCommandId,
            _ => viewModel.FindReferencesAsync(CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            FindInFilesCommandId,
            _ => FindInFilesAsync(context, viewModel, CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            GoToDefinitionCommandId,
            _ => viewModel.GoToDefinitionAsync(CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            QuickOpenCommandId,
            _ => QuickOpenAsync(context, CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            GoToLineCommandId,
            _ => GoToLineAsync(context, CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            NavigateBackCommandId,
            _ => context.NavigationHistory.NavigateBackAsync(CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            NavigateForwardCommandId,
            _ => context.NavigationHistory.NavigateForwardAsync(CancellationToken.None)));
        context.Subscriptions.Add(context.Commands.Register(
            ToggleReferencesCommandId,
            _ => context.ViewHost.ToggleAsync(ReferencesViewId, CancellationToken.None)));

        context.Subscriptions.Add(context.CommandMetadata.Register(
            FindReferencesCommandId,
            new CommandMetadata(
                Title: "Navigation: Find References",
                Category: "Navigation",
                When: "hasTextDocument",
                Keybinding: "Shift+F12",
                Priority: 50)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            FindInFilesCommandId,
            new CommandMetadata(
                Title: "Navigation: Find In Files",
                Category: "Navigation",
                When: "hasWorkspace",
                Keybinding: "Ctrl+Shift+F",
                Priority: 55)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            GoToDefinitionCommandId,
            new CommandMetadata(
                Title: "Navigation: Go To Definition",
                Category: "Navigation",
                When: "hasTextDocument",
                Keybinding: "F12",
                Priority: 40)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            QuickOpenCommandId,
            new CommandMetadata(
                Title: "Navigation: Quick Open",
                Category: "Navigation",
                When: "hasWorkspace",
                Keybinding: "Ctrl+P",
                Priority: 12)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            GoToLineCommandId,
            new CommandMetadata(
                Title: "Navigation: Go To Line",
                Category: "Navigation",
                When: "hasTextDocument",
                Keybinding: "Ctrl+G",
                Priority: 13)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            NavigateBackCommandId,
            new CommandMetadata(
                Title: "Navigation: Back",
                Category: "Navigation",
                When: "canNavigateBack",
                Keybinding: "Alt+Left",
                Priority: 10)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            NavigateForwardCommandId,
            new CommandMetadata(
                Title: "Navigation: Forward",
                Category: "Navigation",
                When: "canNavigateForward",
                Keybinding: "Alt+Right",
                Priority: 20)));
        context.Subscriptions.Add(context.CommandMetadata.Register(
            ToggleReferencesCommandId,
            new CommandMetadata(
                Title: "View: Toggle References",
                Category: "View",
                Priority: 60)));

        context.Subscriptions.Add(context.Contributions.RegisterCommandPaletteItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionCommandPaletteContribution(NavigateBackCommandId, "Navigate Back", "Navigation"),
                new ExtensionCommandPaletteContribution(NavigateForwardCommandId, "Navigate Forward", "Navigation"),
                new ExtensionCommandPaletteContribution(QuickOpenCommandId, "Quick Open File", "Navigation"),
                new ExtensionCommandPaletteContribution(FindInFilesCommandId, "Find In Files", "Navigation"),
                new ExtensionCommandPaletteContribution(GoToLineCommandId, "Go To Line", "Navigation"),
                new ExtensionCommandPaletteContribution(GoToDefinitionCommandId, "Go To Definition", "Navigation"),
                new ExtensionCommandPaletteContribution(FindReferencesCommandId, "Find References", "Navigation")
            }));

        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(
            context.ExtensionId,
            new[]
            {
                new ExtensionMenuContribution(
                    NavigateBackCommandId,
                    "Navigate Back",
                    ExtensionMenuLocations.Edit,
                    "navigation",
                    60),
                new ExtensionMenuContribution(
                    NavigateForwardCommandId,
                    "Navigate Forward",
                    ExtensionMenuLocations.Edit,
                    "navigation",
                    65),
                new ExtensionMenuContribution(
                    QuickOpenCommandId,
                    "Quick Open...",
                    ExtensionMenuLocations.Edit,
                    "navigation",
                    66),
                new ExtensionMenuContribution(
                    GoToLineCommandId,
                    "Go To Line...",
                    ExtensionMenuLocations.Edit,
                    "navigation",
                    67),
                new ExtensionMenuContribution(
                    FindInFilesCommandId,
                    "Find In Files...",
                    ExtensionMenuLocations.Edit,
                    "navigation",
                    68),
                new ExtensionMenuContribution(
                    GoToDefinitionCommandId,
                    "Go To Definition",
                    ExtensionMenuLocations.Edit,
                    "navigation",
                    70),
                new ExtensionMenuContribution(
                    FindReferencesCommandId,
                    "Find References",
                    ExtensionMenuLocations.Edit,
                    "navigation",
                    75),
                new ExtensionMenuContribution(
                    ToggleReferencesCommandId,
                    "References",
                    ExtensionMenuLocations.View,
                    "views.bottom",
                    40)
            }));

        context.Subscriptions.Add(context.Contributions.RegisterViews(
            context.ExtensionId,
            new[]
            {
                new ExtensionViewContribution(
                    ReferencesViewId,
                    "References",
                    ExtensionViewType.Custom,
                    ExtensionViewLocation.Bottom,
                    30,
                    ActivateByDefault: true)
            }));

        context.Subscriptions.Add(context.Views.RegisterCustomViewProvider(
            ReferencesViewId,
            new ReferencesPanelViewProvider(viewModel)));

        return Task.CompletedTask;
    }

    private static async Task FindInFilesAsync(
        ExtensionContext context,
        ReferencesPanelViewModel viewModel,
        CancellationToken cancellationToken)
    {
        string? workspacePath = context.WorkspaceInfo.WorkspacePath;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            await context.Window.ShowWarningMessageAsync("No workspace loaded.", cancellationToken);
            return;
        }

        string? defaultQuery = await GetSelectedTextAsync(context.Editor, cancellationToken).ConfigureAwait(false);
        string? query = await context.Window.ShowInputBoxAsync(
            new InputBoxOptions(
                "Find In Files",
                "Enter text to search in workspace files",
                defaultQuery),
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        string trimmedQuery = query.Trim();
        string workspaceRoot = ResolveWorkspaceRoot(workspacePath);
        IReadOnlyList<string> discovered = await context.Workspace.FindFilesAsync("**/*", null, cancellationToken).ConfigureAwait(false);

        List<ReferenceLocationItemViewModel> matches = new();
        foreach (string filePath in discovered
                     .Select(path => ResolveCandidatePath(path, workspaceRoot))
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Where(IsFindInFilesCandidate)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                     .Take(FindInFilesMaxFiles))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (matches.Count >= FindInFilesMaxMatches)
            {
                break;
            }

            byte[] content;
            try
            {
                content = await context.Workspace.ReadFileAsync(filePath, cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            if (content.Length == 0
                || content.Length > FindInFilesMaxFileBytes
                || IsBinaryContent(content))
            {
                continue;
            }

            string text;
            try
            {
                text = StrictUtf8.GetString(content);
            }
            catch (DecoderFallbackException)
            {
                continue;
            }

            AppendTextMatches(filePath, text, trimmedQuery, matches, FindInFilesMaxMatches);
        }

        viewModel.ReplaceItems(matches);
        await context.ViewHost.ShowAsync(ReferencesViewId, cancellationToken).ConfigureAwait(false);

        if (matches.Count == 0)
        {
            await context.Window.ShowInformationMessageAsync(
                $"No matches found for '{trimmedQuery}'.",
                cancellationToken).ConfigureAwait(false);
        }
        else if (matches.Count >= FindInFilesMaxMatches)
        {
            await context.Window.ShowInformationMessageAsync(
                $"Showing first {FindInFilesMaxMatches} matches for '{trimmedQuery}'.",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task QuickOpenAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        string? workspacePath = context.WorkspaceInfo.WorkspacePath;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            await context.Window.ShowWarningMessageAsync("No workspace loaded.", cancellationToken);
            return;
        }

        string workspaceRoot = ResolveWorkspaceRoot(workspacePath);
        IReadOnlyList<string> discovered = await context.Workspace.FindFilesAsync("**/*", null, cancellationToken);
        List<string> candidates = discovered
            .Select(path => ResolveCandidatePath(path, workspaceRoot))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(IsQuickOpenCandidate)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(1500)
            .ToList();

        if (candidates.Count == 0)
        {
            await context.Window.ShowInformationMessageAsync("No workspace files available for quick open.", cancellationToken);
            return;
        }

        List<QuickPickItem> items = new(candidates.Count);
        foreach (string path in candidates)
        {
            string relative = Path.GetRelativePath(workspaceRoot, path);
            string description = NormalizePath(Path.GetDirectoryName(relative) ?? ".");
            items.Add(new QuickPickItem(
                Path.GetFileName(path),
                description,
                path));
        }

        QuickPickItem? selected = await context.Window.ShowQuickPickAsync(
            items,
            new QuickPickOptions("Quick Open", false),
            cancellationToken);

        if (selected is null || string.IsNullOrWhiteSpace(selected.Detail))
        {
            return;
        }

        await context.Editor.OpenDocumentAsync(
            selected.Detail,
            EditorDocumentOpenBehavior.DocumentOnly,
            cancellationToken);
    }

    private static async Task GoToLineAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        IEditorDocument? activeDocument = context.Editor.ActiveDocument;
        if (activeDocument is null)
        {
            await context.Window.ShowWarningMessageAsync("No active text document.", cancellationToken);
            return;
        }

        string text = await activeDocument.GetTextAsync(cancellationToken);
        int currentLine = GetLineForOffset(text, activeDocument.CaretOffset);
        string? input = await context.Window.ShowInputBoxAsync(
            new InputBoxOptions(
                "Go To Line",
                "Enter line or line:column",
                currentLine.ToString(CultureInfo.InvariantCulture)),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        if (!TryParseLineAndColumn(input, out int line, out int column))
        {
            await context.Window.ShowWarningMessageAsync("Invalid location format. Use line or line:column.", cancellationToken);
            return;
        }

        bool opened = await context.Editor.OpenLocationAsync(
            new LanguageLocation
            {
                FilePath = activeDocument.FilePath,
                Range = new LanguageTextRange(
                    new LanguageTextPosition(line, column),
                    new LanguageTextPosition(line, column))
            },
            cancellationToken);

        if (!opened)
        {
            await context.Window.ShowWarningMessageAsync("Unable to navigate to the requested location.", cancellationToken);
        }
    }

    private static bool TryParseLineAndColumn(string value, out int line, out int column)
    {
        line = 1;
        column = 1;

        string[] parts = value
            .Trim()
            .Split([':', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length is 0 or > 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out line)
            || line <= 0)
        {
            return false;
        }

        if (parts.Length == 2
            && (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out column)
                || column <= 0))
        {
            return false;
        }

        return true;
    }

    private static int GetLineForOffset(string text, int caretOffset)
    {
        int boundedOffset = Math.Clamp(caretOffset, 0, text.Length);
        int line = 1;
        for (int index = 0; index < boundedOffset; index++)
        {
            if (text[index] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static bool IsFindInFilesCandidate(string path)
    {
        return IsCandidatePath(path, FindInFilesExtensions);
    }

    private static bool IsQuickOpenCandidate(string path)
    {
        return IsCandidatePath(path, QuickOpenExtensions);
    }

    private static bool IsCandidatePath(string path, IReadOnlyList<string> allowedExtensions)
    {
        string normalizedPath = NormalizePath(path);
        foreach (string segment in QuickOpenExcludedSegments)
        {
            if (normalizedPath.Contains(segment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        string extension = Path.GetExtension(path);
        foreach (string supported in allowedExtensions)
        {
            if (string.Equals(extension, supported, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<string?> GetSelectedTextAsync(IEditorServices editor, CancellationToken cancellationToken)
    {
        IEditorDocument? document = editor.ActiveDocument;
        if (document is null || document.SelectionLength <= 0)
        {
            return null;
        }

        string text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        int start = Math.Clamp(document.SelectionStart, 0, text.Length);
        int length = Math.Clamp(document.SelectionLength, 0, text.Length - start);
        if (length <= 0)
        {
            return null;
        }

        string selected = text.Substring(start, length).Trim();
        if (selected.Length == 0 || selected.IndexOfAny(['\r', '\n']) >= 0)
        {
            return null;
        }

        return selected.Length > 120 ? selected[..120] : selected;
    }

    private static bool IsBinaryContent(byte[] content)
    {
        return Array.IndexOf(content, (byte)0) >= 0;
    }

    private static void AppendTextMatches(
        string filePath,
        string text,
        string query,
        List<ReferenceLocationItemViewModel> matches,
        int maxMatches)
    {
        if (string.IsNullOrWhiteSpace(text)
            || string.IsNullOrWhiteSpace(query)
            || matches.Count >= maxMatches)
        {
            return;
        }

        using StringReader reader = new(text);
        string? lineText;
        int lineNumber = 1;
        while ((lineText = reader.ReadLine()) is not null)
        {
            int searchIndex = 0;
            while (searchIndex < lineText.Length)
            {
                int matchIndex = lineText.IndexOf(query, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    break;
                }

                int column = matchIndex + 1;
                LanguageLocation location = new()
                {
                    FilePath = filePath,
                    Range = new LanguageTextRange(
                        new LanguageTextPosition(lineNumber, column),
                        new LanguageTextPosition(lineNumber, column))
                };

                string preview = CreateLinePreview(lineText);
                string label = $"{Path.GetFileName(filePath)} ({lineNumber},{column}): {preview}";
                matches.Add(new ReferenceLocationItemViewModel(location, label));
                if (matches.Count >= maxMatches)
                {
                    return;
                }

                searchIndex = matchIndex + Math.Max(1, query.Length);
            }

            lineNumber++;
        }
    }

    private static string CreateLinePreview(string lineText)
    {
        string preview = lineText.Trim();
        if (preview.Length == 0)
        {
            return "(blank)";
        }

        return preview.Length <= 120 ? preview : preview[..117] + "...";
    }

    private static string ResolveWorkspaceRoot(string workspacePath)
    {
        if (Directory.Exists(workspacePath))
        {
            return Path.GetFullPath(workspacePath);
        }

        if (File.Exists(workspacePath))
        {
            string? directory = Path.GetDirectoryName(workspacePath);
            return string.IsNullOrWhiteSpace(directory)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(directory);
        }

        string fullPath = Path.GetFullPath(workspacePath);
        if (Path.HasExtension(fullPath))
        {
            string? directory = Path.GetDirectoryName(fullPath);
            return string.IsNullOrWhiteSpace(directory)
                ? Directory.GetCurrentDirectory()
                : directory;
        }

        return fullPath;
    }

    private static string ResolveCandidatePath(string path, string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workspaceRoot, path));
    }

    private static string NormalizePath(string path)
    {
        return path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private sealed class ReferencesPanelViewProvider : ICustomViewProvider
    {
        private readonly ReferencesPanelViewModel _viewModel;

        public ReferencesPanelViewProvider(ReferencesPanelViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }
}
