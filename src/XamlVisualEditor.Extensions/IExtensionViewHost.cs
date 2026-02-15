using System;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Extensions;

/// <summary>Allows extensions to show, toggle, or query view visibility.</summary>
public interface IExtensionViewHost
{
    /// <summary>Raised when extension view visibility changes.</summary>
    event EventHandler<ExtensionViewVisibilityChangedEventArgs>? VisibilityChanged;

    /// <summary>Raised when extension view focus changes.</summary>
    event EventHandler<ExtensionViewFocusChangedEventArgs>? FocusChanged;

    /// <summary>Shows a view by id.</summary>
    Task ShowAsync(string viewId, CancellationToken cancellationToken);

    /// <summary>Toggles a view by id.</summary>
    Task ToggleAsync(string viewId, CancellationToken cancellationToken);

    /// <summary>Checks if a view is visible.</summary>
    Task<bool> IsVisibleAsync(string viewId, CancellationToken cancellationToken);

    /// <summary>Activates a view by id without changing visibility.</summary>
    Task ActivateAsync(string viewId, CancellationToken cancellationToken);
}

/// <summary>Describes extension view visibility state.</summary>
public sealed class ExtensionViewVisibilityChangedEventArgs : EventArgs
{
    public ExtensionViewVisibilityChangedEventArgs(string viewId, bool isVisible)
    {
        ViewId = viewId;
        IsVisible = isVisible;
    }

    public string ViewId { get; }

    public bool IsVisible { get; }
}

/// <summary>Describes extension view focus state.</summary>
public sealed class ExtensionViewFocusChangedEventArgs : EventArgs
{
    public ExtensionViewFocusChangedEventArgs(string viewId, bool isFocused)
    {
        ViewId = viewId;
        IsFocused = isFocused;
    }

    public string ViewId { get; }

    public bool IsFocused { get; }
}
