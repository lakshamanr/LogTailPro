using System.Text.RegularExpressions;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>
/// Matches processes against a user pattern. Supports plain text (substring), wildcards
/// (<c>*</c> and <c>?</c>, several alternatives separated by comma or semicolon) and regular expressions.
/// The pattern is tested against name, PID, executable path and user name.
/// </summary>
public sealed class ProcessSearchMatcher
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly char[] AlternativeSeparators = { ',', ';' };

    private readonly Regex? _regex;
    private readonly string[] _plainTerms = Array.Empty<string>();

    private ProcessSearchMatcher(Regex? regex, string[] plainTerms)
    {
        _regex = regex;
        _plainTerms = plainTerms;
    }

    /// <summary>A matcher that accepts every process (empty pattern).</summary>
    public static ProcessSearchMatcher MatchAll { get; } = new(null, Array.Empty<string>());

    /// <summary>
    /// Builds a matcher. Returns null and sets <paramref name="error"/> when a regex pattern is invalid.
    /// </summary>
    public static ProcessSearchMatcher? Create(string pattern, bool isRegex, out string? error)
    {
        error = null;
        string trimmed = pattern.Trim();
        if (trimmed.Length == 0) return MatchAll;

        try
        {
            if (isRegex)
                return new ProcessSearchMatcher(BuildRegex(trimmed), Array.Empty<string>());

            string[] terms = trimmed
                .Split(AlternativeSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (terms.Length == 0) return MatchAll;

            bool hasWildcard = terms.Any(t => t.Contains('*') || t.Contains('?'));
            if (!hasWildcard)
                return new ProcessSearchMatcher(null, terms);

            // Wildcards: an unanchored regex so "w3wp*" still matches inside a path.
            string alternation = string.Join("|", terms.Select(WildcardToRegex));
            return new ProcessSearchMatcher(BuildRegex(alternation), Array.Empty<string>());
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return null;
        }
    }

    /// <summary>Returns true when <paramref name="p"/> matches the pattern.</summary>
    public bool IsMatch(ProcessInfo p)
    {
        if (_regex == null && _plainTerms.Length == 0) return true;

        string pid = p.Pid.ToString();
        if (_regex != null)
        {
            try
            {
                return _regex.IsMatch(p.Name) || _regex.IsMatch(pid) ||
                       _regex.IsMatch(p.ExecutablePath) || _regex.IsMatch(p.UserName);
            }
            catch (RegexMatchTimeoutException) { return false; }
        }

        foreach (string term in _plainTerms)
        {
            if (p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                pid.Contains(term, StringComparison.Ordinal) ||
                p.ExecutablePath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.UserName.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true when <paramref name="s"/> matches the pattern, tested against service name,
    /// display name, hosting PID, binary path and account.
    /// </summary>
    public bool IsMatch(ServiceInfo s)
    {
        if (_regex == null && _plainTerms.Length == 0) return true;

        string pid = s.Pid > 0 ? s.Pid.ToString() : string.Empty;
        if (_regex != null)
        {
            try
            {
                return _regex.IsMatch(s.Name) || _regex.IsMatch(s.DisplayName) || _regex.IsMatch(pid) ||
                       _regex.IsMatch(s.BinaryPath) || _regex.IsMatch(s.AccountName);
            }
            catch (RegexMatchTimeoutException) { return false; }
        }

        foreach (string term in _plainTerms)
        {
            if (s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                s.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                pid.Contains(term, StringComparison.Ordinal) ||
                s.BinaryPath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                s.AccountName.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static Regex BuildRegex(string pattern) =>
        new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);

    private static string WildcardToRegex(string wildcard) =>
        Regex.Escape(wildcard).Replace("\\*", ".*").Replace("\\?", ".");
}
