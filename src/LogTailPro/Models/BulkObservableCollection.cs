using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LogTailPro.Models;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that can add or remove many items with a single change
/// notification. Adding 250,000 items one by one raises 250,000 UI notifications and freezes the window;
/// a bulk operation raises one.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    private static readonly PropertyChangedEventArgs CountPropArgs = new(nameof(Count));
    private static readonly PropertyChangedEventArgs IndexerPropArgs = new("Item[]");
    private static readonly NotifyCollectionChangedEventArgs ResetArgs = new(NotifyCollectionChangedAction.Reset);

    /// <summary>Appends <paramref name="items"/>; small batches notify per item (keeps scroll position), large ones reset once.</summary>
    public void AppendRange(IReadOnlyList<T> items)
    {
        if (items.Count == 0) return;

        if (items.Count <= 8)
        {
            foreach (T item in items) Add(item);
            return;
        }

        CheckReentrancy();
        if (Items is List<T> list)
        {
            list.Capacity = Math.Max(list.Capacity, list.Count + items.Count);
        }
        foreach (T item in items) Items.Add(item);
        NotifyReset();
    }

    /// <summary>Replaces the entire content with <paramref name="items"/> using a single notification.</summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        if (items is IReadOnlyCollection<T> col && Items is List<T> list)
        {
            list.EnsureCapacity(col.Count);
        }
        foreach (T item in items) Items.Add(item);
        NotifyReset();
    }

    /// <summary>Removes the first <paramref name="count"/> items using a single notification.</summary>
    public void RemoveFirst(int count)
    {
        if (count <= 0) return;
        CheckReentrancy();
        ((List<T>)Items).RemoveRange(0, Math.Min(count, Count));
        NotifyReset();
    }

    private void NotifyReset()
    {
        OnPropertyChanged(CountPropArgs);
        OnPropertyChanged(IndexerPropArgs);
        OnCollectionChanged(ResetArgs);
    }
}
