namespace XamlVisualEditor.AcpExtension;

internal sealed record OAuthTokenInfo(string AccessToken, string? RefreshToken, System.DateTimeOffset? ExpiresAt);
