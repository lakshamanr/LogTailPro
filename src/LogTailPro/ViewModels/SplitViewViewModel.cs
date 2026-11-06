using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LogTailPro.ViewModels;

/// <summary>One selectable log tab in the Split View dialog.</summary>
public sealed partial class SplitTabChoice : ObservableObject
{
    /// <summary>Creates a choice for <paramref name="tab"/>.</summary>
    public SplitTabChoice(LogTabViewModel tab) => Tab = tab;

    /// <summary>The log tab this choice represents.</summary>
    public LogTabViewModel Tab { get; }

    /// <summary>Display name (file name).</summary>
    public string Name => Tab.FileName;

    /// <summary>True when the user wants this tab moved into the second group.</summary>
    [ObservableProperty] private bool _isSelected;
}

/// <summary>ViewModel for the Split View dialog: which tabs go into the new group and how it is laid out.</summary>
public sealed partial class SplitViewViewModel : ObservableObject
{
    /// <summary>Creates the VM; every tab except <paramref name="current"/> can be picked.</summary>
    public SplitViewViewModel(IEnumerable<LogTabViewModel> tabs, LogTabViewModel? current)
    {
        CurrentName = current?.FileName ?? string.Empty;
        foreach (LogTabViewModel t in tabs.Where(t => !ReferenceEquals(t, current)))
            Choices.Add(new SplitTabChoice(t));

        // Convenience: with exactly one other tab, pre-select it.
        if (Choices.Count == 1) Choices[0].IsSelected = true;
    }

    /// <summary>Name of the tab that stays in the first group.</summary>
    public string CurrentName { get; }

    /// <summary>The other open tabs the user can move into the second group.</summary>
    public ObservableCollection<SplitTabChoice> Choices { get; } = new();

    /// <summary>True = side by side (vertical divider); false = stacked (top/bottom).</summary>
    [ObservableProperty] private bool _isSideBySide = true;

    /// <summary>Tabs the user ticked.</summary>
    public IReadOnlyList<LogTabViewModel> SelectedTabs
        => Choices.Where(c => c.IsSelected).Select(c => c.Tab).ToList();
}
