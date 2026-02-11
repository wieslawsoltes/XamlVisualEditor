namespace XamlVisualEditor.Extensions;

/// <summary>Stores extension-scoped values.</summary>
public interface IExtensionStorage
{
    /// <summary>Gets a stored value.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);

    /// <summary>Stores a value.</summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken);

    /// <summary>Removes a stored value.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}
