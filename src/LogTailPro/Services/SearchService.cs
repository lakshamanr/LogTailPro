using System.Text.RegularExpressions;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>Compiles search patterns and applies them to log lines.</summary>
public sealed class SearchService
{
    /// <summary>
    /// Compiles a search pattern. Returns the regex on success, or an error string on failure.
    /// Returns (null, null) when <paramref name="text"/> is empty.
    /// Always applies a 2-second timeout to prevent ReDoS.
    /// </summary>
    public (Regex? Pattern, string? Error) CompilePattern(
        string? text, bool isRegex, bool caseSensitive)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (null, null);

        RegexOptions options = RegexOptions.Compiled;
        if (!caseSensitive) options |= RegexOptions.IgnoreCase;

        string finalPattern = isRegex ? text : Regex.Escape(text);

        try
        {
            return (new Regex(finalPattern, options, TimeSpan.FromSeconds(2)), null);
        }
        catch (ArgumentException ex)
        {
            return (null, $"Invalid regex: {ex.Message}");
        }
    }

    /// <summary>Filters <paramref name="lines"/> using a single compiled pattern and mode.</summary>
    public IEnumerable<LogLine> ApplyFilter(
        IEnumerable<LogLine> lines,
        Regex pattern,
        FilterMode mode)
    {
        return mode switch
        {
            FilterMode.IncludeMatching => lines.Where(l => SafeIsMatch(pattern, l.Text)),
            FilterMode.ExcludeMatching => lines.Where(l => !SafeIsMatch(pattern, l.Text)),
            _ => lines
        };
    }

    /// <summary>
    /// Evaluates the AND/OR/NOT filter chain against a line.
    /// Returns true if the line should be visible (pass the chain).
    /// Returns true when the chain is empty or all rules are disabled.
    /// </summary>
    public bool LineMatchesChain(
        string lineText,
        IReadOnlyList<(FilterRule Rule, Regex CompiledPattern)> compiledChain)
    {
        IEnumerable<(FilterRule Rule, Regex CompiledPattern)> active =
            compiledChain.Where(c => c.Rule.IsEnabled && !string.IsNullOrEmpty(c.Rule.Pattern));

        if (!active.Any()) return true;

        bool result = true;
        bool first = true;

        foreach ((FilterRule rule, Regex pattern) in active)
        {
            bool matches = SafeIsMatch(pattern, lineText);
            if (rule.IsNegated) matches = !matches;

            if (first)
            {
                result = matches;
                first = false;
            }
            else
            {
                result = rule.Operator == FilterOperator.AND
                    ? result && matches
                    : result || matches;
            }
        }

        return result;
    }

    /// <summary>Compiles all rules in a chain, skipping invalid patterns.</summary>
    public IReadOnlyList<(FilterRule Rule, Regex CompiledPattern)> CompileChain(
        IEnumerable<FilterRule> rules)
    {
        List<(FilterRule, Regex)> compiled = new();

        foreach (FilterRule rule in rules.Where(r => r.IsEnabled && !string.IsNullOrEmpty(r.Pattern)))
        {
            (Regex? pattern, _) = CompilePattern(rule.Pattern, rule.IsRegex, rule.IsCaseSensitive);
            if (pattern != null)
                compiled.Add((rule, pattern));
        }

        return compiled;
    }

    /// <summary>Returns positional capture group values (groups 1..N) for the first match, or an empty array.</summary>
    public string[] GetCapturingGroups(Regex pattern, string text)
    {
        try
        {
            Match match = pattern.Match(text);
            if (!match.Success) return [];
            return Enumerable.Range(1, match.Groups.Count - 1)
                .Select(i => match.Groups[i].Value)
                .ToArray();
        }
        catch (RegexMatchTimeoutException) { return []; }
    }

    private static bool SafeIsMatch(Regex pattern, string text)
    {
        try { return pattern.IsMatch(text); }
        catch (RegexMatchTimeoutException) { return false; }
    }
}
