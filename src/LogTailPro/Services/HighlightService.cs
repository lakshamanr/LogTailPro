using System.Text.RegularExpressions;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>Matches log lines against user-defined highlight rules.</summary>
public sealed class HighlightService
{
    private readonly SearchService _searchService = new();

    /// <summary>
    /// Pre-compiles all enabled rules into regex patterns sorted by priority.
    /// Call this once when rules change, not per-line.
    /// </summary>
    public IReadOnlyList<(HighlightRule Rule, Regex CompiledPattern)> CompileRules(
        IEnumerable<HighlightRule> rules)
    {
        return rules
            .Where(r => r.IsEnabled && !string.IsNullOrEmpty(r.Pattern))
            .OrderBy(r => r.Priority)
            .Select(r =>
            {
                (Regex? compiled, _) = _searchService.CompilePattern(r.Pattern, r.IsRegex, r.IsCaseSensitive);
                return (Rule: r, CompiledPattern: compiled!);
            })
            .Where(pair => pair.CompiledPattern != null)
            .ToList();
    }

    /// <summary>
    /// Returns the first matching rule for <paramref name="lineText"/> (lowest priority wins).
    /// Returns null if no rule matches.
    /// </summary>
    public HighlightRule? GetMatchingRule(
        string lineText,
        IReadOnlyList<(HighlightRule Rule, Regex CompiledPattern)> compiledRules)
    {
        foreach ((HighlightRule rule, Regex pattern) in compiledRules)
        {
            try
            {
                if (pattern.IsMatch(lineText))
                    return rule;
            }
            catch (RegexMatchTimeoutException)
            {
                // Silently skip — never crash the viewer
            }
        }

        return null;
    }
}
