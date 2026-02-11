using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting.VscodeCompat;

namespace XamlVisualEditor.VscodeCompatExtension;

public sealed class VscodeCompatExtension : IXveExtension
{
    private VscodeCompatRuntimeController? _controller;

    public async Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        var host = new VscodeCompatHost(
            context.Commands,
            context.Window,
            context.Settings,
            context.Logger);

        _controller = new VscodeCompatRuntimeController(
            context.Settings,
            context.Workspace,
            host);

        await _controller.InitializeAsync(cancellationToken).ConfigureAwait(false);

        context.Subscriptions.Add(new AsyncDisposableAdapter(_controller));
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
