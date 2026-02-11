namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Provides available extension packages.</summary>
public interface IExtensionPackageCatalog
{
    /// <summary>Gets available packages.</summary>
    Task<IReadOnlyList<ExtensionPackageInfo>> GetAvailableAsync(CancellationToken ct);
}
