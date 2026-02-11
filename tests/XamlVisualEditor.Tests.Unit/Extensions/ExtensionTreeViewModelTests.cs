using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionTreeViewModelTests
{
    [Fact]
    public async Task LoadAsync_PopulatesRootItems()
    {
        var contribution = new ExtensionViewContribution(
            "view.tree",
            "Tree",
            ExtensionViewType.Tree,
            ExtensionViewLocation.Left,
            0);
        var provider = new TestExtensionTreeProvider();
        var viewModel = new ExtensionTreeViewModel(contribution, provider);

        await viewModel.LoadAsync(CancellationToken.None);

        Assert.Single(viewModel.RootItems);
        Assert.Equal("root", viewModel.RootItems[0].Label);
    }

    [Fact]
    public async Task EnsureChildrenAsync_LoadsChildren()
    {
        var contribution = new ExtensionViewContribution(
            "view.tree",
            "Tree",
            ExtensionViewType.Tree,
            ExtensionViewLocation.Left,
            0);
        var provider = new TestExtensionTreeProvider();
        var viewModel = new ExtensionTreeViewModel(contribution, provider);

        await viewModel.LoadAsync(CancellationToken.None);
        ExtensionTreeNodeViewModel root = viewModel.RootItems[0];

        await root.EnsureChildrenAsync(CancellationToken.None);

        Assert.Single(root.Children);
        Assert.Equal("child", root.Children[0].Label);
    }

    private sealed class TestExtensionTreeProvider : IExtensionTreeDataProvider
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public Task<IReadOnlyList<object>> GetChildrenAsync(object? element, CancellationToken cancellationToken)
        {
            if (element is null)
            {
                return Task.FromResult<IReadOnlyList<object>>(new object[] { "root" });
            }

            if (element is string value && value == "root")
            {
                return Task.FromResult<IReadOnlyList<object>>(new object[] { "child" });
            }

            return Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());
        }

        public Task<TreeItem> GetTreeItemAsync(object element, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TreeItem(element.ToString() ?? string.Empty, null, null));
        }
    }
}
