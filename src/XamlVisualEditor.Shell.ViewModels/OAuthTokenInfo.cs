namespace XamlVisualEditor.Shell.ViewModels;

internal sealed record OAuthTokenInfo(string AccessToken, string? RefreshToken, System.DateTimeOffset? ExpiresAt);
