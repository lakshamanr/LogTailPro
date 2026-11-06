using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;

namespace LogTailPro.ViewModels;

/// <summary>One selectable row in the log-discovery picker.</summary>
public sealed partial class LogCandidateItem : ObservableObject
{
    /// <summary>Wraps a discovered log file.</summary>
    public LogCandidateItem(LogCandidate candidate, bool isChecked)
    {
        Candidate = candidate;
        _isChecked = isChecked;
    }

    /// <summary>The discovered file.</summary>
    public LogCandidate Candidate { get; }

    /// <summary>Whether the file will be opened.</summary>
    [ObservableProperty] private bool _isChecked;

    /// <summary>Group header text ("name [pid]").</summary>
    public string ProcessText => Candidate.ProcessText;

    /// <summary>Marker shown next to the best match of each process.</summary>
    public string RecommendedMark => Candidate.IsRecommended ? "★" : string.Empty;
}

/// <summary>ViewModel of the dialog that lets the user choose which discovered log files to open.</summary>
public sealed partial class LogDiscoveryViewModel : ObservableObject
{
    /// <summary>Builds the picker; the recommended candidate of each process is pre-checked.</summary>
    public LogDiscoveryViewModel(IReadOnlyList<LogCandidate> candidates, string title)
    {
        Title = title;
        Items = new ObservableCollection<LogCandidateItem>(
            candidates.Select(c => new LogCandidateItem(c, c.IsRecommended)));

        foreach (LogCandidateItem item in Items)
            item.PropertyChanged += OnItemChanged;

        ItemsView = CollectionViewSource.GetDefaultView(Items);
        if (candidates.Select(c => c.Pid).Distinct().Count() > 1)
            ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(LogCandidateItem.ProcessText)));
    }

    /// <summary>Header text, e.g. "Log files for w3wp [1234]".</summary>
    public string Title { get; }

    /// <summary>All discovered files.</summary>
    public ObservableCollection<LogCandidateItem> Items { get; }

    /// <summary>Grouped view over <see cref="Items"/> (grouped by process when several are shown).</summary>
    public ICollectionView ItemsView { get; }

    /// <summary>Number of checked files.</summary>
    public int CheckedCount => Items.Count(i => i.IsChecked);

    /// <summary>Caption of the confirm button.</summary>
    public string OpenButtonText => CheckedCount switch
    {
        0 => "Open Selected",
        1 => "Open 1 file",
        _ => $"Open {CheckedCount} files"
    };

    /// <summary>Paths of the checked files, in list order.</summary>
    public IReadOnlyList<string> SelectedPaths =>
        Items.Where(i => i.IsChecked).Select(i => i.Candidate.Path).ToList();

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LogCandidateItem.IsChecked)) return;
        OnPropertyChanged(nameof(CheckedCount));
        OnPropertyChanged(nameof(OpenButtonText));
    }

    /// <summary>Checks every file.</summary>
    [RelayCommand]
    private void SelectAll()
    {
        foreach (LogCandidateItem i in Items) i.IsChecked = true;
    }

    /// <summary>Unchecks every file.</summary>
    [RelayCommand]
    private void SelectNone()
    {
        foreach (LogCandidateItem i in Items) i.IsChecked = false;
    }
}
