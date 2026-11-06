using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;
using LogTailPro.Services;
using LogTailPro.Views;
using Microsoft.Win32;

namespace LogTailPro.ViewModels;

/// <summary>ViewModel for a single open log file tab.</summary>
public sealed partial class LogTabViewModel : ObservableObject, IDisposable
{
    private readonly LogParserService _parser = new();
    private readonly FileWatcherService _watcher = new();
    private readonly SearchService _searchService = new();
    private readonly HighlightService _highlightService = new();
    private readonly MultilineGroupingService _groupingService = new();

    /// <summary>Whether multiline exceptions are grouped into expandable events.</summary>
    [ObservableProperty] private bool _isExceptionGroupingEnabled = true;

    partial void OnIsExceptionGroupingEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(ExceptionGroupingLabel));
        _ = ApplySearchAsync();
    }

    public string ExceptionGroupingLabel => IsExceptionGroupingEnabled ? "Plain Line View" : "Group Exceptions";

    [RelayCommand]
    private void ToggleExceptionGrouping() => IsExceptionGroupingEnabled = !IsExceptionGroupingEnabled;

    [RelayCommand]
    public void ExpandAllExceptions()
    {
        foreach (LogLine l in _allLines) if (l.HasContinuations) l.IsExpanded = true;
        _ = ApplySearchAsync();
    }

    [RelayCommand]
    public void CollapseAllExceptions()
    {
        foreach (LogLine l in _allLines) if (l.HasContinuations) l.IsExpanded = false;
        _ = ApplySearchAsync();
    }

    [RelayCommand]
    public void ToggleGroupExpanded(LogLine? line)
    {
        if (line != null && line.HasContinuations)
        {
            line.IsExpanded = !line.IsExpanded;
            _ = ApplySearchAsync();
        }
    }
    private readonly TimestampParser _timestampParser = new();
    private readonly AppSettings _settings;
    private readonly bool _isReadOnly;

    /// <summary>Non-null when this tab streams from a process instead of a file.</summary>
    private readonly ProcessOutputCaptureService? _captureService;

    /// <summary>Extra resource (debug monitor, event log…) whose lifetime is tied to this tab.</summary>
    private readonly IDisposable? _ownedResource;

    private IReadOnlyList<(HighlightRule Rule, Regex CompiledPattern)> _compiledHighlightRules = [];

    private readonly List<LogLine> _allLines = new();
    private Regex? _activePattern;
    private TypedFieldFilter? _activeTypedFilter;
    private CancellationTokenSource? _searchCts;
    private long _nextLineNumber = 1;
    private DateTime? _lastTimestamp;
    private bool _disposed;

    // ── Public exposure of _allLines (for merge) ─────────────────────────────
    public IReadOnlyList<LogLine> AllLines => _allLines;

    // ── Identity ──────────────────────────────────────────────────────────────
    public LogFile LogFile { get; }
    public string FileName => LogFile.FileName;
    public string FilePath => LogFile.FilePath;

    /// <summary>True when this tab was opened via Process Explorer (not a file).</summary>
    public bool IsProcessTab { get; private init; }

    /// <summary>PID of the attached process, if this is a process tab (0 = all processes / unknown).</summary>
    public int? AttachedProcessId { get; private init; }

    /// <summary>Capture mode for process tabs.</summary>
    public ProcessAttachmentType? ProcessCaptureType { get; private init; }

    // ── Lines ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private BulkObservableCollection<LogLine> _visibleLines = new();

    // ── File state ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isFollowTail = true;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _loadProgress;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private string _statusText = "Loading…";
    [ObservableProperty] private string _warningMessage = string.Empty;
    [ObservableProperty] private long _fileSizeBytes;
    [ObservableProperty] private bool _showElapsedColumn;

    [ObservableProperty]
    private FileStatus _fileStatus = FileStatus.Watching;

    partial void OnFileStatusChanged(FileStatus value)
    {
        OnPropertyChanged(nameof(FileStatusIcon));
        OnPropertyChanged(nameof(FileStatusText));
    }

    partial void OnFileSizeBytesChanged(long value)
    {
        OnPropertyChanged(nameof(FileSizeText));
        OnPropertyChanged(nameof(FilePathWithSize));
    }

    /// <summary>Unicode icon reflecting <see cref="FileStatus"/> shown in the tab header.</summary>
    public string FileStatusIcon => FileStatus switch
    {
        FileStatus.Watching => "●",
        FileStatus.Paused   => "⏸",
        FileStatus.Error    => "⚠",
        FileStatus.Deleted  => "✕",
        _                   => "●"
    };

    /// <summary>Human-readable status label shown in the status bar.</summary>
    public string FileStatusText => FileStatus switch
    {
        FileStatus.Watching => "Watching",
        FileStatus.Paused   => "Paused",
        FileStatus.Error    => "Error",
        FileStatus.Deleted  => "Deleted",
        _                   => "Unknown"
    };

    /// <summary>Human-readable file size for the status bar.</summary>
    public string FileSizeText => FileSizeBytes switch
    {
        0                  => string.Empty,
        < 1024             => $"{FileSizeBytes} B",
        < 1024 * 1024      => $"{FileSizeBytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{FileSizeBytes / (1024.0 * 1024):F1} MB",
        _                  => $"{FileSizeBytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    /// <summary>Encoding name for the toolbar (e.g. "UTF-8").</summary>
    public string EncodingName => LogFile.Encoding.WebName.ToUpperInvariant();

    /// <summary>File path with size shown in the toolbar read-only TextBox.</summary>
    public string FilePathWithSize => FileSizeBytes > 0
        ? $"{LogFile.FilePath}  ({FileSizeText})"
        : LogFile.FilePath;

    private long _totalLineCount;
    public long TotalLineCount
    {
        get => _totalLineCount;
        private set => SetProperty(ref _totalLineCount, value);
    }

    // ── Bookmarks ─────────────────────────────────────────────────────────────
    public ObservableCollection<LogLine> Bookmarks { get; } = new();

    /// <summary>Raised when the user adds a bookmark (not when bookmarks are restored from settings).</summary>
    public event EventHandler? BookmarkAdded;

    /// <summary>Settings key for a file's bookmarks.</summary>
    public static string BookmarkKey(string filePath) => filePath.ToUpperInvariant();

    private static string Truncate(string text)
        => text.Length > BookmarkEntry.MaxTextLength ? text[..BookmarkEntry.MaxTextLength] : text;

    /// <summary>Snapshot of this tab's bookmarks in a form that can be stored in settings.</summary>
    public List<BookmarkEntry> ExportBookmarks()
        => Bookmarks.Select(b => new BookmarkEntry { LineNumber = b.LineNumber, Text = Truncate(b.Text) }).ToList();

    /// <summary>
    /// Re-applies saved bookmarks to freshly loaded lines: first by line number + text, otherwise by
    /// text alone (covers files whose tail window shifted since the bookmark was saved).
    /// </summary>
    private void RestoreBookmarks(IReadOnlyList<LogLine> lines)
    {
        if (IsProcessTab || _isReadOnly) return;
        if (!_settings.Bookmarks.TryGetValue(BookmarkKey(FilePath), out List<BookmarkEntry>? saved) || saved.Count == 0)
            return;

        foreach (BookmarkEntry entry in saved)
        {
            LogLine? match =
                lines.FirstOrDefault(l => !l.IsBookmarked && l.LineNumber == entry.LineNumber && Truncate(l.Text) == entry.Text)
                ?? lines.FirstOrDefault(l => !l.IsBookmarked && Truncate(l.Text) == entry.Text);
            if (match == null) continue;
            match.IsBookmarked = true;
            Bookmarks.Add(match);
        }
    }

    /// <summary>Restores specific bookmarks from line numbers.</summary>
    public void RestoreBookmarks(IEnumerable<long> lineNumbers)
    {
        HashSet<long> set = new(lineNumbers);
        foreach (LogLine line in _allLines)
        {
            if (set.Contains(line.LineNumber) && !line.IsBookmarked)
            {
                line.IsBookmarked = true;
                Bookmarks.Add(line);
            }
        }
        NavigationBarInvalidated?.Invoke(this, EventArgs.Empty);
    }

    // ── Phase 6 features ─────────────────────────────────────────────────────
    [ObservableProperty] private bool _isLineWrapEnabled;
    [ObservableProperty] private bool _isStatsPanelVisible;
    [ObservableProperty] private int _statsErrorCount;
    [ObservableProperty] private int _statsWarnCount;
    [ObservableProperty] private int _statsInfoCount;
    [ObservableProperty] private string _statsLinesPerSecond = "0";
    [ObservableProperty] private string _displayName = string.Empty;

    private int _recentLineCount;
    private DateTime _rateWindow = DateTime.UtcNow;

    // Selected lines for multi-select copy
    public List<LogLine> SelectedLogLines { get; set; } = new();

    // ── Search state ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isRegexMode;
    [ObservableProperty] private bool _isCaseSensitive;
    [ObservableProperty] private bool _isSearchPanelVisible;
    [ObservableProperty] private string _searchError = string.Empty;
    [ObservableProperty] private FilterMode _filterMode = FilterMode.None;
    [ObservableProperty] private int _matchCount;
    [ObservableProperty] private int _currentMatchIndex = -1;
    [ObservableProperty] private LogLine? _selectedLogLine;
    partial void OnSelectedLogLineChanged(LogLine? value) => UpdateInspectorDetails(value);

    // ── Structured JSON viewer & inspector state ─────────────────────────────
    [ObservableProperty] private bool _isStructuredViewEnabled = true;
    [ObservableProperty] private bool _hasStructuredLogs;
    [ObservableProperty] private bool _isSelectedLineStructured;
    [ObservableProperty] private StructuredLogEvent? _selectedStructuredEvent;
    [ObservableProperty] private string _selectedLineLevel = string.Empty;
    [ObservableProperty] private string _selectedLineTimestamp = string.Empty;
    [ObservableProperty] private string _selectedLineMessage = string.Empty;
    [ObservableProperty] private string _selectedLineTemplate = string.Empty;
    [ObservableProperty] private bool _hasSelectedLineTemplate;
    [ObservableProperty] private string _selectedLineException = string.Empty;
    [ObservableProperty] private bool _hasSelectedLineException;
    [ObservableProperty] private string _selectedLineRawText = string.Empty;
    [ObservableProperty] private ObservableCollection<LogPropertyNode> _selectedLineProperties = new();
    [ObservableProperty] private string? _suggestedCorrelationId;

    public double StructuredLevelWidth => (IsStructuredViewEnabled && HasStructuredLogs) ? 70.0 : 0.0;
    public double StructuredTimeWidth => (IsStructuredViewEnabled && HasStructuredLogs) ? 190.0 : 0.0;

    partial void OnIsStructuredViewEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(StructuredLevelWidth));
        OnPropertyChanged(nameof(StructuredTimeWidth));
        OnPropertyChanged(nameof(StructuredViewLabel));
        LayoutUpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnHasStructuredLogsChanged(bool value)
    {
        OnPropertyChanged(nameof(StructuredLevelWidth));
        OnPropertyChanged(nameof(StructuredTimeWidth));
        LayoutUpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    public string StructuredViewLabel => IsStructuredViewEnabled ? "Raw View" : "Structured View";

    [RelayCommand]
    private void ToggleStructuredView() => IsStructuredViewEnabled = !IsStructuredViewEnabled;

    public event EventHandler? LayoutUpdateRequested;

    /// <summary>Raised when this tab requests correlation search for an ID.</summary>
    public event EventHandler<string?>? CorrelationRequested;

    /// <summary>Extracts a suggested correlation ID from the currently selected log line.</summary>
    public string? ExtractSuggestedCorrelationId() =>
        SelectedLogLine != null ? CorrelationService.ExtractSuggestedId(SelectedLogLine) : null;

    [RelayCommand]
    private void RequestCorrelation(string? id = null)
    {
        string? target = !string.IsNullOrWhiteSpace(id) ? id : ExtractSuggestedCorrelationId();
        CorrelationRequested?.Invoke(this, target);
    }

    private void UpdateInspectorDetails(LogLine? line)
    {
        if (line == null)
        {
            IsSelectedLineStructured = false;
            SelectedStructuredEvent = null;
            SelectedLineLevel = string.Empty;
            SelectedLineTimestamp = string.Empty;
            SelectedLineMessage = string.Empty;
            SelectedLineTemplate = string.Empty;
            HasSelectedLineTemplate = false;
            SelectedLineException = string.Empty;
            HasSelectedLineException = false;
            SelectedLineProperties.Clear();
            SelectedLineRawText = string.Empty;
            DetailText = string.Empty;
            SuggestedCorrelationId = null;
            return;
        }

        SelectedLineRawText = line.Text;
        SuggestedCorrelationId = CorrelationService.ExtractSuggestedId(line);
        if (line.StructuredEvent != null && line.StructuredEvent.IsValidJson)
        {
            IsSelectedLineStructured = true;
            SelectedStructuredEvent = line.StructuredEvent;
            SelectedLineLevel = line.StructuredEvent.Level ?? string.Empty;
            SelectedLineTimestamp = line.StructuredEvent.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff zzz") ?? string.Empty;
            SelectedLineMessage = line.StructuredEvent.Message ?? string.Empty;
            SelectedLineTemplate = line.StructuredEvent.MessageTemplate ?? string.Empty;
            HasSelectedLineTemplate = !string.IsNullOrEmpty(line.StructuredEvent.MessageTemplate);
            SelectedLineException = line.StructuredEvent.Exception ?? string.Empty;
            HasSelectedLineException = !string.IsNullOrEmpty(line.StructuredEvent.Exception);
            SelectedLineProperties.Clear();
            foreach (LogPropertyNode node in LogPropertyNode.FromEvent(line.StructuredEvent))
                SelectedLineProperties.Add(node);

            DetailText = TryPrettyPrintJson(line.Text) ?? line.Text;
        }
        else
        {
            IsSelectedLineStructured = false;
            SelectedStructuredEvent = line.StructuredEvent;
            SelectedLineLevel = string.Empty;
            SelectedLineTimestamp = line.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? string.Empty;
            SelectedLineMessage = line.Text;
            SelectedLineTemplate = string.Empty;
            HasSelectedLineTemplate = false;
            if (line.HasContinuations)
            {
                SelectedLineException = string.Join(Environment.NewLine, line.Continuations.Select(c => c.Text));
                HasSelectedLineException = true;
            }
            else
            {
                SelectedLineException = string.Empty;
                HasSelectedLineException = false;
            }
            SelectedLineProperties.Clear();
            DetailText = line.StructuredEvent?.ParseError != null
                ? $"[Malformed JSON: {line.StructuredEvent.ParseError}]{Environment.NewLine}{line.Text}"
                : (line.HasContinuations ? $"{line.Text}{Environment.NewLine}{SelectedLineException}" : line.Text);
        }
    }

    // ── Highlighting enabled (toggled from toolbar) ───────────────────────────
    [ObservableProperty] private bool _isHighlightingEnabled = true;
    partial void OnIsHighlightingEnabledChanged(bool value) => RefreshHighlighting();

    // ── Watcher pause ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isWatchPaused;
    partial void OnIsWatchPausedChanged(bool value) => OnPropertyChanged(nameof(PauseResumeLabel));
    public string PauseResumeLabel => IsWatchPaused ? "Resume" : "Pause";

    // ── Search bar UI state (new spec) ────────────────────────────────────────
    [ObservableProperty] private bool _isFilterTailMode;
    [ObservableProperty] private bool _isIgnoreCase;
    [ObservableProperty] private bool _isInvertMatch;

    // ── Search results table ──────────────────────────────────────────────────
    [ObservableProperty] private BulkObservableCollection<SearchResultRow> _searchResults = new();

    /// <summary>Most rows shown in the results grid; the match count and highlighting still cover every match.</summary>
    private const int MaxSearchResultRows = 200_000;

    /// <summary>True when at least one result has capturing groups (shows the Groups column).</summary>
    [ObservableProperty] private bool _hasSearchGroups;

    /// <summary>True when at least one result has a timestamp (shows the Time column).</summary>
    [ObservableProperty] private bool _hasSearchTimestamps;

    /// <summary>Text above the results grid, e.g. "111 results".</summary>
    [ObservableProperty] private string _searchResultsSummary = string.Empty;
    [ObservableProperty] private bool _isSearchResultsVisible;
    [ObservableProperty] private SearchResultRow? _selectedSearchResult;
    partial void OnSelectedSearchResultChanged(SearchResultRow? value)
    {
        if (value?.Source == null) return;
        SelectedLogLine = value.Source;
        ScrollToLineRequested?.Invoke(this, value.Source);
    }

    // ── Saved patterns ────────────────────────────────────────────────────────
    [ObservableProperty] private SavedSearchPattern? _selectedSavedPattern;

    /// <summary>Exposes the shared SavedPatterns collection for direct binding in SearchPanel.</summary>
    public ObservableCollection<SavedSearchPattern> SavedPatterns => _settings.SavedPatterns;

    // ── Filter chain ──────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<FilterRuleViewModel> _filterRules = new();

    // ── Detail / pretty-print panel ───────────────────────────────────────────
    [ObservableProperty] private bool _isDetailPanelVisible;
    [ObservableProperty] private string _detailText = string.Empty;

    // ── Elapsed column width (0 = hidden, 90 = visible) ──────────────────────
    public double ElapsedColumnWidth => ShowElapsedColumn ? 90.0 : 0.0;
    partial void OnShowElapsedColumnChanged(bool value) => OnPropertyChanged(nameof(ElapsedColumnWidth));

    // ── Computed props for search error visibility ────────────────────────────
    public bool HasSearchError => !string.IsNullOrEmpty(SearchError);
    partial void OnSearchErrorChanged(string value) => OnPropertyChanged(nameof(HasSearchError));

    // ── Temp file tracking (clipboard pastes) ─────────────────────────────────
    public string? TempFilePath { get; set; }

    // ── Scroll position memory (persists across tab switches) ─────────────────
    public double SavedScrollVertical { get; set; }
    public double SavedScrollHorizontal { get; set; }

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<LogLine>? ScrollToLineRequested;
    public event EventHandler? NavigationBarInvalidated;
    public event EventHandler? SelectAllRequested;

    // ── Property change hooks ─────────────────────────────────────────────────
    partial void OnSelectedSavedPatternChanged(SavedSearchPattern? value)
    {
        if (value != null) LoadPatternCommand.Execute(value);
    }

    partial void OnSearchTextChanged(string value) { _ = ApplySearchAsync(); }
    partial void OnIsRegexModeChanged(bool value) { OnPropertyChanged(nameof(IsRegexModeIndex)); _ = ApplySearchAsync(); }
    partial void OnIsCaseSensitiveChanged(bool value) { _ = ApplySearchAsync(); }
    partial void OnIsIgnoreCaseChanged(bool value) { IsCaseSensitive = !value; }

    /// <summary>0=Text, 1=Regex — for binding the mode ComboBox SelectedIndex.</summary>
    public int IsRegexModeIndex
    {
        get => IsRegexMode ? 1 : 0;
        set => IsRegexMode = value == 1;
    }
    partial void OnFilterModeChanged(FilterMode value)
    {
        OnPropertyChanged(nameof(FilterModeIndex));
        _ = ApplySearchAsync();
    }

    /// <summary>Maps FilterMode enum (0/1/2) to ComboBox SelectedIndex — same ordinal order.</summary>
    public int FilterModeIndex
    {
        get => (int)FilterMode;
        set => FilterMode = (FilterMode)value;
    }

    // ── Constructors ──────────────────────────────────────────────────────────

    public LogTabViewModel(string filePath, AppSettings settings)
    {
        _settings = settings;
        LogFile = new LogFile { FilePath = filePath };
        _isFollowTail = settings.FollowTailOnOpen;
        _compiledHighlightRules = _highlightService.CompileRules(settings.HighlightRules);
    }

    /// <summary>Creates a read-only merged tab from a pre-built sorted line list.</summary>
    public LogTabViewModel(string displayName, IReadOnlyList<LogLine> preloadedLines, AppSettings settings)
    {
        _settings = settings;
        _isReadOnly = true;
        LogFile = new LogFile { FilePath = displayName };
        _isFollowTail = false;
        FileStatus = FileStatus.Paused;

        long num = 1;
        foreach (LogLine line in preloadedLines)
        {
            LogLine copy = new()
            {
                LineNumber = num++,
                Text = line.SourceFileName != null
                    ? $"[{line.SourceFileName}] {line.Text}"
                    : line.Text,
                Timestamp = line.Timestamp,
                TimelineTimestamp = line.TimelineTimestamp,
                StructuredEvent = line.StructuredEvent,
                ElapsedSincePrevious = line.ElapsedSincePrevious,
                ByteOffset = line.ByteOffset
            };
            if (copy.HasStructuredEvent) HasStructuredLogs = true;
            _allLines.Add(copy);
        }
        VisibleLines.ReplaceAll(_allLines);

        _nextLineNumber = num;
        TotalLineCount = preloadedLines.Count;
        _compiledHighlightRules = _highlightService.CompileRules(settings.HighlightRules);
        IsLoaded = true;
        UpdateStatus();
    }

    /// <summary>
    /// Creates a tab that streams lines from a <see cref="ProcessOutputCaptureService"/>
    /// instead of a file. The tab takes ownership of <paramref name="captureService"/> and
    /// <paramref name="ownedResource"/> and disposes both when it is closed.
    /// </summary>
    public LogTabViewModel(ProcessInfo process, ProcessAttachmentType captureType,
                           ProcessOutputCaptureService captureService, AppSettings settings,
                           IDisposable? ownedResource = null)
    {
        _settings = settings;
        _captureService = captureService;
        _ownedResource = ownedResource;
        IsProcessTab = true;
        AttachedProcessId = process.Pid;
        ProcessCaptureType = captureType;

        string modeLabel = captureType switch
        {
            ProcessAttachmentType.StandardOutput         => "stdout",
            ProcessAttachmentType.StandardError          => "stderr",
            ProcessAttachmentType.StandardOutputAndError => "stdout+stderr",
            ProcessAttachmentType.DebugOutput            => "debug output",
            ProcessAttachmentType.WindowsEventLog        => "event log",
            _                                            => "output"
        };
        string pidText = process.Pid > 0 ? $" [{process.Pid}]" : string.Empty;
        LogFile = new LogFile { FilePath = $"{process.Name}{pidText} — {modeLabel}" };
        _displayName = LogFile.FilePath;
        _isFollowTail = true;
        _compiledHighlightRules = _highlightService.CompileRules(settings.HighlightRules);
        FileStatus = FileStatus.Watching;
        IsLoaded = true;

        _captureService.NewLinesAvailable += OnNewLinesAvailable;
        _captureService.ProcessExited     += OnCapturedProcessExited;
        UpdateStatus();
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isReadOnly || IsProcessTab) return;

        IsLoading = true;
        LoadProgress = 0;

        try
        {
            IProgress<int> progress = new Progress<int>(p => LoadProgress = p);
            long maxBytes = _settings.MaxLoadMegabytes > 0 ? _settings.MaxLoadMegabytes * 1024L * 1024L : 0;
            TailLoadResult result = await _parser.LoadTailAsync(
                LogFile.FilePath, _settings.MaxLinesInMemory, maxBytes, progress, ct);

            LogFile.FileSizeBytes = result.FileSize;
            LogFile.Encoding = result.Encoding;
            FileSizeBytes = result.FileSize;

            // Line numbers are absolute: when the start of a huge file was skipped, numbering continues from there.
            _nextLineNumber = result.FirstLineNumber;
            List<LogLine> lines = await Task.Run(() => BuildLogLines(result.Lines), ct);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RestoreBookmarks(lines);
                _groupingService.GroupLines(lines, isLoadedFromBeginning: result.SkippedBytes == 0 && result.FirstLineNumber == 1);
                _allLines.AddRange(lines);
                List<LogLine> initialVisible = IsExceptionGroupingEnabled ? lines.Where(l => !l.IsContinuation).ToList() : lines;
                VisibleLines.ReplaceAll(initialVisible);
                TotalLineCount = _nextLineNumber - 1;
                WarningMessage = string.Empty;
                if (result.SkippedBytes > 0)
                    WarningMessage = $"Large file: showing the last {FormatSize(result.FileSize - result.SkippedBytes)} of {FormatSize(result.FileSize)} " +
                                     $"(first {result.FirstLineNumber - 1:N0} lines not loaded). Raise \"Max load size\" in Settings to load more.";
                IsLoaded = true;
                IsLoading = false;
                FileStatus = FileStatus.Watching;
                UpdateStatus();
                NavigationBarInvalidated?.Invoke(this, EventArgs.Empty);
                LinesChanged?.Invoke(this, EventArgs.Empty);
            }, DispatcherPriority.Background, ct);

            // Continue exactly where the load stopped — never from the start of the loaded range, or the
            // whole file would be delivered a second time on the first append.
            _watcher.NewLinesAvailable -= OnNewLinesAvailable;
            _watcher.FileDeleted -= OnFileDeleted;
            _watcher.FileTruncated -= OnFileTruncated;
            _watcher.NewLinesAvailable += OnNewLinesAvailable;
            _watcher.FileDeleted += OnFileDeleted;
            _watcher.FileTruncated += OnFileTruncated;
            _watcher.StartWatching(LogFile.FilePath, result.ReadEndOffset, result.Encoding);
        }
        catch (OperationCanceledException)
        {
            IsLoading = false;
        }
        catch (Exception ex)
        {
            IsLoading = false;
            FileStatus = FileStatus.Error;
            StatusText = $"Error: {ex.Message}";
        }
    }

    private static string FormatSize(long bytes)
        => bytes >= 1_073_741_824 ? $"{bytes / 1_073_741_824.0:F1} GB"
         : bytes >= 1_048_576 ? $"{bytes / 1_048_576.0:F0} MB"
         : $"{bytes / 1024.0:F0} KB";

    /// <summary>True when a line limit is configured (0 or less means unlimited).</summary>
    private bool HasLineLimit => _settings.MaxLinesInMemory > 0;

    private List<LogLine> BuildLogLines(IReadOnlyList<string> rawLines, bool isFromWatcher = false)
    {
        List<LogLine> result = new(rawLines.Count);
        long lineNumber = _nextLineNumber;
        string tabExpansion = _settings.TabWidth > 0 ? new string(' ', _settings.TabWidth) : "\t";
        bool foundStructured = false;

        foreach (string raw in rawLines)
        {
            string text = raw.Contains('\t') ? raw.Replace("\t", tabExpansion) : raw;
            StructuredLogEvent? jsonEvent = JsonLogParser.TryParse(text);
            DateTime? ts = null;
            DateTimeOffset? timelineTs = null;

            if (jsonEvent != null && jsonEvent.IsValidJson)
            {
                foundStructured = true;
                if (jsonEvent.Timestamp.HasValue)
                {
                    ts = jsonEvent.Timestamp.Value.LocalDateTime;
                    timelineTs = jsonEvent.Timestamp.Value.ToUniversalTime();
                }
            }

            if (!ts.HasValue)
            {
                ts = _timestampParser.TryParse(text);
            }

            TimeSpan? elapsed = (ts.HasValue && _lastTimestamp.HasValue && ts > _lastTimestamp)
                ? ts - _lastTimestamp : null;

            LogLine line = new()
            {
                LineNumber = lineNumber++,
                Text = text,
                Timestamp = ts,
                TimelineTimestamp = timelineTs,
                StructuredEvent = jsonEvent,
                ElapsedSincePrevious = elapsed,
                MatchedRule = IsHighlightingEnabled
                    ? _highlightService.GetMatchingRule(text, _compiledHighlightRules)
                    : null,
                ArrivedAt = isFromWatcher ? DateTime.Now : DateTime.MinValue
            };
            result.Add(line);

            if (ts.HasValue) _lastTimestamp = ts;
        }

        if (foundStructured && !HasStructuredLogs)
        {
            HasStructuredLogs = true;
        }

        _nextLineNumber = lineNumber;
        return result;
    }

    // ── Watcher callbacks ─────────────────────────────────────────────────────

    private void OnNewLinesAvailable(object? sender, IReadOnlyList<string> newLines)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_disposed) return;
            List<LogLine> created = BuildLogLines(newLines, isFromWatcher: true);
            TotalLineCount += created.Count;

            foreach (LogLine line in created) _groupingService.AttachLine(_allLines, line);
            TrimOldLines();

            List<LogLine> toShow = new(created.Count);
            foreach (LogLine line in created)
            {
                MarkSearchMatch(line);
                if (LinePassesFilter(line)) { if (!IsExceptionGroupingEnabled || !line.IsContinuation || (line.GroupParent != null && line.GroupParent.IsExpanded)) toShow.Add(line); }
            }
            VisibleLines.AppendRange(toShow);

            if (HasLineLimit && VisibleLines.Count > _settings.MaxLinesInMemory)
                VisibleLines.RemoveFirst(VisibleLines.Count - _settings.MaxLinesInMemory);

            if (!IsProcessTab)
            {
                try { FileSizeBytes = new FileInfo(LogFile.FilePath).Length; } catch { }
            }
            UpdateStats(created);
            CheckForNotifications(created);
            UpdateStatus();
            NavigationBarInvalidated?.Invoke(this, EventArgs.Empty);
            LinesChanged?.Invoke(this, EventArgs.Empty);
        }, DispatcherPriority.Background);
    }

    private void OnFileDeleted(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            FileStatus = FileStatus.Deleted;
            WarningMessage = "File deleted or moved.";
            StatusText = "File deleted";
        });
    }

    private void OnCapturedProcessExited(object? sender, int exitCode)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            FileStatus = FileStatus.Deleted;
            StatusText = $"Process exited (code {exitCode})";
            WarningMessage = $"Process exited with code {exitCode}.";
        }, DispatcherPriority.Background);
    }

    private void OnFileTruncated(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            _allLines.Clear();
            VisibleLines.Clear();
            LinesChanged?.Invoke(this, EventArgs.Empty);
            TotalLineCount = 0;
            _nextLineNumber = 1;
            _lastTimestamp = null;
            Bookmarks.Clear();
            WarningMessage = "File truncated — reloading…";
            await InitializeAsync();
        });
    }

    private void TrimOldLines()
    {
        if (!HasLineLimit) return;
        int toRemove = _allLines.Count - _settings.MaxLinesInMemory;
        if (toRemove > 0)
        {
            _allLines.RemoveRange(0, toRemove);
            if (_allLines.Count > 0 && _allLines[0].IsContinuation && _allLines[0].GroupParent is { } parent && !_allLines.Contains(parent))
            {
                _allLines[0].IsPartialContinuation = true;
                _allLines[0].GroupParent = null;
            }
        }
    }

    // ── Search & filter ───────────────────────────────────────────────────────

    private async Task ApplySearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        CancellationToken ct = _searchCts.Token;

        SearchError = string.Empty;
        TypedFieldFilter? typedFilter = null;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            if (TypedFieldFilter.TryParse(SearchText, out TypedFieldFilter? parsedTf))
            {
                typedFilter = parsedTf;
                _activeTypedFilter = typedFilter;
                _activePattern = null;
            }
            else
            {
                (Regex? compiled, string? error) = _searchService.CompilePattern(
                    SearchText, IsRegexMode, IsCaseSensitive);
                if (error != null) { SearchError = error; return; }
                _activePattern = compiled;
                _activeTypedFilter = null;
            }
        }
        else
        {
            _activePattern = null;
            _activeTypedFilter = null;
            SearchResults.ReplaceAll(Array.Empty<SearchResultRow>());
            HasSearchGroups = false;
            HasSearchTimestamps = false;
            SearchResultsSummary = string.Empty;
            IsSearchResultsVisible = false;
        }

        IReadOnlyList<CompiledFilterRule> compiledChain =
            _searchService.CompileChain(FilterRules.Select(r => r.ToModel()).ToList());

        List<LogLine> snapshot = _allLines.ToList();
        Regex? pattern = _activePattern;
        FilterMode mode = FilterMode;

        try
        {
            List<SearchResultRow> rows = new();
            List<LogLine> filtered = await Task.Run(() =>
            {
                foreach (LogLine line in snapshot)
                {
                    bool m = typedFilter != null
                        ? typedFilter.Matches(line)
                        : (pattern != null && SafeIsMatch(pattern, line.Text));
                    if (!m && line.HasContinuations)
                    {
                        foreach (LogLine cont in line.Continuations)
                        {
                            if (typedFilter != null ? typedFilter.Matches(cont) : (pattern != null && SafeIsMatch(pattern, cont.Text)))
                            {
                                m = true;
                                break;
                            }
                        }
                    }
                    line.IsSearchMatch = m;
                }

                IEnumerable<LogLine> result = snapshot;
                if (mode != FilterMode.None)
                {
                    if (typedFilter != null)
                        result = _searchService.ApplyTypedFilter(result, typedFilter, mode);
                    else if (pattern != null)
                        result = _searchService.ApplyFilter(result, pattern, mode);
                }

                if (compiledChain.Count > 0)
                    result = result.Where(l => _searchService.LineMatchesChain(l, compiledChain));

                List<LogLine> list = result.ToList();
                if (IsExceptionGroupingEnabled) list = list.Where(l => !l.IsContinuation || (l.GroupParent != null && l.GroupParent.IsExpanded)).ToList();

                // Build the grid rows here, off the UI thread
                if (typedFilter != null)
                {
                    foreach (LogLine line in list)
                    {
                        if (!line.IsSearchMatch) continue;
                        if (rows.Count >= MaxSearchResultRows) break;
                        rows.Add(new SearchResultRow
                        {
                            LineNumber = line.LineNumber,
                            Timestamp = line.ArrivedAt != DateTime.MinValue
                                ? line.ArrivedAt
                                : line.Timestamp ?? DateTime.MinValue,
                            Text = line.Text,
                            Group1 = line.StructuredLevel ?? string.Empty,
                            Group2 = line.StructuredEvent?.Message ?? string.Empty,
                            Group3 = string.Empty,
                            CapturingGroups = line.StructuredLevel != null ? [line.StructuredLevel] : [],
                            Source = line
                        });
                    }
                }
                else if (pattern != null)
                {
                    foreach (LogLine line in list)
                    {
                        if (!line.IsSearchMatch) continue;
                        if (rows.Count >= MaxSearchResultRows) break;
                        string[] groups = _searchService.GetCapturingGroups(pattern, line.Text);
                        rows.Add(new SearchResultRow
                        {
                            LineNumber = line.LineNumber,
                            Timestamp = line.ArrivedAt != DateTime.MinValue
                                ? line.ArrivedAt
                                : line.Timestamp ?? DateTime.MinValue,
                            Text = line.Text,
                            Group1 = groups.Length > 0 ? groups[0] : string.Empty,
                            Group2 = groups.Length > 1 ? groups[1] : string.Empty,
                            Group3 = groups.Length > 2 ? groups[2] : string.Empty,
                            CapturingGroups = groups.ToList(),
                            Source = line
                        });
                    }
                }
                return list;
            }, ct);

            if (ct.IsCancellationRequested) return;

            VisibleLines.ReplaceAll(filtered);

            MatchCount = (pattern != null || typedFilter != null) ? filtered.Count(l => l.IsSearchMatch) : 0;
            CurrentMatchIndex = -1;
            SelectedLogLine = null;

            // Populate the results grid in one go (a single UI notification)
            SearchResults.ReplaceAll(rows);
            HasSearchGroups = rows.Any(r => r.CapturingGroups.Any(g => g.Length > 0));
            HasSearchTimestamps = rows.Any(r => r.Timestamp != DateTime.MinValue);
            SearchResultsSummary = (pattern == null && typedFilter == null) ? string.Empty
                : MatchCount > rows.Count
                    ? $"Showing first {rows.Count:N0} of {MatchCount:N0} matches"
                    : $"{rows.Count:N0} match{(rows.Count == 1 ? "" : "es")}";
            IsSearchResultsVisible = SearchResults.Count > 0;

            NavigationBarInvalidated?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) { }
    }

    private void MarkSearchMatch(LogLine line)
    {
        if (_activeTypedFilter != null)
            line.IsSearchMatch = _activeTypedFilter.Matches(line);
        else if (_activePattern != null)
            line.IsSearchMatch = SafeIsMatch(_activePattern, line.Text);
        else
            line.IsSearchMatch = false;
    }

    private bool LinePassesFilter(LogLine line)
    {
        if (FilterMode != FilterMode.None)
        {
            bool matches = false;
            if (_activeTypedFilter != null) matches = _activeTypedFilter.Matches(line);
            else if (_activePattern != null) matches = SafeIsMatch(_activePattern, line.Text);
            else matches = true;

            if (FilterMode == FilterMode.IncludeMatching && !matches) return false;
            if (FilterMode == FilterMode.ExcludeMatching && matches) return false;
        }
        IReadOnlyList<CompiledFilterRule> chain =
            _searchService.CompileChain(FilterRules.Select(r => r.ToModel()).ToList());
        return _searchService.LineMatchesChain(line, chain);
    }

    private static bool SafeIsMatch(Regex pattern, string text)
    {
        try { return pattern.IsMatch(text); }
        catch (RegexMatchTimeoutException) { return false; }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand] private void ToggleFollowTail() => IsFollowTail = !IsFollowTail;
    [RelayCommand] private void ShowSearch() => IsSearchPanelVisible = true;

    // Search bar button commands (new spec)
    [RelayCommand] private void ExecuteSearch() => _ = ApplySearchAsync();
    [RelayCommand] private void StopSearch() => _searchCts?.Cancel();

    [RelayCommand]
    private void PauseSearch()
    {
        if (IsTimeline)
        {
            IsWatchPaused = !IsWatchPaused;
            if (!IsWatchPaused) MarkTimelineDirty();
            UpdateStatus();
            return;
        }
        if (_isReadOnly) return;
        if (IsWatchPaused) { _watcher.Resume(); IsWatchPaused = false; }
        else               { _watcher.Pause();  IsWatchPaused = true;  }
    }

    [RelayCommand] private void ClearSearch() { SearchText = string.Empty; SearchResults.ReplaceAll(Array.Empty<SearchResultRow>()); HasSearchGroups = false; HasSearchTimestamps = false; SearchResultsSummary = string.Empty; IsSearchResultsVisible = false; MatchCount = 0; }
    [RelayCommand] private void AddSavedPattern() => SavePatternCommand.Execute(null);

    [RelayCommand]
    private void CopyWithLineNumbers()
    {
        IList<LogLine> toCopy = SelectedLogLines.Count > 0
            ? SelectedLogLines
            : SelectedLogLine != null ? [SelectedLogLine] : (IList<LogLine>)Array.Empty<LogLine>();
        if (toCopy.Count == 0) return;
        List<string> lines = new();
        foreach (LogLine l in toCopy)
        {
            lines.Add($"{l.LineNumber,6}: {l.Text}");
            if (l.HasContinuations && !l.IsExpanded)
            {
                foreach (LogLine cont in l.Continuations) lines.Add($"{cont.LineNumber,6}: {cont.Text}");
            }
        }
        string text = string.Join(Environment.NewLine, lines);
        System.Windows.Clipboard.SetText(text);
    }

    [RelayCommand]
    private void SelectAllVisibleLines() => SelectAllRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void CloseSearch()
    {
        SearchText = string.Empty;
        SearchError = string.Empty;
        IsSearchPanelVisible = false;
        FilterMode = FilterMode.None;
    }

    [RelayCommand]
    private void NextMatch()
    {
        if (VisibleLines.Count == 0) return;
        if (_activePattern != null)
        {
            int start = CurrentMatchIndex + 1;
            for (int i = 0; i < VisibleLines.Count; i++)
            {
                int idx = (start + i) % VisibleLines.Count;
                if (VisibleLines[idx].IsSearchMatch)
                {
                    CurrentMatchIndex = idx;
                    SelectedLogLine = VisibleLines[idx];
                    return;
                }
            }
        }
        else
        {
            int idx = (CurrentMatchIndex + 1) % VisibleLines.Count;
            CurrentMatchIndex = idx;
            SelectedLogLine = VisibleLines[idx];
        }
    }

    [RelayCommand]
    private void PreviousMatch()
    {
        if (VisibleLines.Count == 0) return;
        if (_activePattern != null)
        {
            int start = CurrentMatchIndex <= 0 ? VisibleLines.Count - 1 : CurrentMatchIndex - 1;
            for (int i = 0; i < VisibleLines.Count; i++)
            {
                int idx = (start - i + VisibleLines.Count) % VisibleLines.Count;
                if (VisibleLines[idx].IsSearchMatch)
                {
                    CurrentMatchIndex = idx;
                    SelectedLogLine = VisibleLines[idx];
                    return;
                }
            }
        }
        else
        {
            int idx = CurrentMatchIndex <= 0 ? VisibleLines.Count - 1 : CurrentMatchIndex - 1;
            CurrentMatchIndex = idx;
            SelectedLogLine = VisibleLines[idx];
        }
    }

    [RelayCommand]
    private void AddFilterRule()
    {
        FilterRules.Add(new FilterRuleViewModel(
            onRemove: r => { FilterRules.Remove(r); _ = ApplySearchAsync(); },
            onChanged: () => _ = ApplySearchAsync()));
    }

    [RelayCommand]
    private void GoToLine()
    {
        GoToLineDialog dialog = new() { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;

        long target = dialog.LineNumber;
        LogLine? match = VisibleLines.FirstOrDefault(l => l.LineNumber >= target);
        if (match != null)
        {
            SelectedLogLine = match;
            ScrollToLineRequested?.Invoke(this, match);
        }
    }

    [RelayCommand] private void ToggleElapsedColumn() => ShowElapsedColumn = !ShowElapsedColumn;
    [RelayCommand] private void ToggleLineWrap() => IsLineWrapEnabled = !IsLineWrapEnabled;
    [RelayCommand] private void ToggleStatsPanel() => IsStatsPanelVisible = !IsStatsPanelVisible;

    [RelayCommand]
    private async Task ExportFilteredLinesAsync()
    {
        SaveFileDialog dialog = new()
        {
            Filter = "Text Files|*.txt|Log Files|*.log|All Files|*.*",
            FileName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };
        if (dialog.ShowDialog() != true) return;

        List<string> lines = VisibleLines.Select(l => l.Text).ToList();
        await File.WriteAllLinesAsync(dialog.FileName, lines);
        StatusText = $"Exported {lines.Count:N0} lines to {System.IO.Path.GetFileName(dialog.FileName)}";
    }

    [RelayCommand]
    private void ToggleSearchResults() => IsSearchResultsVisible = !IsSearchResultsVisible;

    [RelayCommand]
    private void SavePattern()
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return;
        string defaultName = $"{(IsRegexMode ? ".*" : "Aa")} {SearchText}";

        NameInputDialog dlg = new("Save Pattern", "Pattern name:", defaultName)
        {
            Owner = Application.Current.MainWindow
        };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputValue)) return;

        _settings.SavedPatterns.Add(new SavedSearchPattern
        {
            Name = dlg.InputValue.Trim(),
            Pattern = SearchText,
            IsRegex = IsRegexMode,
            IsCaseSensitive = IsCaseSensitive,
            FilterMode = FilterMode
        });
    }

    [RelayCommand]
    private void LoadPattern(SavedSearchPattern? pattern)
    {
        if (pattern == null) return;
        SearchText = pattern.Pattern;
        IsRegexMode = pattern.IsRegex;
        IsCaseSensitive = pattern.IsCaseSensitive;
        FilterMode = pattern.FilterMode;
    }

    [RelayCommand]
    private void DeleteSavedPattern(SavedSearchPattern? pattern)
    {
        if (pattern != null) _settings.SavedPatterns.Remove(pattern);
    }

    [RelayCommand]
    private void PrettyPrint()
    {
        string? raw = SelectedLogLine?.Text;
        if (string.IsNullOrWhiteSpace(raw)) return;

        // Extract first JSON object/array or XML fragment found in the line
        string pretty = TryPrettyPrintJson(raw) ?? TryPrettyPrintXml(raw) ?? raw;
        DetailText = pretty;
        IsDetailPanelVisible = true;
    }

    private static string? TryPrettyPrintJson(string text)
    {
        int start = text.IndexOfAny(['{', '[']);
        if (start < 0) return null;
        try
        {
            string candidate = text[start..];
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(candidate);
            return System.Text.Json.JsonSerializer.Serialize(
                doc.RootElement,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch { return null; }
    }

    private static string? TryPrettyPrintXml(string text)
    {
        int start = text.IndexOf('<');
        if (start < 0) return null;
        try
        {
            System.Xml.XmlDocument xml = new();
            xml.LoadXml(text[start..]);
            System.Text.StringBuilder sb = new();
            using System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(sb,
                new System.Xml.XmlWriterSettings { Indent = true, OmitXmlDeclaration = true });
            xml.Save(writer);
            return sb.ToString();
        }
        catch { return null; }
    }

    [RelayCommand]
    private void CloseDetailPanel()
    {
        IsDetailPanelVisible = false;
        DetailText = string.Empty;
    }

    [RelayCommand]
    private void CopySelectedLines()
    {
        IList<LogLine> toCopy = SelectedLogLines.Count > 0
            ? SelectedLogLines
            : SelectedLogLine != null ? [SelectedLogLine] : (IList<LogLine>)Array.Empty<LogLine>();
        if (toCopy.Count == 0) return;

        List<string> lines = new();
        foreach (LogLine l in toCopy)
        {
            lines.Add(l.Text);
            if (l.HasContinuations && !l.IsExpanded)
            {
                foreach (LogLine cont in l.Continuations) lines.Add(cont.Text);
            }
        }
        string text = string.Join(Environment.NewLine, lines);
        System.Windows.Clipboard.SetText(text);
    }

    [RelayCommand]
    private void CopyException()
    {
        if (!string.IsNullOrEmpty(SelectedLineException))
            System.Windows.Clipboard.SetText(SelectedLineException);
    }

    [RelayCommand]
    private void CopyDetailText()
    {
        if (!string.IsNullOrEmpty(DetailText))
            System.Windows.Clipboard.SetText(DetailText);
    }

    [RelayCommand]
    private void ScrollToEnd()
    {
        if (VisibleLines.Count > 0)
        {
            SelectedLogLine = VisibleLines[^1];
            ScrollToLineRequested?.Invoke(this, VisibleLines[^1]);
        }
    }

    [RelayCommand]
    private void ScrollToTop()
    {
        if (VisibleLines.Count > 0)
        {
            SelectedLogLine = VisibleLines[0];
            ScrollToLineRequested?.Invoke(this, VisibleLines[0]);
        }
    }

    // ── Bookmark commands ─────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleBookmark()
    {
        if (SelectedLogLine == null) return;
        SelectedLogLine.IsBookmarked = !SelectedLogLine.IsBookmarked;
        if (SelectedLogLine.IsBookmarked)
        {
            Bookmarks.Add(SelectedLogLine);
            BookmarkAdded?.Invoke(this, EventArgs.Empty);
        }
        else
            Bookmarks.Remove(SelectedLogLine);
        NavigationBarInvalidated?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void NextBookmark()
    {
        if (Bookmarks.Count == 0) return;
        int currentIdx = SelectedLogLine != null ? VisibleLines.IndexOf(SelectedLogLine) : -1;
        LogLine? next = VisibleLines
            .Skip(currentIdx + 1)
            .FirstOrDefault(l => l.IsBookmarked)
            ?? VisibleLines.FirstOrDefault(l => l.IsBookmarked); // wrap around

        if (next != null)
        {
            SelectedLogLine = next;
            ScrollToLineRequested?.Invoke(this, next);
        }
    }

    [RelayCommand]
    private void PreviousBookmark()
    {
        if (Bookmarks.Count == 0) return;
        int currentIdx = SelectedLogLine != null ? VisibleLines.IndexOf(SelectedLogLine) : VisibleLines.Count;
        LogLine? prev = VisibleLines
            .Take(currentIdx)
            .LastOrDefault(l => l.IsBookmarked)
            ?? VisibleLines.LastOrDefault(l => l.IsBookmarked); // wrap around

        if (prev != null)
        {
            SelectedLogLine = prev;
            ScrollToLineRequested?.Invoke(this, prev);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Called by MainViewModel after highlight rules change. Re-applies rules to all lines and refreshes the view.</summary>
    public void RefreshHighlighting()
    {
        _compiledHighlightRules = _highlightService.CompileRules(_settings.HighlightRules);

        foreach (LogLine line in _allLines)
            line.MatchedRule = _highlightService.GetMatchingRule(line.Text, _compiledHighlightRules);

        Application.Current.Dispatcher.Invoke(() =>
        {
            List<LogLine> temp = VisibleLines.ToList();
            VisibleLines.Clear();
            foreach (LogLine l in temp) VisibleLines.Add(l);
            UpdateStatsAll();
        });
    }

    private void CheckForNotifications(IReadOnlyList<LogLine> newLines)
    {
        IEnumerable<HighlightRule> notifyRules = _settings.HighlightRules
            .Where(r => r.IsEnabled && r.NotifyOnMatch);

        foreach (HighlightRule rule in notifyRules)
        {
            LogLine? match = newLines.FirstOrDefault(l =>
                _highlightService.GetMatchingRule(l.Text, _compiledHighlightRules) == rule);
            if (match != null)
                NotificationService.Show(rule.NotifyTitle, $"[{FileName}] {match.Text}");
        }
    }

    private static bool IsErrorLine(string text) =>
        text.Contains("ERROR",     StringComparison.OrdinalIgnoreCase) ||
        text.Contains("EXCEPTION", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("FATAL",     StringComparison.OrdinalIgnoreCase) ||
        text.Contains("CRITICAL",  StringComparison.OrdinalIgnoreCase);

    private static bool IsWarnLine(string text) =>
        text.Contains("WARN", StringComparison.OrdinalIgnoreCase);

    private static bool IsInfoLine(string text) =>
        text.Contains("INFO", StringComparison.OrdinalIgnoreCase);

    private void UpdateStats(IReadOnlyList<LogLine> newLines)
    {
        foreach (LogLine line in newLines)
        {
            if      (IsErrorLine(line.Text)) StatsErrorCount++;
            else if (IsWarnLine(line.Text))  StatsWarnCount++;
            else if (IsInfoLine(line.Text))  StatsInfoCount++;
        }

        _recentLineCount += newLines.Count;
        TimeSpan elapsed = DateTime.UtcNow - _rateWindow;
        if (elapsed.TotalSeconds >= 1.0)
        {
            StatsLinesPerSecond = $"{_recentLineCount / elapsed.TotalSeconds:F1}";
            _recentLineCount = 0;
            _rateWindow = DateTime.UtcNow;
        }
    }

    private void UpdateStatsAll()
    {
        StatsErrorCount = 0; StatsWarnCount = 0; StatsInfoCount = 0;
        foreach (LogLine line in _allLines)
        {
            if      (IsErrorLine(line.Text)) StatsErrorCount++;
            else if (IsWarnLine(line.Text))  StatsWarnCount++;
            else if (IsInfoLine(line.Text))  StatsInfoCount++;
        }
    }

    private void UpdateStatus()
    {
        string state = IsLoaded ? (FileStatus == FileStatus.Watching ? "Watching" : FileStatus.ToString()) : "Loading";
        if (IsWatchPaused) state = "Paused";
        string size = IsProcessTab || IsTimeline
            ? "live"
            : FileSizeBytes >= 1_048_576
                ? $"{FileSizeBytes / 1_048_576.0:F1} MB"
                : $"{FileSizeBytes / 1024.0:F1} KB";
        string filter = FilterMode != FilterMode.None || !string.IsNullOrEmpty(SearchText)
            ? " | Filter active" : string.Empty;
        string trim = HasLineLimit && _allLines.Count >= _settings.MaxLinesInMemory
            ? " | trimming old lines" : string.Empty;
        StatusText = $"{state} | Lines: {TotalLineCount:N0} | {size}{filter}{trim}";
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Closed?.Invoke(this, EventArgs.Empty);
        DisposeTimeline();
        _searchCts?.Cancel();
        if (_captureService != null)
        {
            _captureService.NewLinesAvailable -= OnNewLinesAvailable;
            _captureService.ProcessExited     -= OnCapturedProcessExited;
            _ownedResource?.Dispose();
            _captureService.Dispose();
        }
        if (!_isReadOnly && !IsProcessTab)
        {
            _watcher.NewLinesAvailable -= OnNewLinesAvailable;
            _watcher.FileDeleted -= OnFileDeleted;
            _watcher.FileTruncated -= OnFileTruncated;
            _watcher.Dispose();
        }
        if (TempFilePath != null)
            try { File.Delete(TempFilePath); } catch { }
    }
}
