using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;
using LogTailPro.Services;

namespace LogTailPro.ViewModels;

/// <summary>
/// View-model for the interactive error timeline chart.
/// Provides data binding for adaptive time buckets, severity breakdown, undated record statistics,
/// and click/drag time-range filtering.
/// </summary>
public sealed partial class ErrorTimelineChartViewModel : ObservableObject
{
    [ObservableProperty] private BulkObservableCollection<TimeBucket> _buckets = new();
    [ObservableProperty] private string _sourceScope = string.Empty;
    [ObservableProperty] private string _timeRangeDescription = string.Empty;
    [ObservableProperty] private string _bucketSizeDescription = string.Empty;
    [ObservableProperty] private string _timezoneDescription = string.Empty;

    [ObservableProperty] private int _totalEventCount;
    [ObservableProperty] private int _errorCount;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private int _infoCount;
    [ObservableProperty] private int _otherCount;

    [ObservableProperty] private int _undatedCount;
    [ObservableProperty] private bool _hasUndatedEvents;
    [ObservableProperty] private string _undatedDescription = string.Empty;

    [ObservableProperty] private DateTimeOffset? _selectedStartTime;
    [ObservableProperty] private DateTimeOffset? _selectedEndTime;
    [ObservableProperty] private bool _hasTimeRangeFilter;
    [ObservableProperty] private string _selectionSummary = string.Empty;

    [ObservableProperty] private int _selectedBucketIndex = -1;

    /// <summary>Raised when the user changes or clears the active time-range filter.</summary>
    public event EventHandler? TimeRangeFilterChanged;

    /// <summary>Raised when the user requests navigation to events at a specific timestamp.</summary>
    public event EventHandler<DateTimeOffset>? NavigateToTimestampRequested;

    /// <summary>Populates the view-model from service aggregation output.</summary>
    public void SetData(ErrorTimelineChartData data)
    {
        SourceScope = data.SourceScope;
        BucketSizeDescription = data.BucketIntervalDescription;
        TimezoneDescription = data.TimezoneDescription;

        TotalEventCount = data.TotalEvents;
        ErrorCount = data.TotalErrors;
        WarningCount = data.TotalWarnings;
        InfoCount = data.TotalInfos;
        OtherCount = data.TotalOthers;

        UndatedCount = data.UndatedCount;
        HasUndatedEvents = data.UndatedCount > 0;
        UndatedDescription = data.UndatedCount > 0
            ? $"{data.UndatedCount:N0} undated events ({data.UndatedErrors:N0} err, {data.UndatedWarnings:N0} warn) not on timeline"
            : string.Empty;

        if (data.StartTime.HasValue && data.EndTime.HasValue)
        {
            string startStr = data.StartTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string endStr = data.EndTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            TimeRangeDescription = $"{startStr} – {endStr}";
        }
        else
        {
            TimeRangeDescription = data.TotalEvents > 0 ? "Undated stream" : "No events loaded";
        }

        // Re-apply selection state to new buckets
        if (HasTimeRangeFilter && SelectedStartTime.HasValue && SelectedEndTime.HasValue)
        {
            DateTimeOffset s = SelectedStartTime.Value;
            DateTimeOffset e = SelectedEndTime.Value;
            foreach (TimeBucket b in data.Buckets)
            {
                b.IsSelected = b.EndTime >= s && b.StartTime <= e;
            }
        }

        Buckets.ReplaceAll(data.Buckets);
    }

    /// <summary>Filters the view to events within the specified time bucket.</summary>
    [RelayCommand]
    public void SelectBucket(TimeBucket? bucket)
    {
        if (bucket == null) return;

        // If clicking the already selected single bucket, toggle clear
        if (HasTimeRangeFilter && SelectedStartTime == bucket.StartTime && SelectedEndTime == bucket.EndTime)
        {
            ClearFilter();
            return;
        }

        SelectRange(bucket.StartTime, bucket.EndTime);
    }

    /// <summary>Filters the view to events between <paramref name="start"/> and <paramref name="end"/>.</summary>
    public void SelectRange(DateTimeOffset start, DateTimeOffset end)
    {
        DateTimeOffset actualStart = start <= end ? start : end;
        DateTimeOffset actualEnd = start <= end ? end : start;

        SelectedStartTime = actualStart;
        SelectedEndTime = actualEnd;
        HasTimeRangeFilter = true;

        int matchingCount = 0;
        foreach (TimeBucket b in Buckets)
        {
            b.IsSelected = b.EndTime >= actualStart && b.StartTime <= actualEnd;
            if (b.IsSelected) matchingCount += b.TotalCount;
        }

        string sTime = actualStart.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        string eTime = actualEnd.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        SelectionSummary = $"Filtered: {sTime} – {eTime} ({matchingCount:N0} events)";

        TimeRangeFilterChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears the time-range filter and restores the full event view.</summary>
    [RelayCommand]
    public void ClearFilter()
    {
        if (!HasTimeRangeFilter) return;

        SelectedStartTime = null;
        SelectedEndTime = null;
        HasTimeRangeFilter = false;
        SelectionSummary = string.Empty;

        foreach (TimeBucket b in Buckets)
            b.IsSelected = false;

        TimeRangeFilterChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Navigates to the first event matching the specified bucket timestamp.</summary>
    [RelayCommand]
    public void DrillToFirstEvent(TimeBucket? bucket)
    {
        if (bucket == null) return;
        NavigateToTimestampRequested?.Invoke(this, bucket.StartTime);
    }

    // Keyboard accessibility navigation
    public void SelectPreviousBucket()
    {
        if (Buckets.Count == 0) return;
        int nextIdx = SelectedBucketIndex <= 0 ? Buckets.Count - 1 : SelectedBucketIndex - 1;
        SelectedBucketIndex = nextIdx;
        SelectBucket(Buckets[nextIdx]);
    }

    public void SelectNextBucket()
    {
        if (Buckets.Count == 0) return;
        int nextIdx = SelectedBucketIndex >= Buckets.Count - 1 ? 0 : SelectedBucketIndex + 1;
        SelectedBucketIndex = nextIdx;
        SelectBucket(Buckets[nextIdx]);
    }
}
