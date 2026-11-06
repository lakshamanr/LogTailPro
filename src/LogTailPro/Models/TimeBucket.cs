using System.ComponentModel;

namespace LogTailPro.Models;

/// <summary>
/// Normalized severity categories for the timeline error chart.
/// </summary>
public enum LogSeverity
{
    Error,
    Warning,
    Information,
    Other
}

/// <summary>
/// Aggregated event counts for a specific time interval in the error timeline chart.
/// </summary>
public sealed class TimeBucket : INotifyPropertyChanged
{
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }

    /// <summary>Short label for x-axis display (e.g. '14:30' or '10-24').</summary>
    public string TimeLabel { get; set; } = string.Empty;

    /// <summary>Detailed tooltip text listing exact counts per severity.</summary>
    public string ToolTipText { get; set; } = string.Empty;

    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public int OtherCount { get; set; }

    /// <summary>Total count of distinct root events in this time interval.</summary>
    public int TotalCount => ErrorCount + WarningCount + InfoCount + OtherCount;

    /// <summary>Maximum bucket total across the active dataset for proportional bar scaling.</summary>
    public int MaxBucketTotal { get; set; } = 1;

    public double ErrorHeightRatio => MaxBucketTotal > 0 ? (double)ErrorCount / MaxBucketTotal : 0.0;
    public double WarningHeightRatio => MaxBucketTotal > 0 ? (double)WarningCount / MaxBucketTotal : 0.0;
    public double InfoHeightRatio => MaxBucketTotal > 0 ? (double)InfoCount / MaxBucketTotal : 0.0;
    public double OtherHeightRatio => MaxBucketTotal > 0 ? (double)OtherCount / MaxBucketTotal : 0.0;
    public double TotalHeightRatio => MaxBucketTotal > 0 ? (double)TotalCount / MaxBucketTotal : 0.0;

    private bool _isSelected;
    /// <summary>True when this bucket falls within the user's active time-range filter.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
