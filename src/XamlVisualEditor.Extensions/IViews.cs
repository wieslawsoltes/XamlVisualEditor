namespace XamlVisualEditor.Extensions;

/// <summary>Registers view providers.</summary>
public interface IViews
{
    /// <summary>Registers a tree data provider.</summary>
    IDisposable RegisterTreeDataProvider<T>(string viewId, ITreeDataProvider<T> provider);

    /// <summary>Registers a webview provider.</summary>
    IDisposable RegisterWebviewViewProvider(string viewId, IWebviewViewProvider provider);

    /// <summary>Registers a custom view provider.</summary>
    IDisposable RegisterCustomViewProvider(string viewId, ICustomViewProvider provider);
}

/// <summary>Provides tree data for a view.</summary>
public interface ITreeDataProvider<T>
{
    /// <summary>Gets children of a node.</summary>
    Task<IReadOnlyList<T>> GetChildrenAsync(T? element, CancellationToken cancellationToken);

    /// <summary>Gets a tree item descriptor.</summary>
    Task<TreeItem> GetTreeItemAsync(T element, CancellationToken cancellationToken);

    /// <summary>Raised when data changes.</summary>
    event EventHandler? Changed;
}

/// <summary>Describes a tree item.</summary>
public sealed record TreeItem(string Label, string? Description, string? ContextValue);

/// <summary>Provides a webview view.</summary>
public interface IWebviewViewProvider
{
    /// <summary>Resolves the webview view.</summary>
    Task ResolveAsync(WebviewView view, CancellationToken cancellationToken);
}

/// <summary>Provides a custom view model.</summary>
public interface ICustomViewProvider
{
    /// <summary>Creates the view model instance.</summary>
    object? CreateViewModel();
}

/// <summary>Represents a webview-hosted view.</summary>
public sealed class WebviewView
{
    /// <summary>Creates a webview view.</summary>
    public WebviewView(IWebview webview)
    {
        Webview = webview;
    }

    /// <summary>Gets the webview surface.</summary>
    public IWebview Webview { get; }
}

/// <summary>Represents a webview surface.</summary>
public interface IWebview
{
    /// <summary>Gets or sets HTML content.</summary>
    string Html { get; set; }

    /// <summary>Posts a message to the webview.</summary>
    Task PostMessageAsync(object message, CancellationToken cancellationToken);

    /// <summary>Raised when a message is received from the webview.</summary>
    event EventHandler<object> MessageReceived;
}
