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
    /// <summary>Appends <paramref name="items"/>; small batches notify per item (keeps scroll position), large ones reset once.</summary>
    public void AppendRange(IReadOnlyList<T> items)
    {
        if (items.Count == 0) return;

        if (items.Count <= 64)
        {
            foreach (T item in items) Add(item);
            return;
        }

        CheckReentrancy();
        foreach (T item in items) Items.Add(item);
        NotifyReset();
    }

    /// <summary>Replaces the entire content with <paramref name="items"/> using a single notification.</summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
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
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
