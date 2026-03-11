using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.OutputExtension;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

#pragma warning disable CS0067

public sealed class OutputExtensionTests
{
    [Fact]
    public async Task NavigateToRelativeAsync_ReturnsFalse_WhenNoDiagnostics()
    {
        StubEditorServices editor = new();
        using ProblemsPanelViewModel viewModel = new(new StubDiagnosticsService(), editor);

        bool navigated = await viewModel.NavigateToRelativeAsync(1, CancellationToken.None);

        Assert.False(navigated);
        Assert.Empty(editor.OpenedLocations);
    }

    [Fact]
    public async Task NavigateToRelativeAsync_CyclesDiagnosticsAndOpensLocations()
    {
        StubEditorServices editor = new();
        using ProblemsPanelViewModel viewModel = new(new StubDiagnosticsService(), editor);
        viewModel.HandleSnapshotsPublished(
        [
            new DiagnosticsDocumentSnapshot(
                "/repo",
                [
                    CreateDiagnostic("/repo/FileA.axaml", 2, 5, "A"),
                    CreateDiagnostic("/repo/FileB.axaml", 9, 3, "B")
                ])
        ]);

        bool first = await viewModel.NavigateToRelativeAsync(1, CancellationToken.None);
        bool second = await viewModel.NavigateToRelativeAsync(1, CancellationToken.None);
        bool previous = await viewModel.NavigateToRelativeAsync(-1, CancellationToken.None);

        Assert.True(first);
        Assert.True(second);
        Assert.True(previous);
        Assert.Equal(3, editor.OpenedLocations.Count);
        Assert.Equal("/repo/FileA.axaml", editor.OpenedLocations[0].FilePath);
        Assert.Equal("/repo/FileB.axaml", editor.OpenedLocations[1].FilePath);
        Assert.Equal("/repo/FileA.axaml", editor.OpenedLocations[2].FilePath);
    }

    private static LanguageDiagnostic CreateDiagnostic(string filePath, int line, int column, string message)
    {
        return new LanguageDiagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Message = message,
            FilePath = filePath,
            Range = new LanguageTextRange(
                new LanguageTextPosition(line, column),
                new LanguageTextPosition(line, column))
        };
    }

    private sealed class StubEditorServices : IEditorServices
    {
        public List<LanguageLocation> OpenedLocations { get; } = new();

        public IEditorDocument? ActiveDocument => null;

        public event EventHandler<EditorActiveDocumentChangedEventArgs>? ActiveDocumentChanged;

        public IReadOnlyList<IEditorDocument> GetOpenDocuments() => Array.Empty<IEditorDocument>();

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, CancellationToken ct)
            => Task.FromResult<IEditorDocument?>(null);

        public Task<IEditorDocument?> OpenDocumentAsync(string filePath, EditorDocumentOpenBehavior behavior, CancellationToken ct)
            => Task.FromResult<IEditorDocument?>(null);

        public Task<bool> OpenLocationAsync(LanguageLocation location, CancellationToken ct)
        {
            OpenedLocations.Add(location);
            return Task.FromResult(true);
        }
    }

    private sealed class StubDiagnosticsService : IDiagnosticsService
    {
        public event EventHandler<DiagnosticsChannelsChangedEventArgs>? ChannelsChanged;
        public event EventHandler<DiagnosticsChannelPublishedEventArgs>? DiagnosticsChannelPublished;
        public event EventHandler<DiagnosticsSnapshotPublishedEventArgs>? DiagnosticsSnapshotPublished;
        public event EventHandler<DiagnosticsPublishedEventArgs>? DiagnosticsPublished;
        public event EventHandler<DiagnosticsChangedEventArgs>? DiagnosticsChanged;

        public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(string? filePath, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());

        public Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(DiagnosticsQuery query, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<LanguageDiagnostic>>(Array.Empty<LanguageDiagnostic>());

        public Task<IReadOnlyList<DiagnosticsDocumentSnapshot>> GetDiagnosticsSnapshotAsync(
            DiagnosticsQuery query,
            CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DiagnosticsDocumentSnapshot>>(Array.Empty<DiagnosticsDocumentSnapshot>());

        public Task<IReadOnlyList<DiagnosticsChannelInfo>> GetChannelsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DiagnosticsChannelInfo>>(Array.Empty<DiagnosticsChannelInfo>());
    }
}

#pragma warning restore CS0067
