using System;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting.IdeBridge;

namespace XamlVisualEditor.IdeBridgeExtension;

public sealed class IdeBridgeExtension : IXveExtension
{
    private const string ViewId = "idebridge.panel";
    private IdeBridgePanelViewModel? _viewModel;
    private IdeBridgeRuntimeController? _controller;

    public async Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        IdeBridgePermissionService permissions = new(context.Settings, context.Window);
        _controller = new IdeBridgeRuntimeController(
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

        _viewModel = new IdeBridgePanelViewModel(_controller, permissions, context.WorkspaceInfo);

        ExtensionViewContribution view = new(
            ViewId,
            "IDE Bridge",
            ExtensionViewType.Custom,
            ExtensionViewLocation.Right,
            35);

        IDisposable viewRegistration = context.Contributions.RegisterViews(context.ExtensionId, new[] { view });
        IDisposable providerRegistration = context.Views.RegisterCustomViewProvider(ViewId, new IdeBridgeViewProvider(_viewModel));

        context.Subscriptions.Add(viewRegistration);
        context.Subscriptions.Add(providerRegistration);
        context.Subscriptions.Add(Disposable.Create(() => _viewModel?.Dispose()));
        context.Subscriptions.Add(new AsyncDisposableAdapter(_controller));
    }

    private sealed class IdeBridgeViewProvider : ICustomViewProvider
    {
        private readonly IdeBridgePanelViewModel _viewModel;

        public IdeBridgeViewProvider(IdeBridgePanelViewModel viewModel)
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
