namespace LogTailPro.Models;

/// <summary>A user-defined rule that colorizes matching log lines.</summary>
public sealed class HighlightRule
{
    /// <summary>Stable unique identifier for this rule.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Text or regex pattern to match against.</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>True if <see cref="Pattern"/> is a regular expression.</summary>
    public bool IsRegex { get; set; }

    /// <summary>True if matching should be case-sensitive.</summary>
    public bool IsCaseSensitive { get; set; }

    /// <summary>Foreground (text) color as an HTML hex string, e.g. "#FFFFFF".</summary>
    public string ForegroundColor { get; set; } = "#FFFFFF";

    /// <summary>Background color as an HTML hex string, e.g. "#FF0000".</summary>
    public string BackgroundColor { get; set; } = "#FF0000";

    /// <summary>Whether this rule is active during rendering.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Evaluation order — lower value means evaluated first.</summary>
    public int Priority { get; set; }
}
