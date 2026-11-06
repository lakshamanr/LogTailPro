namespace LogTailPro.Models;

/// <summary>A single rule in a multi-condition filter chain.</summary>
public sealed class FilterRule
{
    /// <summary>Text, regex pattern, or typed field expression (e.g. "StatusCode >= 400") to evaluate.</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>True if <see cref="Pattern"/> is a regular expression.</summary>
    public bool IsRegex { get; set; }

    /// <summary>True if matching should be case-sensitive.</summary>
    public bool IsCaseSensitive { get; set; }

    /// <summary>How this rule combines with the previous rule in the chain.</summary>
    public FilterOperator Operator { get; set; } = FilterOperator.AND;

    /// <summary>When true, the match result is inverted (NOT logic).</summary>
    public bool IsNegated { get; set; }

    /// <summary>Whether this rule participates in evaluation.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Parsed typed field filter if the pattern is a structured condition.</summary>
    public TypedFieldFilter? TypedFilter { get; set; }
}

/// <summary>How a filter rule combines with the previous rule's result.</summary>
public enum FilterOperator
{
    /// <summary>Both this rule and the previous result must be true.</summary>
    AND,

    /// <summary>Either this rule or the previous result may be true.</summary>
    OR
}
