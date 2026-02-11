using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.GitExtension;

public sealed class GitExtension : IXveExtension
{
    private const string ViewId = "git.panel";
    private readonly IGitService _gitService;
    private readonly IWorkspaceInfo _workspaceInfo;

    public GitExtension(IGitService gitService, IWorkspaceInfo workspaceInfo)
    {
        _gitService = gitService;
        _workspaceInfo = workspaceInfo;
    }

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        ExtensionViewContribution view = new(
            ViewId,
            "Git",
            ExtensionViewType.Custom,
            ExtensionViewLocation.Bottom,
            40);

        IDisposable viewRegistration = context.Contributions.RegisterViews(
            context.ExtensionId,
            new[] { view });
        IDisposable providerRegistration = context.Views.RegisterCustomViewProvider(
            ViewId,
            new GitPanelViewProvider(_gitService, _workspaceInfo));

        context.Subscriptions.Add(viewRegistration);
        context.Subscriptions.Add(providerRegistration);

        return Task.CompletedTask;
    }

    private sealed class GitPanelViewProvider : ICustomViewProvider
    {
        private readonly IGitService _gitService;
        private readonly IWorkspaceInfo _workspaceInfo;

        public GitPanelViewProvider(IGitService gitService, IWorkspaceInfo workspaceInfo)
        {
            _gitService = gitService;
            _workspaceInfo = workspaceInfo;
        }

        public object? CreateViewModel()
        {
            return new GitPanelViewModel(_gitService, _workspaceInfo);
        }
    }
}
