using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XamlVisualEditor.Extensions.Hosting;

public sealed class ExtensionViewRegistry : IExtensionViewRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IExtensionTreeDataProvider> _treeProviders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IWebviewViewProvider> _webviewProviders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ICustomViewProvider> _customProviders = new(StringComparer.Ordinal);

    public event EventHandler<ExtensionViewRegistryChangedEventArgs>? Changed;

    public IDisposable RegisterTreeDataProvider<T>(string viewId, ITreeDataProvider<T> provider)
    {
        if (string.IsNullOrWhiteSpace(viewId))
        {
            throw new ArgumentException("View id is required.", nameof(viewId));
        }

        if (provider is null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        var adapter = new TreeDataProviderAdapter<T>(provider);
        lock (_gate)
        {
            _treeProviders[viewId] = adapter;
        }

        Changed?.Invoke(this, new ExtensionViewRegistryChangedEventArgs(viewId));
        return new Registration(() => RemoveTreeProvider(viewId, adapter));
    }

    public IDisposable RegisterWebviewViewProvider(string viewId, IWebviewViewProvider provider)
    {
        if (string.IsNullOrWhiteSpace(viewId))
        {
            throw new ArgumentException("View id is required.", nameof(viewId));
        }

        if (provider is null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        lock (_gate)
        {
            _webviewProviders[viewId] = provider;
        }

        Changed?.Invoke(this, new ExtensionViewRegistryChangedEventArgs(viewId));
        return new Registration(() => RemoveWebviewProvider(viewId, provider));
    }

    public IDisposable RegisterCustomViewProvider(string viewId, ICustomViewProvider provider)
    {
        if (string.IsNullOrWhiteSpace(viewId))
        {
            throw new ArgumentException("View id is required.", nameof(viewId));
        }

        if (provider is null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        lock (_gate)
        {
            _customProviders[viewId] = provider;
        }

        Changed?.Invoke(this, new ExtensionViewRegistryChangedEventArgs(viewId));
        return new Registration(() => RemoveCustomProvider(viewId, provider));
    }

    public bool TryGetTreeProvider(string viewId, out IExtensionTreeDataProvider provider)
    {
        lock (_gate)
        {
            return _treeProviders.TryGetValue(viewId, out provider!);
        }
    }

    public bool TryGetWebviewProvider(string viewId, out IWebviewViewProvider provider)
    {
        lock (_gate)
        {
            return _webviewProviders.TryGetValue(viewId, out provider!);
        }
    }

    public bool TryGetCustomViewProvider(string viewId, out ICustomViewProvider provider)
    {
        lock (_gate)
        {
            return _customProviders.TryGetValue(viewId, out provider!);
        }
    }

    private void RemoveTreeProvider(string viewId, IExtensionTreeDataProvider provider)
    {
        lock (_gate)
        {
            if (_treeProviders.TryGetValue(viewId, out IExtensionTreeDataProvider? current)
                && ReferenceEquals(current, provider))
            {
                _treeProviders.Remove(viewId);
            }
            else
            {
                return;
            }
        }

        Changed?.Invoke(this, new ExtensionViewRegistryChangedEventArgs(viewId));
    }

    private void RemoveWebviewProvider(string viewId, IWebviewViewProvider provider)
    {
        lock (_gate)
        {
            if (_webviewProviders.TryGetValue(viewId, out IWebviewViewProvider? current)
                && ReferenceEquals(current, provider))
            {
                _webviewProviders.Remove(viewId);
            }
            else
            {
                return;
            }
        }

        Changed?.Invoke(this, new ExtensionViewRegistryChangedEventArgs(viewId));
    }

    private void RemoveCustomProvider(string viewId, ICustomViewProvider provider)
    {
        lock (_gate)
        {
            if (_customProviders.TryGetValue(viewId, out ICustomViewProvider? current)
                && ReferenceEquals(current, provider))
            {
                _customProviders.Remove(viewId);
            }
            else
            {
                return;
            }
        }

        Changed?.Invoke(this, new ExtensionViewRegistryChangedEventArgs(viewId));
    }

    private sealed class TreeDataProviderAdapter<T> : IExtensionTreeDataProvider
    {
        private readonly ITreeDataProvider<T> _provider;

        public TreeDataProviderAdapter(ITreeDataProvider<T> provider)
        {
            _provider = provider;
        }

        public event EventHandler? Changed
        {
            add => _provider.Changed += value;
            remove => _provider.Changed -= value;
        }

        public async Task<IReadOnlyList<object>> GetChildrenAsync(object? element, CancellationToken cancellationToken)
        {
            T? typed = element is null
                ? default
                : element is T match
                    ? match
                    : default;

            IReadOnlyList<T> children = await _provider.GetChildrenAsync(typed, cancellationToken).ConfigureAwait(false);
            if (children.Count == 0)
            {
                return Array.Empty<object>();
            }

            List<object> results = new(children.Count);
            foreach (T child in children)
            {
                results.Add(child!);
            }

            return results;
        }

        public Task<TreeItem> GetTreeItemAsync(object element, CancellationToken cancellationToken)
        {
            if (element is not T typed)
            {
                throw new InvalidOperationException("Tree element type mismatch for view provider.");
            }

            return _provider.GetTreeItemAsync(typed, cancellationToken);
        }
    }

    private sealed class Registration : IDisposable
    {
        private readonly Action _dispose;
        private bool _isDisposed;

        public Registration(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _dispose();
            _isDisposed = true;
        }
    }
}

public sealed class NullExtensionTreeDataProvider : IExtensionTreeDataProvider
{
    public static NullExtensionTreeDataProvider Instance { get; } = new();

    private NullExtensionTreeDataProvider()
    {
    }

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public Task<IReadOnlyList<object>> GetChildrenAsync(object? element, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());
    }

    public Task<TreeItem> GetTreeItemAsync(object element, CancellationToken cancellationToken)
    {
        string label = element?.ToString() ?? string.Empty;
        return Task.FromResult(new TreeItem(label, null, null));
    }
}
