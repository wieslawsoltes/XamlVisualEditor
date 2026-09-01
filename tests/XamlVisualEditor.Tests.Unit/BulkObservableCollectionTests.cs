using System.Collections.Generic;
using System.Collections.Specialized;
using XamlVisualEditor.PropertyEditorExtension;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class BulkObservableCollectionTests
{
    [Fact]
    public void ReplaceAll_RaisesSingleResetAndReplacesContent()
    {
        BulkObservableCollection<int> collection = new() { 1, 2, 3 };
        List<NotifyCollectionChangedAction> actions = new();
        collection.CollectionChanged += (_, e) => actions.Add(e.Action);

        collection.ReplaceAll(new[] { 4, 5 });

        Assert.Equal(new[] { 4, 5 }, collection);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Reset }, actions);
    }

    [Fact]
    public void ReplaceAll_WithEmptySource_ClearsCollection()
    {
        BulkObservableCollection<string> collection = new() { "a" };

        collection.ReplaceAll(System.Array.Empty<string>());

        Assert.Empty(collection);
    }
}
