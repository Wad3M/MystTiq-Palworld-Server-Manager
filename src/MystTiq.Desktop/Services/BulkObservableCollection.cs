// MystTiq v1.0.7.3: file reviewed for this release (2026-10-11).
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.0.7.3 (roadmap Q-6): an ObservableCollection that can replace its whole content with one change notification. The
/// console's lists were cleared and refilled line by line on every 5-s poll (about 1,000 notifications, each one a layout
/// pass), even when nothing had changed.
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>Replaces every item and raises a single Reset.</summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var item in items) Items.Add(item);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
