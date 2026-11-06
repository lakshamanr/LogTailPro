using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;
using LogTailPro.Services;

namespace LogTailPro.ViewModels;

public sealed partial class LogTabViewModel
{
    private readonly List<LogTabViewModel> _timelineSources = new();
    private readonly TimelineService _timelineService = new();
    private readonly CorrelationService _correlationService = new();
    private DispatcherTimer? _timelineTimer;
    private bool _timelineDirty;
    private bool _timelineBuilding;
    private int _timelineRevision;
    private CancellationTokenSource? _timelineCts;

    /// <summary>Raised on the UI thread when the source's loaded records change.</summary>
    public event EventHandler? LinesChanged;

    /// <summary>Raised when a source tab closes.</summary>
    public event EventHandler? Closed;

    /// <summary>Requests navigation back to a timeline row's source.</summary>
    public event EventHandler<LogLine>? OriginalLineRequested;

    /// <summary>Whether this tab is a live combined timeline or correlation view.</summary>
    public bool IsTimeline { get; private init; }

    /// <summary>Whether this tab is a Request/Trace ID correlation tab.</summary>
    public bool IsCorrelationTab { get; private init; }

    /// <summary>The target correlation ID for this tab.</summary>
    public string? CorrelationId { get; private init; }

    /// <summary>Whether this tab can be selected as a file source.</summary>
    public bool IsTimelineSource => !_isReadOnly && !IsProcessTab && TempFilePath == null;

    /// <summary>Active source tabs used by this timeline or correlation tab.</summary>
    public IReadOnlyList<LogTabViewModel> TimelineSources => _timelineSources;

    /// <summary>File paths of the active source tabs used by this timeline or correlation tab.</summary>
    public IReadOnlyList<string> TimelineSourcePaths =>
        _timelineSources.Select(source => source.FilePath).ToArray();

    /// <summary>Width of the dedicated source column; zero in ordinary file tabs.</summary>
    public double TimelineSourceWidth => IsTimeline ? 180 : 0;

    /// <summary>Width of the UTC timestamp column; zero in ordinary file tabs.</summary>
    public double TimelineTimeWidth => IsTimeline ? 245 : 0;

    /// <summary>Creates a live timeline using existing tabs rather than additional file watchers.</summary>
    public LogTabViewModel(IReadOnlyList<LogTabViewModel> sources, AppSettings settings)
        : this("Combined Timeline", Array.Empty<LogLine>(), settings)
    {
        IsTimeline = true;
        IsFollowTail = settings.FollowTailOnOpen;
        FileStatus = FileStatus.Watching;
        _timelineSources.AddRange(sources.Distinct());
        foreach (LogTabViewModel source in _timelineSources)
        {
            source.LinesChanged += OnTimelineSourceChanged;
            source.Closed += OnTimelineSourceClosed;
            source.PropertyChanged += OnTimelineSourcePropertyChanged;
        }
        _timelineTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timelineTimer.Tick += OnTimelineTick;
        _timelineTimer.Start();
        MarkTimelineDirty();
    }

    /// <summary>Creates a live correlation tab filtering across existing tabs by exact correlation ID.</summary>
    public LogTabViewModel(IReadOnlyList<LogTabViewModel> sources, string correlationId, AppSettings settings)
        : this($"Correlate: {correlationId}", Array.Empty<LogLine>(), settings)
    {
        IsTimeline = true;
        IsCorrelationTab = true;
        CorrelationId = correlationId;
        IsFollowTail = settings.FollowTailOnOpen;
        FileStatus = FileStatus.Watching;
        _timelineSources.AddRange(sources.Distinct());
        foreach (LogTabViewModel source in _timelineSources)
        {
            source.LinesChanged += OnTimelineSourceChanged;
            source.Closed += OnTimelineSourceClosed;
            source.PropertyChanged += OnTimelineSourcePropertyChanged;
        }
        _timelineTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timelineTimer.Tick += OnTimelineTick;
        _timelineTimer.Start();
        MarkTimelineDirty();
    }

    private void MarkTimelineDirty()
    {
        _timelineDirty = true;
        _timelineRevision++;
    }

    private void OnTimelineSourceChanged(object? sender, EventArgs args) => MarkTimelineDirty();

    private void OnTimelineSourcePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(WarningMessage) or nameof(FileStatus)) MarkTimelineDirty();
    }

    private void OnTimelineSourceClosed(object? sender, EventArgs args)
    {
        if (sender is not LogTabViewModel source) return;
        DetachTimelineSource(source);
        _timelineSources.Remove(source);
        MarkTimelineDirty();
    }

    private static string TimelinePolicy =>
        "UTC order; times without offsets use local time. Undated/time-only lines appear last. " +
        "Only loaded source lines are included. Source filters do not affect this view.";

    private async void OnTimelineTick(object? sender, EventArgs args)
    {
        if (!_timelineDirty || _timelineBuilding || _disposed || IsWatchPaused) return;
        _timelineDirty = false;
        _timelineBuilding = true;
        int revision = _timelineRevision;
        _timelineCts = new CancellationTokenSource();
        CancellationToken ct = _timelineCts.Token;
        try
        {
            TimelineSource[] snapshot = _timelineSources.Select(source =>
                new TimelineSource(source.FilePath, source.FileName, source.AllLines.ToArray())).ToArray();
            IReadOnlyList<LogLine> rows;
            bool isTruncated = false;
            int totalFound = 0;

            if (IsCorrelationTab && !string.IsNullOrEmpty(CorrelationId))
            {
                CorrelationResult corrResult = await Task.Run(() =>
                    _correlationService.MergeCorrelated(snapshot, CorrelationId, ct: ct), ct);
                rows = corrResult.Rows;
                isTruncated = corrResult.IsTruncated;
                totalFound = corrResult.TotalMatches;
            }
            else
            {
                rows = await Task.Run(() => _timelineService.Merge(snapshot, ct), ct);
            }

            if (_disposed || ct.IsCancellationRequested) return;
            // Publish each completed batch even when more lines arrived during the merge;
            // the next tick reconciles the newest snapshot without starving the display.
            if (IsWatchPaused) { _timelineDirty = true; return; }
            LogLine? selectedOriginal = SelectedLogLine?.OriginalLine;
            _allLines.Clear();
            _allLines.AddRange(rows);
            if (rows.Any(r => r.HasStructuredEvent)) HasStructuredLogs = true;
            foreach (LogLine row in _allLines)
                row.MatchedRule = IsHighlightingEnabled
                    ? _highlightService.GetMatchingRule(row.Text, _compiledHighlightRules) : null;
            TotalLineCount = rows.Count;
            UpdateStatsAll();
            string warnings = string.Join(" | ", _timelineSources.Where(source => !string.IsNullOrEmpty(source.WarningMessage))
                .Select(source => $"{source.FileName}: {source.WarningMessage}"));

            if (IsCorrelationTab)
            {
                string truncMsg = isTruncated ? $" (capped at {rows.Count:N0} of {totalFound:N0} matches)" : string.Empty;
                WarningMessage = $"Correlating '{CorrelationId}' across {_timelineSources.Count} source(s){truncMsg}. {TimelinePolicy}" +
                    (warnings.Length == 0 ? string.Empty : $" {warnings}");
            }
            else
            {
                WarningMessage = $"{_timelineSources.Count} source(s). {TimelinePolicy}" +
                    (warnings.Length == 0 ? string.Empty : $" {warnings}");
            }

            UpdateStatus();
            await ApplySearchAsync();
            _ = UpdateChartAsync();
            if (!_disposed && selectedOriginal != null && !IsFollowTail)
                SelectedLogLine = VisibleLines.FirstOrDefault(row => ReferenceEquals(row.OriginalLine, selectedOriginal));
            if (revision != _timelineRevision) _timelineDirty = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!_disposed) WarningMessage = $"{(IsCorrelationTab ? "Correlation" : "Timeline")} update failed: {ex.Message}";
        }
        finally
        {
            _timelineBuilding = false;
            _timelineCts?.Dispose();
            _timelineCts = null;
        }
    }

    private void DetachTimelineSource(LogTabViewModel source)
    {
        source.LinesChanged -= OnTimelineSourceChanged;
        source.Closed -= OnTimelineSourceClosed;
        source.PropertyChanged -= OnTimelineSourcePropertyChanged;
    }

    private void DisposeTimeline()
    {
        _timelineTimer?.Stop();
        if (_timelineTimer != null) _timelineTimer.Tick -= OnTimelineTick;
        _timelineCts?.Cancel();
        foreach (LogTabViewModel source in _timelineSources) DetachTimelineSource(source);
        _timelineSources.Clear();
    }

    [RelayCommand]
    private void OpenOriginalLine()
    {
        if (IsTimeline && SelectedLogLine is LogLine line)
            OriginalLineRequested?.Invoke(this, line);
    }

    /// <summary>Selects an original record and scrolls to it after activating its tab.</summary>
    public void NavigateToLine(LogLine line)
    {
        if (!_allLines.Contains(line)) return;
        if (!VisibleLines.Contains(line))
        {
            WarningMessage = "The original line is hidden by this source's filters. Clear its filters to reveal it.";
            return;
        }
        IsFollowTail = false;
        SelectedLogLine = line;
        Application.Current.Dispatcher.InvokeAsync(() => ScrollToLineRequested?.Invoke(this, line),
            DispatcherPriority.Loaded);
    }
}
