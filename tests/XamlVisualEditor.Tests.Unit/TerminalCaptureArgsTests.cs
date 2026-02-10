using System;
using System.IO;
using XamlVisualEditor.Terminal;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class TerminalCaptureArgsTests
{
    [Fact]
    public void ResolveCapturePathReturnsNullWhenNotRequested()
    {
        string? path = TerminalCaptureArgs.ResolveCapturePath(Array.Empty<string>(), "base");
        Assert.Null(path);
    }

    [Fact]
    public void ResolveCapturePathReturnsExplicitPath()
    {
        string expected = Path.Combine("root", "project", "capture.log");
        string? path = TerminalCaptureArgs.ResolveCapturePath(new[]
        {
            TerminalCaptureArgs.CaptureArg,
            expected
        }, "base");

        Assert.Equal(expected, path);
    }

    [Fact]
    public void ResolveCapturePathUsesDefaultPathWhenValueMissing()
    {
        DateTime timestamp = new(2026, 2, 10, 11, 12, 13, DateTimeKind.Utc);
        string baseDirectory = Path.Combine("root", "project");
        string expected = Path.Combine(baseDirectory, "Captures", "terminal-20260210-111213.xve.log");

        string? path = TerminalCaptureArgs.ResolveCapturePath(new[]
        {
            TerminalCaptureArgs.CaptureArg
        }, baseDirectory, () => timestamp);

        Assert.Equal(expected, path);
    }

    [Fact]
    public void ResolveCapturePathUsesDefaultPathWhenNextArgIsFlag()
    {
        DateTime timestamp = new(2026, 2, 10, 11, 12, 13, DateTimeKind.Utc);
        string baseDirectory = Path.Combine("root", "project");
        string expected = Path.Combine(baseDirectory, "Captures", "terminal-20260210-111213.xve.log");

        string? path = TerminalCaptureArgs.ResolveCapturePath(new[]
        {
            "--other",
            TerminalCaptureArgs.CaptureArg,
            "--flag"
        }, baseDirectory, () => timestamp);

        Assert.Equal(expected, path);
    }
}
