namespace LogTailPro.Models;

/// <summary>A persisted bookmark: identifies a line by its number and text so it can be re-found after a reload.</summary>
public sealed class BookmarkEntry
{
    /// <summary>Line number within the loaded log when the bookmark was saved.</summary>
    public long LineNumber { get; set; }

    /// <summary>Line text (truncated to <see cref="MaxTextLength"/> characters).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Maximum number of characters of the line stored in settings.</summary>
    public const int MaxTextLength = 500;
}
