using System.Collections.Generic;
using System.Reflection;
using XamlVisualEditor.NavigationExtension;
using NavigationExtensionEntry = XamlVisualEditor.NavigationExtension.NavigationExtension;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class NavigationExtensionTests
{
    [Fact]
    public void TryParseLineAndColumn_ParsesLineAndColumnFormats()
    {
        bool parsedPair = InvokeTryParse("42:7", out int line, out int column);
        Assert.True(parsedPair);
        Assert.Equal(42, line);
        Assert.Equal(7, column);

        bool parsedLineOnly = InvokeTryParse("19", out line, out column);
        Assert.True(parsedLineOnly);
        Assert.Equal(19, line);
        Assert.Equal(1, column);

        bool parsedInvalid = InvokeTryParse("abc", out _, out _);
        Assert.False(parsedInvalid);
    }

    [Fact]
    public void IsQuickOpenCandidate_FiltersByExtension_AndBuildArtifacts()
    {
        Assert.True(InvokeIsQuickOpenCandidate("/repo/src/MainWindow.axaml"));
        Assert.True(InvokeIsQuickOpenCandidate("/repo/src/ViewModel.cs"));
        Assert.False(InvokeIsQuickOpenCandidate("/repo/src/bin/Debug/Generated.axaml"));
        Assert.False(InvokeIsQuickOpenCandidate("/repo/assets/logo.png"));
    }

    [Fact]
    public void IsFindInFilesCandidate_FiltersByExtension_AndBuildArtifacts()
    {
        Assert.True(InvokeIsFindInFilesCandidate("/repo/app.slnx"));
        Assert.True(InvokeIsFindInFilesCandidate("/repo/src/ViewModel.cs"));
        Assert.False(InvokeIsFindInFilesCandidate("/repo/src/obj/Debug/generated.cs"));
        Assert.False(InvokeIsFindInFilesCandidate("/repo/assets/logo.png"));
    }

    [Fact]
    public void AppendTextMatches_CreatesExpectedLocations()
    {
        List<ReferenceLocationItemViewModel> matches = [];
        InvokeAppendTextMatches(
            "/repo/src/MainWindow.axaml",
            "line1\nSecond line with abc\nabc third",
            "abc",
            matches,
            10);

        Assert.Equal(2, matches.Count);
        Assert.Equal(2, matches[0].Line);
        Assert.Equal(18, matches[0].Column);
        Assert.Equal(3, matches[1].Line);
        Assert.Equal(1, matches[1].Column);
        Assert.Contains("MainWindow.axaml (2,18): Second line with abc", matches[0].DisplayText);
    }

    [Fact]
    public void GetLineForOffset_ReturnsOneBasedLine()
    {
        int line1 = InvokeGetLineForOffset("line1\nline2\nline3", 0);
        int line2 = InvokeGetLineForOffset("line1\nline2\nline3", 6);
        int line3 = InvokeGetLineForOffset("line1\nline2\nline3", 12);

        Assert.Equal(1, line1);
        Assert.Equal(2, line2);
        Assert.Equal(3, line3);
    }

    private static bool InvokeTryParse(string input, out int line, out int column)
    {
        MethodInfo method = typeof(NavigationExtensionEntry).GetMethod(
                                "TryParseLineAndColumn",
                                BindingFlags.NonPublic | BindingFlags.Static)
                            ?? throw new InvalidOperationException("TryParseLineAndColumn not found.");

        object?[] args = [input, 0, 0];
        bool parsed = (bool)(method.Invoke(null, args) ?? false);
        line = (int)args[1]!;
        column = (int)args[2]!;
        return parsed;
    }

    private static bool InvokeIsQuickOpenCandidate(string path)
    {
        MethodInfo method = typeof(NavigationExtensionEntry).GetMethod(
                                "IsQuickOpenCandidate",
                                BindingFlags.NonPublic | BindingFlags.Static)
                            ?? throw new InvalidOperationException("IsQuickOpenCandidate not found.");

        return (bool)(method.Invoke(null, [path]) ?? false);
    }

    private static bool InvokeIsFindInFilesCandidate(string path)
    {
        MethodInfo method = typeof(NavigationExtensionEntry).GetMethod(
                                "IsFindInFilesCandidate",
                                BindingFlags.NonPublic | BindingFlags.Static)
                            ?? throw new InvalidOperationException("IsFindInFilesCandidate not found.");

        return (bool)(method.Invoke(null, [path]) ?? false);
    }

    private static int InvokeGetLineForOffset(string text, int offset)
    {
        MethodInfo method = typeof(NavigationExtensionEntry).GetMethod(
                                "GetLineForOffset",
                                BindingFlags.NonPublic | BindingFlags.Static)
                            ?? throw new InvalidOperationException("GetLineForOffset not found.");

        return (int)(method.Invoke(null, [text, offset]) ?? 1);
    }

    private static void InvokeAppendTextMatches(
        string filePath,
        string text,
        string query,
        List<ReferenceLocationItemViewModel> matches,
        int maxMatches)
    {
        MethodInfo method = typeof(NavigationExtensionEntry).GetMethod(
                                "AppendTextMatches",
                                BindingFlags.NonPublic | BindingFlags.Static)
                            ?? throw new InvalidOperationException("AppendTextMatches not found.");

        method.Invoke(null, [filePath, text, query, matches, maxMatches]);
    }
}
