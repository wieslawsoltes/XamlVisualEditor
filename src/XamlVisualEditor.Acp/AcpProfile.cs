using System.Collections.Generic;

namespace XamlVisualEditor.Acp;

public sealed class AcpProfile
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Command { get; set; } = string.Empty;

    public List<string> Arguments { get; set; } = new();

    public string? WorkingDirectory { get; set; }

    public Dictionary<string, string> Environment { get; set; } = new();

    public string? Model { get; set; }

    public string? ModelEnvVar { get; set; }

    public string? ApiKeyEnvVar { get; set; }

    public string? OAuthClientId { get; set; }

    public string? OAuthScopes { get; set; }

    public string? OAuthDeviceCodeUrl { get; set; }

    public string? OAuthTokenUrl { get; set; }

    public bool UseKeychain { get; set; } = true;

    public bool IsBuiltIn { get; set; }

    public static AcpProfile CreateCodexProfile()
    {
        return new AcpProfile
        {
            Id = "codex",
            Name = "OpenAI Codex",
            Description = "OpenAI Codex ACP agent",
            Command = "codex",
            Arguments = new List<string> { "--stdio" },
            Model = "gpt-5-codex",
            ModelEnvVar = "OPENAI_MODEL",
            ApiKeyEnvVar = "OPENAI_API_KEY",
            OAuthDeviceCodeUrl = "https://api.openai.com/v1/oauth/device/code",
            OAuthTokenUrl = "https://api.openai.com/v1/oauth/token",
            OAuthScopes = "openid profile email offline_access",
            UseKeychain = true,
            IsBuiltIn = true
        };
    }
}
