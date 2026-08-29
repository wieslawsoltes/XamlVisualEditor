using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using XamlVisualEditor.CSharp.Language;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class CSharpWorkspaceManagerTests
{
    [Fact]
    public async Task InitializeWorkspaceAsync_DefersMsbuildLoadUntilDocumentRequest()
    {
        RecordingLogger<CSharpWorkspaceManager> logger = new();
        using CSharpWorkspaceManager manager = new(logger);
        string missingProject = Path.Combine(
            Path.GetTempPath(),
            "XamlVisualEditor-Missing-" + Guid.NewGuid().ToString("N"),
            "Missing.csproj");

        await manager.InitializeWorkspaceAsync(missingProject, CancellationToken.None);

        Assert.Empty(logger.Messages);

        Document? document = await manager.GetOrAddDocumentAsync(
            Path.ChangeExtension(missingProject, ".cs"),
            "internal sealed class Sample { }",
            CancellationToken.None);

        Assert.NotNull(document);
        Assert.Contains(logger.Messages, message => message.Contains("Failed to load MSBuild workspace", StringComparison.Ordinal));
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
