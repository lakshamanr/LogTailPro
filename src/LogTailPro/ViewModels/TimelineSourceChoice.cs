namespace LogTailPro.ViewModels;

/// <summary>One file tab offered by the timeline source picker.</summary>
public sealed class TimelineSourceChoice(LogTabViewModel tab)
{
    /// <summary>Source tab identity.</summary>
    public LogTabViewModel Tab { get; } = tab;

    /// <summary>Whether the source is included; all sources start selected.</summary>
    public bool IsSelected { get; set; } = true;
}
