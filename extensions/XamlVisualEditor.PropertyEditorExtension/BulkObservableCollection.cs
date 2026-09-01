using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace XamlVisualEditor.PropertyEditorExtension;

/// <summary>
/// Observable collection with a bulk replace that raises a single reset
/// notification. Replacing a large entry set item by item makes a listening
/// collection view process every insert separately, which is quadratic overall
/// and freezes the UI thread for large property sets.
/// </summary>
internal sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (T item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
