namespace LogTailPro.Models;

/// <summary>A user-saved search pattern that can be recalled from the search panel.</summary>
public sealed class SavedSearchPattern
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public bool IsRegex { get; set; }
    public bool IsCaseSensitive { get; set; }
    public FilterMode FilterMode { get; set; } = FilterMode.None;
}
