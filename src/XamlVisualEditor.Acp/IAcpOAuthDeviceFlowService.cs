using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Acp;

public sealed record AcpDeviceCodeResponse(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    string? VerificationUriComplete,
    int ExpiresIn,
    int Interval);

public sealed record AcpTokenResponse(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string? TokenType);

public interface IAcpOAuthDeviceFlowService
{
    Task<AcpDeviceCodeResponse> StartDeviceFlowAsync(
        string clientId,
        string scope,
        string deviceCodeUrl,
        CancellationToken ct);

    Task<AcpTokenResponse> CompleteDeviceFlowAsync(
        string clientId,
        string deviceCode,
        int intervalSeconds,
        string tokenUrl,
        CancellationToken ct);

    Task<AcpTokenResponse> RefreshTokenAsync(
        string clientId,
        string refreshToken,
        string tokenUrl,
        CancellationToken ct);
}
