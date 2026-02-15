using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.TreeInspectorExtension;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class TreeInspectorPanelViewModelTests
{
    [Fact]
    public async Task Refresh_PreservesExpandedAndSelectedNodes_ByStableNodeId()
    {
        string rootId = Guid.NewGuid().ToString("D");
        string childId = Guid.NewGuid().ToString("D");

        FakeDesignerHost designer = new();
        designer.VisualTree = CreateTree(rootId, childId, "Initial child");
        designer.SelectedNodes = new[] { designer.VisualTree[1] };

        using TreeInspectorPanelViewModel viewModel = new(designer, TreeKind.Visual);
        await viewModel.InitializeAsync(CancellationToken.None);

        TreeInspectorNodeViewModel root = Assert.Single(viewModel.TreeModel.RootItems);
        root.IsExpanded = true;
        TreeInspectorNodeViewModel child = Assert.Single(root.Children);
        viewModel.TreeModel.SelectNode(child);

        designer.VisualTree = CreateTree(rootId, childId, "Updated child");
        designer.SelectedNodes = new[] { designer.VisualTree[1] };

        await viewModel.RefreshAsync(CancellationToken.None);

        TreeInspectorNodeViewModel refreshedRoot = Assert.Single(viewModel.TreeModel.RootItems);
        Assert.True(refreshedRoot.IsExpanded);
        Assert.Equal(childId, viewModel.TreeModel.SelectedNode?.NodeId);
    }

    [Fact]
    public async Task Selection_SynchronizesBothWays_BetweenTreeAndDesigner()
    {
        string rootId = Guid.NewGuid().ToString("D");
        string childId = Guid.NewGuid().ToString("D");

        FakeDesignerHost designer = new();
        designer.VisualTree = CreateTree(rootId, childId, "Child");
        designer.SelectedNodes = Array.Empty<DesignerNodeSummary>();

        using TreeInspectorPanelViewModel viewModel = new(designer, TreeKind.Visual);
        await viewModel.InitializeAsync(CancellationToken.None);

        TreeInspectorNodeViewModel root = Assert.Single(viewModel.TreeModel.RootItems);
        TreeInspectorNodeViewModel child = Assert.Single(root.Children);
        viewModel.TreeModel.SelectNode(child);

        await WaitForConditionAsync(
            () => string.Equals(designer.LastSelectedNodeId, childId, StringComparison.OrdinalIgnoreCase),
            TimeSpan.FromSeconds(1));

        viewModel.UpdateSelection(new[] { designer.VisualTree[0] });
        Assert.Equal(rootId, viewModel.TreeModel.SelectedNode?.NodeId);
    }

    private static IReadOnlyList<DesignerNodeSummary> CreateTree(string rootId, string childId, string childDisplayName)
    {
        return new[]
        {
            new DesignerNodeSummary(rootId, "Grid", "Root", null, 1),
            new DesignerNodeSummary(childId, "Button", childDisplayName, rootId, 0)
        };
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset end = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < end)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), "Timed out while waiting for condition.");
    }

    private sealed class FakeDesignerHost : IDesignerHost
    {
        private string? _activeDocumentPath = "/tmp/Test.axaml";

        public string? ActiveDocumentPath
        {
            get => _activeDocumentPath;
            set
            {
                if (string.Equals(_activeDocumentPath, value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _activeDocumentPath = value;
                ActiveDocumentChanged?.Invoke(this, new DesignerDocumentChangedEventArgs(_activeDocumentPath));
            }
        }

        public IReadOnlyList<DesignerNodeSummary> VisualTree { get; set; } = Array.Empty<DesignerNodeSummary>();

        public IReadOnlyList<DesignerNodeSummary> LogicalTree { get; set; } = Array.Empty<DesignerNodeSummary>();

        public IReadOnlyList<DesignerNodeSummary> SelectedNodes { get; set; } = Array.Empty<DesignerNodeSummary>();

        public string? LastSelectedNodeId { get; private set; }

        public event EventHandler<DesignerDocumentChangedEventArgs>? ActiveDocumentChanged;

        public event EventHandler<DesignerSelectionChangedEventArgs>? SelectionChanged;

        public Task<IReadOnlyList<DesignerNodeSummary>> GetSelectedNodesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(SelectedNodes);
        }

        public Task<IReadOnlyList<DesignerNodeSummary>> GetVisualTreeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(VisualTree);
        }

        public Task<IReadOnlyList<DesignerNodeSummary>> GetLogicalTreeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(LogicalTree);
        }

        public Task<IReadOnlyList<DesignerPropertyInfo>> GetPropertiesAsync(string nodeId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<DesignerPropertyInfo>>(Array.Empty<DesignerPropertyInfo>());
        }

        public Task<IReadOnlyList<DesignerEventInfo>> GetEventsAsync(string nodeId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<DesignerEventInfo>>(Array.Empty<DesignerEventInfo>());
        }

        public Task<bool> SetPropertyAsync(string nodeId, string propertyName, string? value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }

        public Task<string?> InsertElementAsync(
            string typeName,
            string xmlNamespace,
            string? parentNodeId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(null);
        }

        public Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }

        public Task<string?> WrapNodeAsync(
            string nodeId,
            string wrapperTypeName,
            string wrapperXmlNamespace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(null);
        }

        public Task<bool> SelectNodeAsync(string nodeId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSelectedNodeId = nodeId;
            DesignerNodeSummary? selected = VisualTree.FirstOrDefault(node =>
                string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
            SelectedNodes = selected is null ? Array.Empty<DesignerNodeSummary>() : new[] { selected };
            SelectionChanged?.Invoke(this, new DesignerSelectionChangedEventArgs(SelectedNodes));
            return Task.FromResult(true);
        }

        public Task<bool> RevealNodeAsync(string nodeId, CancellationToken cancellationToken)
        {
            return SelectNodeAsync(nodeId, cancellationToken);
        }

        public IDesignerTransaction BeginTransaction(string name)
        {
            return new NoOpDesignerTransaction();
        }

        private sealed class NoOpDesignerTransaction : IDesignerTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public Task RollbackAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public void Dispose()
            {
            }
        }
    }
}
