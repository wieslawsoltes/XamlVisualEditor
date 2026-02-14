using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Extensions;

/// <summary>Allows extensions to show, toggle, or query view visibility.</summary>
public interface IExtensionViewHost
{
    /// <summary>Shows a view by id.</summary>
    Task ShowAsync(string viewId, CancellationToken cancellationToken);

    /// <summary>Toggles a view by id.</summary>
    Task ToggleAsync(string viewId, CancellationToken cancellationToken);

    /// <summary>Checks if a view is visible.</summary>
    Task<bool> IsVisibleAsync(string viewId, CancellationToken cancellationToken);

    /// <summary>Activates a view by id without changing visibility.</summary>
    Task ActivateAsync(string viewId, CancellationToken cancellationToken);
}
