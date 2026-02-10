using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Acp;
using Xunit;

namespace XamlVisualEditor.Tests.Integration;

public sealed class AcpIntegrationTests
{
    [Fact]
    public async Task MockAgentRespondsToSessionFlow()
    {
        string mockAgentPath = ResolveMockAgentPath();
        Assert.True(File.Exists(mockAgentPath), $"Mock agent not found at {mockAgentPath}");

        AcpAgentProcessOptions options = new()
        {
            FileName = "dotnet",
            Arguments = $"\"{mockAgentPath}\""
        };

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        await using AcpAgentHost host = await AcpAgentHost.StartAsync(options, cts.Token);

        TaskCompletionSource<bool> updateReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Client.NotificationReceived += (_, _) => updateReceived.TrySetResult(true);

        _ = await host.Client.SendRequestAsync("initialize", new { clientInfo = new { name = "XVE", version = "test" } }, cts.Token);
        var sessionResult = await host.Client.SendRequestAsync("session/new", new { }, cts.Token);
        string sessionId = sessionResult.TryGetProperty("sessionId", out var sessionIdElement)
            ? sessionIdElement.GetString() ?? string.Empty
            : string.Empty;
        Assert.False(string.IsNullOrWhiteSpace(sessionId));

        _ = await host.Client.SendRequestAsync(
            "session/prompt",
            new
            {
                sessionId,
                content = new[] { new { type = "text", text = "hello" } }
            },
            cts.Token);

        await Task.WhenAny(updateReceived.Task, Task.Delay(TimeSpan.FromSeconds(5), cts.Token));
        Assert.True(updateReceived.Task.IsCompleted, "Did not receive session/update notification.");
    }

    private static string ResolveMockAgentPath()
    {
        string baseDir = AppContext.BaseDirectory;
        DirectoryInfo? current = new DirectoryInfo(baseDir);
        for (int i = 0; i < 6 && current is not null; i++)
        {
            string repoRoot = current.FullName;
            string debugPath = Path.Combine(repoRoot, "src", "XamlVisualEditor.Acp.MockAgent", "bin", "Debug", "net10.0", "XamlVisualEditor.Acp.MockAgent.dll");
            if (File.Exists(debugPath))
            {
                return debugPath;
            }

            string releasePath = Path.Combine(repoRoot, "src", "XamlVisualEditor.Acp.MockAgent", "bin", "Release", "net10.0", "XamlVisualEditor.Acp.MockAgent.dll");
            if (File.Exists(releasePath))
            {
                return releasePath;
            }

            current = current.Parent;
        }

        return string.Empty;
    }
}
