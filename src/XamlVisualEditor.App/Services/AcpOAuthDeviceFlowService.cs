using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Acp;

namespace XamlVisualEditor.App.Services;

public sealed class AcpOAuthDeviceFlowService : IAcpOAuthDeviceFlowService
{
    private readonly HttpClient _httpClient;

    public AcpOAuthDeviceFlowService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<AcpDeviceCodeResponse> StartDeviceFlowAsync(
        string clientId,
        string scope,
        string deviceCodeUrl,
        CancellationToken ct)
    {
        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["scope"] = scope
        });

        using HttpResponseMessage response = await _httpClient.PostAsync(deviceCodeUrl, content, ct)
            .ConfigureAwait(false);
        string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        EnsureSuccess(response, json);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        return new AcpDeviceCodeResponse(
            RequireString(root, "device_code"),
            RequireString(root, "user_code"),
            RequireString(root, "verification_uri"),
            TryGetString(root, "verification_uri_complete"),
            RequireInt(root, "expires_in"),
            TryGetInt(root, "interval") ?? 5);
    }

    public async Task<AcpTokenResponse> CompleteDeviceFlowAsync(
        string clientId,
        string deviceCode,
        int intervalSeconds,
        string tokenUrl,
        CancellationToken ct)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        while (!ct.IsCancellationRequested)
        {
            using FormUrlEncodedContent content = new(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["device_code"] = deviceCode
            });

            using HttpResponseMessage response = await _httpClient.PostAsync(tokenUrl, content, ct)
                .ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return ParseTokenResponse(json);
            }

            if (TryReadDeviceFlowError(json, out string? error))
            {
                if (string.Equals(error, "authorization_pending", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct).ConfigureAwait(false);
                    continue;
                }

                if (string.Equals(error, "slow_down", StringComparison.OrdinalIgnoreCase))
                {
                    intervalSeconds = Math.Max(5, intervalSeconds + 2);
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct).ConfigureAwait(false);
                    continue;
                }

                throw new InvalidOperationException("OAuth device flow failed: " + error);
            }

            EnsureSuccess(response, json);
        }

        throw new OperationCanceledException();
    }

    public async Task<AcpTokenResponse> RefreshTokenAsync(
        string clientId,
        string refreshToken,
        string tokenUrl,
        CancellationToken ct)
    {
        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        using HttpResponseMessage response = await _httpClient.PostAsync(tokenUrl, content, ct)
            .ConfigureAwait(false);
        string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        EnsureSuccess(response, json);
        return ParseTokenResponse(json);
    }

    private static AcpTokenResponse ParseTokenResponse(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        return new AcpTokenResponse(
            RequireString(root, "access_token"),
            TryGetString(root, "refresh_token"),
            TryGetInt(root, "expires_in") ?? 3600,
            TryGetString(root, "token_type"));
    }

    private static bool TryReadDeviceFlowError(string json, out string? error)
    {
        error = null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            error = TryGetString(root, "error");
            return !string.IsNullOrWhiteSpace(error);
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string message = "OAuth request failed (" + (int)response.StatusCode + ")";
        if (!string.IsNullOrWhiteSpace(body))
        {
            message += ": " + body;
        }

        throw new InvalidOperationException(message);
    }

    private static string RequireString(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            string? text = value.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        throw new InvalidOperationException("OAuth response missing " + name + ".");
    }

    private static string? TryGetString(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static int RequireInt(JsonElement element, string name)
    {
        int? value = TryGetInt(element, name);
        if (value is null)
        {
            throw new InvalidOperationException("OAuth response missing " + name + ".");
        }

        return value.Value;
    }

    private static int? TryGetInt(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int result))
        {
            return result;
        }

        return null;
    }
}
