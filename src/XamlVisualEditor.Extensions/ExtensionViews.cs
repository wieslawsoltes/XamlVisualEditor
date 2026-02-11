using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Extensions;

public interface IExtensionViewRegistry : IViews
{
    event EventHandler<ExtensionViewRegistryChangedEventArgs>? Changed;

    bool TryGetTreeProvider(string viewId, out IExtensionTreeDataProvider provider);

    bool TryGetWebviewProvider(string viewId, out IWebviewViewProvider provider);

    bool TryGetCustomViewProvider(string viewId, out ICustomViewProvider provider);
}

public sealed class ExtensionViewRegistryChangedEventArgs : EventArgs
{
    public ExtensionViewRegistryChangedEventArgs(string viewId)
    {
        ViewId = viewId;
    }

    public string ViewId { get; }
}

public interface IExtensionTreeDataProvider
{
    Task<IReadOnlyList<object>> GetChildrenAsync(object? element, CancellationToken cancellationToken);

    Task<TreeItem> GetTreeItemAsync(object element, CancellationToken cancellationToken);

    event EventHandler? Changed;
}
