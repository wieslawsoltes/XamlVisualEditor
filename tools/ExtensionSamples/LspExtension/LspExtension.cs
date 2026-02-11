using XamlVisualEditor.Extensions;

namespace LspExtension;

public sealed class LspExtension : IXveExtension
{
    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        context.Logger.Info("LSP sample extension activated.");
        return Task.CompletedTask;
    }
}
