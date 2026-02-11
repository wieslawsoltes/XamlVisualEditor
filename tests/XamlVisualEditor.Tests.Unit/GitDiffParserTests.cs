using System.Linq;
using XamlVisualEditor.Core;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class GitDiffParserTests
{
    [Fact]
    public void ParseUnifiedDiff_ParsesMultipleFilesAndHunks()
    {
        string diffText = string.Join("\n", new[]
        {
            "diff --git a/foo.txt b/foo.txt",
            "index 1234567..89abcde 100644",
            "--- a/foo.txt",
            "+++ b/foo.txt",
            "@@ -1,2 +1,3 @@",
            " line1",
            "-line2",
            "+line2 modified",
            "+line3",
            "diff --git a/bar.txt b/bar.txt",
            "new file mode 100644",
            "index 0000000..fedcba9",
            "--- /dev/null",
            "+++ b/bar.txt",
            "@@ -0,0 +1,2 @@",
            "+alpha",
            "+beta",
            string.Empty
        });

        GitDiff diff = GitDiffParser.ParseUnifiedDiff(diffText);

        Assert.Equal(2, diff.Files.Count);
        Assert.Equal("foo.txt", diff.Files[0].Path);
        Assert.Single(diff.Files[0].Hunks);
        Assert.Equal(4, diff.Files[0].Hunks[0].Lines.Count);
        Assert.Equal(GitDiffLineKind.Removed, diff.Files[0].Hunks[0].Lines[1].Kind);
        Assert.Equal(GitDiffLineKind.Added, diff.Files[0].Hunks[0].Lines[2].Kind);

        Assert.Equal("bar.txt", diff.Files[1].Path);
        Assert.Single(diff.Files[1].Hunks);
        Assert.True(diff.Files[1].Hunks[0].Lines.All(line => line.Kind == GitDiffLineKind.Added));
    }

    [Fact]
    public void ParseUnifiedDiff_DetectsBinaryFiles()
    {
        string diffText = string.Join("\n", new[]
        {
            "diff --git a/image.png b/image.png",
            "index 1111111..2222222 100644",
            "Binary files a/image.png and b/image.png differ",
            string.Empty
        });

        GitDiff diff = GitDiffParser.ParseUnifiedDiff(diffText);

        Assert.Single(diff.Files);
        Assert.True(diff.Files[0].IsBinary);
        Assert.Equal("image.png", diff.Files[0].Path);
    }
}
