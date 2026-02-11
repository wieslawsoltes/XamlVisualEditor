using XamlVisualEditor.Extensions;

namespace HelloExtension;

public sealed class HelloExtension : IXveExtension
{
    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        IDisposable commandRegistration = context.Commands.Register(
            "hello.showMessage",
            async _ => await context.Window.ShowInformationMessageAsync(
                "Hello from XamlVisualEditor extension.",
                cancellationToken));

        IDisposable treeRegistration = context.Views.RegisterTreeDataProvider(
            "hello.view",
            new HelloTreeProvider());

        context.Subscriptions.Add(commandRegistration);
        context.Subscriptions.Add(treeRegistration);

        return Task.CompletedTask;
    }

    private sealed class HelloTreeProvider : ITreeDataProvider<string>
    {
        public event EventHandler? Changed;

        public Task<IReadOnlyList<string>> GetChildrenAsync(string? element, CancellationToken ct)
        {
            if (element is null)
            {
                return Task.FromResult<IReadOnlyList<string>>(new[] { "Hello" });
            }

            if (string.Equals(element, "Hello", StringComparison.Ordinal))
            {
                return Task.FromResult<IReadOnlyList<string>>(new[] { "World" });
            }

            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task<TreeItem> GetTreeItemAsync(string element, CancellationToken ct)
        {
            return Task.FromResult(new TreeItem(element, null, null));
        }

        public void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
