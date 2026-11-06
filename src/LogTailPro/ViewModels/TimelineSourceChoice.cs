using CommunityToolkit.Mvvm.ComponentModel;

namespace LogTailPro.ViewModels;

/// <summary>One file tab offered by the timeline or correlation source picker.</summary>
public sealed partial class TimelineSourceChoice : ObservableObject
{
    public TimelineSourceChoice(LogTabViewModel tab)
    {
        Tab = tab;
    }

    /// <summary>Source tab identity.</summary>
    public LogTabViewModel Tab { get; }

    /// <summary>Whether the source is included; all sources start selected.</summary>
    [ObservableProperty]
    private bool _isSelected = true;
}
