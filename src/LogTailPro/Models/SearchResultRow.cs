namespace LogTailPro.Models;

/// <summary>A row in the search results table, capturing a matched line and its regex groups.</summary>
public sealed class SearchResultRow
{
    public DateTime Timestamp { get; init; }
    public long LineNumber { get; init; }
    public string Text { get; init; } = string.Empty;
    public string Group1 { get; init; } = string.Empty;
    public string Group2 { get; init; } = string.Empty;
    public string Group3 { get; init; } = string.Empty;
    public List<string> CapturingGroups { get; init; } = new();
    public LogLine Source { get; init; } = null!;
}
