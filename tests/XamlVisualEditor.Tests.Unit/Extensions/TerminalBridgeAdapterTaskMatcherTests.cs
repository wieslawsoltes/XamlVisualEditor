using System.Collections.Generic;
using System.Text.RegularExpressions;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class TerminalBridgeAdapterTaskMatcherTests
{
    [Fact]
    public void TryMatchProblem_ParsesLineWithConfiguredGroups()
    {
        List<(Regex Regex, TaskProblemMatcher Matcher)> matchers =
        [
            (
                new Regex(@"^(.*)\((\d+),(\d+)\):\s+error\s+(.*)$", RegexOptions.Compiled),
                new TaskProblemMatcher(
                    Pattern: @"^(.*)\((\d+),(\d+)\):\s+error\s+(.*)$",
                    Severity: TaskProblemSeverity.Error,
                    FileGroup: 1,
                    LineGroup: 2,
                    ColumnGroup: 3,
                    MessageGroup: 4))
        ];

        bool matched = TerminalBridgeAdapter.TryMatchProblem(
            "src/Main.axaml(10,25): error XVE0001 invalid element",
            matchers,
            out TaskProblemMatch? problem);

        Assert.True(matched);
        Assert.NotNull(problem);
        Assert.Equal("src/Main.axaml", problem!.FilePath);
        Assert.Equal(10, problem.Line);
        Assert.Equal(25, problem.Column);
        Assert.Equal("XVE0001 invalid element", problem.Message);
        Assert.Equal(TaskProblemSeverity.Error, problem.Severity);
    }

    [Fact]
    public void TryMatchProblem_ReturnsFalseForNoMatch()
    {
        List<(Regex Regex, TaskProblemMatcher Matcher)> matchers =
        [
            (
                new Regex(@"^warning: (.*)$", RegexOptions.Compiled),
                new TaskProblemMatcher(@"^warning: (.*)$", TaskProblemSeverity.Warning, MessageGroup: 1))
        ];

        bool matched = TerminalBridgeAdapter.TryMatchProblem(
            "build completed successfully",
            matchers,
            out TaskProblemMatch? problem);

        Assert.False(matched);
        Assert.Null(problem);
    }
}
