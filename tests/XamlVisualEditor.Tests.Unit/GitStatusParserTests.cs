using XamlVisualEditor.Core;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class GitStatusParserTests
{
    [Fact]
    public void ParseStatus_ParsesBranchAndChanges()
    {
        string statusText = string.Join("\n", new[]
        {
            "## main...origin/main [ahead 2, behind 1]",
            " M src/App.cs",
            "A  README.md",
            "R  old.txt -> new.txt",
            "?? newfile.txt",
            string.Empty
        });

        GitRepositoryStatus status = GitStatusParser.ParseStatus("/repo", statusText);

        Assert.Equal("main", status.BranchName);
        Assert.Equal("origin/main", status.UpstreamName);
        Assert.Equal(2, status.AheadBy);
        Assert.Equal(1, status.BehindBy);
        Assert.Equal(4, status.Changes.Count);

        GitFileChange modified = status.Changes[0];
        Assert.Equal("src/App.cs", modified.Path);
        Assert.Equal(GitChangeKind.Modified, modified.WorkTreeStatus);

        GitFileChange added = status.Changes[1];
        Assert.Equal("README.md", added.Path);
        Assert.Equal(GitChangeKind.Added, added.IndexStatus);

        GitFileChange renamed = status.Changes[2];
        Assert.Equal("new.txt", renamed.Path);
        Assert.Equal("old.txt", renamed.OldPath);
        Assert.True(renamed.IsRenamed);

        GitFileChange untracked = status.Changes[3];
        Assert.Equal("newfile.txt", untracked.Path);
        Assert.True(untracked.IsUntracked);
        Assert.Equal(GitChangeKind.Untracked, untracked.IndexStatus);
    }
}
