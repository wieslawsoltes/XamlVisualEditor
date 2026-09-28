using System;
using System.IO;
using XamlVisualEditor.Core;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class FileSystemPathComparisonTests
{
    [Fact]
    public void ComparerMatchesCurrentOperatingSystem()
    {
        bool pathsAreEqual = FileSystemPathComparison.Equals("Sample", "sample");

        Assert.Equal(OperatingSystem.IsWindows(), pathsAreEqual);
    }

    [Fact]
    public void IsSameOrDescendantRejectsSiblingWithSharedPrefix()
    {
        string root = Path.Combine(Path.GetTempPath(), "XvePathComparison", "Root");
        string child = Path.Combine(root, "Views", "Sample.axaml");
        string sibling = root + "-Other";

        Assert.True(FileSystemPathComparison.IsSameOrDescendant(child, root));
        Assert.False(FileSystemPathComparison.IsSameOrDescendant(sibling, root));
    }
}
