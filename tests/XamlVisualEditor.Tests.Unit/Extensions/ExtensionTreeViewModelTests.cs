using System;
using System.Collections.Generic;
using System.Reactive.Threading.Tasks;
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

    [Fact]
    public async Task OpenSelectedCommand_InvokesOpenAsync()
    {
        var contribution = new ExtensionViewContribution(
            "view.tree",
            "Tree",
            ExtensionViewType.Tree,
            ExtensionViewLocation.Left,
            0);
        var provider = new OpenableTreeProvider();
        var viewModel = new ExtensionTreeViewModel(contribution, provider);

        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SelectedRow = viewModel.Model.Flattened[0];

        await viewModel.OpenSelectedCommand.Execute().ToTask();

        Assert.True(provider.Opened);
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

    private sealed class OpenableTreeProvider : IExtensionTreeDataProvider
    {
        private readonly OpenableItem _item = new();

        public bool Opened => _item.Opened;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public Task<IReadOnlyList<object>> GetChildrenAsync(object? element, CancellationToken cancellationToken)
        {
            if (element is null)
            {
                return Task.FromResult<IReadOnlyList<object>>(new object[] { _item });
            }

            return Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());
        }

        public Task<TreeItem> GetTreeItemAsync(object element, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TreeItem("openable", null, null));
        }
    }

    private sealed class OpenableItem : IExtensionTreeItemOperationsProvider
    {
        public bool Opened { get; private set; }

        public bool CanOpen => true;

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            Opened = true;
            return Task.CompletedTask;
        }

        public bool CanRename => false;

        public Task RenameAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public bool CanDelete => false;

        public Task DeleteAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public bool CanCreateFile => false;

        public Task CreateFileAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public bool CanCreateFolder => false;

        public Task CreateFolderAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
