using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Acp;
using XamlVisualEditor.AcpExtension;
using Xunit;

namespace XamlVisualEditor.Tests.Integration;

public sealed class AcpOAuthFlowIntegrationTests
{
    [Fact]
    public async Task DeviceFlowStoresTokenAndApiKey()
    {
        FakeOAuthService oauth = new();
        FakeSecretStore secrets = new();
        FakeAcpService service = new();

        AcpProfile profile = AcpProfile.CreateCodexProfile();
        profile.OAuthClientId = "client-id";

        AcpProfileViewModel profileVm = new(profile);
        AcpToolViewModel vm = new(service, null, secrets, oauth, () => "/tmp");
        vm.SelectedProfile = profileVm;

        vm.OpenUrlInteraction.RegisterHandler(ctx =>
        {
            ctx.SetOutput(Unit.Default);
            return Task.CompletedTask;
        });

        await vm.StartWebAuthCommand.Execute().ToTask();

        string oauthKey = "acp.profile." + profile.Id + ".oauth";
        string apiKey = "acp.profile." + profile.Id + ".apiKey";

        Assert.True(secrets.Stored.ContainsKey(oauthKey));
        Assert.True(secrets.Stored.ContainsKey(apiKey));
        Assert.Equal("token-123", secrets.Stored[apiKey]);

        using JsonDocument doc = JsonDocument.Parse(secrets.Stored[oauthKey]);
        string token = doc.RootElement.GetProperty("AccessToken").GetString() ?? string.Empty;
        Assert.Equal("token-123", token);
    }

    private sealed class FakeOAuthService : IAcpOAuthDeviceFlowService
    {
        public Task<AcpDeviceCodeResponse> StartDeviceFlowAsync(string clientId, string scope, string deviceCodeUrl, CancellationToken ct)
        {
            return Task.FromResult(new AcpDeviceCodeResponse(
                "device",
                "CODE-123",
                "https://example.com/verify",
                "https://example.com/verify?code=CODE-123",
                900,
                0));
        }

        public Task<AcpTokenResponse> CompleteDeviceFlowAsync(string clientId, string deviceCode, int intervalSeconds, string tokenUrl, CancellationToken ct)
        {
            return Task.FromResult(new AcpTokenResponse("token-123", "refresh-123", 3600, "bearer"));
        }

        public Task<AcpTokenResponse> RefreshTokenAsync(string clientId, string refreshToken, string tokenUrl, CancellationToken ct)
        {
            return Task.FromResult(new AcpTokenResponse("token-456", "refresh-456", 3600, "bearer"));
        }
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        public Dictionary<string, string> Stored { get; } = new(StringComparer.Ordinal);

        public Task<string?> GetSecretAsync(string key, CancellationToken ct)
        {
            return Task.FromResult(Stored.TryGetValue(key, out string? value) ? value : null);
        }

        public Task SetSecretAsync(string key, string secret, CancellationToken ct)
        {
            Stored[key] = secret;
            return Task.CompletedTask;
        }

        public Task RemoveSecretAsync(string key, CancellationToken ct)
        {
            Stored.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAcpService : IAcpService
    {
        public bool IsConnected => false;
        public string? ActiveSessionId => null;
        public event Action<string>? StderrReceived { add { } remove { } }
        public event Action<string, JsonElement?>? NotificationReceived { add { } remove { } }

        public Task ConnectAsync(AcpAgentProcessOptions options, CancellationToken ct) => Task.CompletedTask;
        public Task ConnectMockAgentAsync(CancellationToken ct) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<JsonElement> InitializeAsync(object? parameters, CancellationToken ct) => Task.FromResult(default(JsonElement));
        public Task<string> CreateSessionAsync(object? parameters, CancellationToken ct) => Task.FromResult(string.Empty);
        public Task<JsonElement> PromptAsync(string sessionId, object? content, CancellationToken ct) => Task.FromResult(default(JsonElement));
        public Task CancelAsync(string sessionId, CancellationToken ct) => Task.CompletedTask;
        public Task<JsonElement> SendRequestAsync(string method, object? parameters, CancellationToken ct) => Task.FromResult(default(JsonElement));
        public Task SendNotificationAsync(string method, object? parameters, CancellationToken ct) => Task.CompletedTask;
        public void SetPermissionHandler(Func<AcpPermissionRequest, CancellationToken, Task<AcpPermissionOutcome>>? handler)
        {
        }
    }
}
