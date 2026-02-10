using System;
using System.IO;
using XamlVisualEditor.Acp;

namespace XamlVisualEditor.App.Services;

public sealed class AcpSettings : IAcpSettings
{
    private const string MockAgentPathEnv = "XVE_ACP_MOCK_AGENT_PATH";

    public AcpSettings()
    {
        MockAgentPath = ResolveDefaultMockAgentPath();
    }

    public string MockAgentPath { get; set; }

    private static string ResolveDefaultMockAgentPath()
    {
        string? envPath = Environment.GetEnvironmentVariable(MockAgentPathEnv);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return envPath;
        }

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
