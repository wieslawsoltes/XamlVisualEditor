namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Checks for extension updates.</summary>
public interface IExtensionUpdateService
{
    /// <summary>Checks for updates.</summary>
    Task<IReadOnlyList<ExtensionUpdateInfo>> CheckForUpdatesAsync(CancellationToken ct);
}

/// <summary>Represents an available extension update.</summary>
public sealed record ExtensionUpdateInfo(ExtensionPackageInfo Installed, ExtensionPackageInfo Available);
