using System;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting.Mcp;

namespace XamlVisualEditor.McpExtension;

public sealed class McpExtension : IXveExtension
{
    private const string ViewId = "mcp.panel";
    private McpPanelViewModel? _viewModel;
    private McpRuntimeController? _controller;

    public async Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        McpPermissionService permissions = new(context.Settings, context.Window);
        _controller = new McpRuntimeController(
            context.Settings,
            permissions,
            context.Commands,
            context.Workspace,
            context.WorkspaceInfo,
            context.Window,
            context.Editor,
            context.Diagnostics,
            context.Terminal);

        await _controller.InitializeAsync(cancellationToken).ConfigureAwait(false);

        _viewModel = new McpPanelViewModel(_controller, permissions, context.WorkspaceInfo);

        ExtensionViewContribution view = new(
            ViewId,
            "MCP",
            ExtensionViewType.Custom,
            ExtensionViewLocation.Right,
            36);

        IDisposable viewRegistration = context.Contributions.RegisterViews(context.ExtensionId, new[] { view });
        IDisposable providerRegistration = context.Views.RegisterCustomViewProvider(ViewId, new McpViewProvider(_viewModel));

        context.Subscriptions.Add(viewRegistration);
        context.Subscriptions.Add(providerRegistration);
        context.Subscriptions.Add(Disposable.Create(() => _viewModel?.Dispose()));
        context.Subscriptions.Add(new AsyncDisposableAdapter(_controller));
    }

    private sealed class McpViewProvider : ICustomViewProvider
    {
        private readonly McpPanelViewModel _viewModel;

        public McpViewProvider(McpPanelViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public object? CreateViewModel()
        {
            return _viewModel;
        }
    }

    private sealed class AsyncDisposableAdapter : IDisposable
    {
        private readonly IAsyncDisposable? _target;

        public AsyncDisposableAdapter(IAsyncDisposable? target)
        {
            _target = target;
        }

        public void Dispose()
        {
            if (_target is null)
            {
                return;
            }

            _ = _target.DisposeAsync();
        }
    }
}
