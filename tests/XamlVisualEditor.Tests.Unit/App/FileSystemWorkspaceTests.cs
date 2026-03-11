using System.Text;
using XamlVisualEditor.App.Services;
using XamlVisualEditor.Extensions;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.App;

public sealed class FileSystemWorkspaceTests
{
    [Fact]
    public async Task FindFilesAsync_UsesWorkspaceRoot_AndRespectsExcludeGlob()
    {
        string root = CreateTempDirectory();
        try
        {
            string solutionPath = Path.Combine(root, "App.sln");
            await File.WriteAllTextAsync(solutionPath, string.Empty);
            await File.WriteAllTextAsync(Path.Combine(root, "MainWindow.axaml"), "<Window />");

            string nestedRoot = Path.Combine(root, "src");
            string nestedFile = Path.Combine(nestedRoot, "View.axaml");
            string excludedFile = Path.Combine(nestedRoot, "bin", "Debug", "Generated.axaml");
            Directory.CreateDirectory(Path.GetDirectoryName(excludedFile)!);
            await File.WriteAllTextAsync(nestedFile, "<UserControl />");
            await File.WriteAllTextAsync(excludedFile, "<Generated />");

            StubWorkspaceInfo workspaceInfo = new() { WorkspacePath = solutionPath };
            FileSystemWorkspace workspace = new(workspaceInfo);

            IReadOnlyList<string> matches = await workspace.FindFilesAsync(
                "**/*.axaml",
                "**/bin/**",
                CancellationToken.None);

            Assert.Contains(matches, path => string.Equals(path, nestedFile, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(matches, path => string.Equals(path, Path.Combine(root, "MainWindow.axaml"), StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(matches, path => string.Equals(path, excludedFile, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task WriteAndReadFileAsync_HandleRelativePaths()
    {
        string root = CreateTempDirectory();
        try
        {
            StubWorkspaceInfo workspaceInfo = new() { WorkspacePath = root };
            FileSystemWorkspace workspace = new(workspaceInfo);

            byte[] expected = Encoding.UTF8.GetBytes("hello workspace");
            await workspace.WriteFileAsync("folder/test.txt", expected, CancellationToken.None);
            byte[] actual = await workspace.ReadFileAsync("folder/test.txt", CancellationToken.None);

            Assert.Equal(expected, actual);
            Assert.True(File.Exists(Path.Combine(root, "folder", "test.txt")));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "XveFileSystemWorkspaceTests", Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class StubWorkspaceInfo : IWorkspaceInfo
    {
        public string? WorkspacePath { get; set; }

        public event EventHandler<WorkspaceChangedEventArgs> WorkspaceChanged
        {
            add { }
            remove { }
        }
    }
}
