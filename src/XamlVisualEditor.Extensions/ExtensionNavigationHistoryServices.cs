namespace XamlVisualEditor.Extensions;

/// <summary>Provides navigation history services.</summary>
public interface INavigationHistoryService
{
    /// <summary>Gets whether back navigation is available.</summary>
    bool CanNavigateBack { get; }

    /// <summary>Gets whether forward navigation is available.</summary>
    bool CanNavigateForward { get; }

    /// <summary>Raised when navigation history state changes.</summary>
    event EventHandler<NavigationHistoryChangedEventArgs>? HistoryChanged;

    /// <summary>Navigates back in history.</summary>
    Task<bool> NavigateBackAsync(CancellationToken ct);

    /// <summary>Navigates forward in history.</summary>
    Task<bool> NavigateForwardAsync(CancellationToken ct);
}

/// <summary>Provides navigation history change data.</summary>
public sealed class NavigationHistoryChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public NavigationHistoryChangedEventArgs(bool canNavigateBack, bool canNavigateForward)
    {
        CanNavigateBack = canNavigateBack;
        CanNavigateForward = canNavigateForward;
    }

    /// <summary>Gets whether back navigation is available.</summary>
    public bool CanNavigateBack { get; }

    /// <summary>Gets whether forward navigation is available.</summary>
    public bool CanNavigateForward { get; }
}
