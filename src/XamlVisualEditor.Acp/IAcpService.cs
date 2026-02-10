using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp;

public interface IAcpService
{
    bool IsConnected { get; }
    string? ActiveSessionId { get; }

    event Action<string>? StderrReceived;
    event Action<string, JsonElement?>? NotificationReceived;

    Task ConnectAsync(AcpAgentProcessOptions options, CancellationToken ct);
    Task ConnectMockAgentAsync(CancellationToken ct);
    Task DisconnectAsync();

    Task<JsonElement> InitializeAsync(object? parameters, CancellationToken ct);
    Task<string> CreateSessionAsync(object? parameters, CancellationToken ct);
    Task<JsonElement> PromptAsync(string sessionId, object? content, CancellationToken ct);
    Task CancelAsync(string sessionId, CancellationToken ct);

    Task<JsonElement> SendRequestAsync(string method, object? parameters, CancellationToken ct);
    Task SendNotificationAsync(string method, object? parameters, CancellationToken ct);

    void SetPermissionHandler(Func<AcpPermissionRequest, CancellationToken, Task<AcpPermissionOutcome>>? handler);
}
