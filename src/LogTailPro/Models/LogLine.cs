namespace LogTailPro.Models;

/// <summary>Represents a single line in the log file.</summary>
public sealed class LogLine
{
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
    public bool IsBookmarked { get; set; }

    /// <summary>Source file name (used in merged tabs to label lines from different files).</summary>
    public string? SourceFileName { get; set; }

    /// <summary>Wall-clock time when this line arrived from the file watcher. DateTime.MinValue for initial load.</summary>
    public DateTime ArrivedAt { get; set; } = DateTime.MinValue;
}
