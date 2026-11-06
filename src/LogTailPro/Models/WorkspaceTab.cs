namespace LogTailPro.Models;

/// <summary>Type of tab stored in a workspace.</summary>
public enum WorkspaceTabType
{
    File = 0,
    Timeline = 1,
    Correlation = 2
}

/// <summary>Persists the state of a single log-viewer, timeline, or correlation tab in a saved workspace.</summary>
public sealed class WorkspaceTab
{
    public WorkspaceTabType TabType { get; set; } = WorkspaceTabType.File;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public bool IsRegex { get; set; }
    public bool IsCaseSensitive { get; set; }
    public FilterMode FilterMode { get; set; } = FilterMode.None;
    public List<long> BookmarkedLines { get; set; } = new();
    public bool IsFollowTail { get; set; }
    public double ScrollVerticalOffset { get; set; }
    public double ScrollHorizontalOffset { get; set; }
    public int ScrollLineIndex
    {
        get => (int)ScrollVerticalOffset;
        set { if (ScrollVerticalOffset == 0) ScrollVerticalOffset = value; }
    }
    public bool IsStructuredViewEnabled { get; set; } = true;
    public List<string> SourceFilePaths { get; set; } = new();
    public string? CorrelationId { get; set; }
}
