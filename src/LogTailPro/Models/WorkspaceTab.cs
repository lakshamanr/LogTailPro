namespace LogTailPro.Models;

/// <summary>Persists the state of a single log-viewer tab in a saved workspace.</summary>
public sealed class WorkspaceTab
{
    public string FilePath { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public bool IsRegex { get; set; }
    public bool IsCaseSensitive { get; set; }
    public FilterMode FilterMode { get; set; } = FilterMode.None;
    public List<long> BookmarkedLines { get; set; } = new();
}
