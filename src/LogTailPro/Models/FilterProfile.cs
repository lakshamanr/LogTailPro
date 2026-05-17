namespace LogTailPro.Models;

/// <summary>A saved named set of filter settings the user can recall.</summary>
public sealed class FilterProfile
{
    /// <summary>Stable unique identifier for this profile.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>User-visible name for the profile.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The filter pattern (plain text or regex).</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>True if <see cref="Pattern"/> is a regular expression.</summary>
    public bool IsRegex { get; set; }

    /// <summary>True if matching should be case-sensitive.</summary>
    public bool IsCaseSensitive { get; set; }

    /// <summary>Include or exclude matching lines.</summary>
    public FilterMode Mode { get; set; } = FilterMode.IncludeMatching;
}

/// <summary>Determines how filter results are displayed.</summary>
public enum FilterMode
{
    /// <summary>No filtering applied — all lines shown.</summary>
    None,

    /// <summary>Only lines matching the pattern are shown.</summary>
    IncludeMatching,

    /// <summary>Lines matching the pattern are hidden.</summary>
    ExcludeMatching
}
