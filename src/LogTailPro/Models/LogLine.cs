namespace LogTailPro.Models;

/// <summary>Represents a single line in the log file.</summary>
public sealed class LogLine : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isBookmarked;

    /// <inheritdoc/>
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    /// <summary>1-based line number within the loaded tail (not necessarily absolute file line).</summary>
    public long LineNumber { get; init; }

    /// <summary>Raw text of the line.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Byte position in the file where this line begins.</summary>
    public long ByteOffset { get; init; }

    /// <summary>Whether this line passes the current filter.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>The first highlight rule that matched this line, or null.</summary>
    public HighlightRule? MatchedRule { get; set; }

    /// <summary>True when this line matches the active search pattern (used for visual highlighting).</summary>
    public bool IsSearchMatch { get; set; }

    /// <summary>Timestamp parsed from the line text, or null if no recognizable format found.</summary>
    public DateTime? Timestamp { get; set; }

    /// <summary>Time elapsed between this line's timestamp and the previous line's timestamp.</summary>
    public TimeSpan? ElapsedSincePrevious { get; set; }

    /// <summary>Formatted elapsed time for display in the Elapsed column.</summary>
    public string ElapsedDisplay => ElapsedSincePrevious.HasValue
        ? $"+{ElapsedSincePrevious.Value.TotalMilliseconds:N0}ms"
        : string.Empty;

    /// <summary>True when <see cref="ElapsedSincePrevious"/> exceeds 1 second (shown in orange).</summary>
    public bool IsElapsedWarning => ElapsedSincePrevious?.TotalSeconds >= 1 && ElapsedSincePrevious?.TotalSeconds < 5;

    /// <summary>True when <see cref="ElapsedSincePrevious"/> exceeds 5 seconds (shown in red).</summary>
    public bool IsElapsedCritical => ElapsedSincePrevious?.TotalSeconds >= 5;

    /// <summary>Whether this line has been bookmarked by the user.</summary>
    public bool IsBookmarked
    {
        get => _isBookmarked;
        set
        {
            if (_isBookmarked == value) return;
            _isBookmarked = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsBookmarked)));
        }
    }

    /// <summary>Source file name (used in merged tabs to label lines from different files).</summary>
    public string? SourceFileName { get; set; }

    /// <summary>Full source path for a timeline row, independent of its display name.</summary>
    public string? SourceFilePath { get; init; }

    /// <summary>Original loaded line for precise navigation, including after line-number reuse.</summary>
    public LogLine? OriginalLine { get; init; }

    /// <summary>Original source line number for timeline display.</summary>
    public long SourceLineNumber => OriginalLine?.LineNumber ?? LineNumber;

    /// <summary>Timestamp in UTC for cross-file ordering; null means no full date was recognized.</summary>
    public DateTimeOffset? TimelineTimestamp { get; set; }

    /// <summary>Explicit timestamp label, including the timezone.</summary>
    public string TimelineTimeDisplay => TimelineTimestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'") ?? "No dated timestamp";

    /// <summary>Wall-clock time when this line arrived from the file watcher. DateTime.MinValue for initial load.</summary>
    public DateTime ArrivedAt { get; set; } = DateTime.MinValue;

    /// <summary>Structured event representation if this line was parsed from structured JSON, or null.</summary>
    public StructuredLogEvent? StructuredEvent { get; set; }

    /// <summary>Structured severity level (e.g. Information, Warning, Error), or null if not structured.</summary>
    public string? StructuredLevel => StructuredEvent?.Level;

    /// <summary>Formatted timestamp string preserving the original timezone offset, or null.</summary>
    public string? StructuredTimeDisplay => StructuredEvent?.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");

    /// <summary>Rendered message if structured JSON, or malformed notice, or fallback raw text.</summary>
    public string StructuredMessage => StructuredEvent != null
        ? (StructuredEvent.IsValidJson
            ? (StructuredEvent.Message ?? StructuredEvent.MessageTemplate ?? Text)
            : $"[Malformed JSON: {StructuredEvent.ParseError}] {Text}")
        : Text;

    /// <summary>True when this line represents a valid structured JSON event.</summary>
    public bool HasStructuredEvent => StructuredEvent != null && StructuredEvent.IsValidJson;

    /// <summary>Continuation lines that belong to this multiline event (stack trace frames, inner exceptions).</summary>
    public List<LogLine> Continuations { get; } = new();

    /// <summary>True when this line is the root of a multiline event with continuations.</summary>
    public bool HasContinuations => Continuations.Count > 0;

    /// <summary>Whether this multiline exception group is expanded in the view.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ExpanderGlyph)));
        }
    }
    private bool _isExpanded;

    /// <summary>Expander symbol for display in the viewer: '▼' when expanded, '▶' when collapsed.</summary>
    public string ExpanderGlyph => HasContinuations ? (_isExpanded ? "\u25bc" : "\u25b6") : string.Empty;

    /// <summary>Summary badge indicating the count of continuation lines (e.g. '+8 frames').</summary>
    public string ContinuationBadge => HasContinuations ? $"+{Continuations.Count} frame{(Continuations.Count == 1 ? "" : "s")}" : string.Empty;

    /// <summary>True when this line is a continuation line of a multiline event group.</summary>
    public bool IsContinuation { get; set; }

    /// <summary>Reference to the parent group header line if this line is a continuation line.</summary>
    public LogLine? GroupParent { get; set; }

    /// <summary>True when retention started in the middle of an event, so the beginning of the event was not loaded.</summary>
    public bool IsPartialContinuation { get; set; }
}
