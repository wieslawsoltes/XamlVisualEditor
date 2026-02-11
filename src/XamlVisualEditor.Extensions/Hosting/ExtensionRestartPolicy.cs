namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Defines restart limits for crashed extensions.</summary>
public sealed record ExtensionRestartPolicy(int MaxRestarts, TimeSpan Window);
