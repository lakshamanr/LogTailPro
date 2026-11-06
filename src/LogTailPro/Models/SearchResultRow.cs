namespace LogTailPro.Models;

/// <summary>A row in the search results table, capturing a matched line and its regex groups.</summary>
public sealed class SearchResultRow
{
    /// <summary>Timestamp of the line (when it arrived or was parsed); <see cref="DateTime.MinValue"/> if unknown.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Line number of the match within the log.</summary>
    public long LineNumber { get; init; }

    /// <summary>Full text of the matching line.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>First capturing group (empty if none).</summary>
    public string Group1 { get; init; } = string.Empty;

    /// <summary>Second capturing group (empty if none).</summary>
    public string Group2 { get; init; } = string.Empty;

    /// <summary>Third capturing group (empty if none).</summary>
    public string Group3 { get; init; } = string.Empty;

    /// <summary>All capturing groups of the match.</summary>
    public List<string> CapturingGroups { get; init; } = new();

    /// <summary>The log line this row points to (used to jump to it).</summary>
    public LogLine Source { get; init; } = null!;

    /// <summary>Timestamp formatted for the grid, or empty when the line has none.</summary>
    public string TimestampDisplay => Timestamp == DateTime.MinValue ? string.Empty : Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>Capturing groups joined for display, e.g. "42 | GET | /index".</summary>
    public string GroupsDisplay => string.Join(" | ", CapturingGroups.Where(g => g.Length > 0));
}
