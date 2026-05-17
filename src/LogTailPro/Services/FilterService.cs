using System.Text.RegularExpressions;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>Applies include/exclude filters to collections of log lines.</summary>
public sealed class FilterService
{
    private readonly SearchService _searchService = new();

    /// <summary>
    /// Returns a filtered view of <paramref name="lines"/> according to the given pattern and mode.
    /// If <paramref name="pattern"/> is null or empty, all lines are returned.
    /// </summary>
    public IEnumerable<LogLine> Apply(
        IEnumerable<LogLine> lines,
        string? pattern,
        bool isRegex,
        bool caseSensitive,
        FilterMode mode)
    {
        if (mode == FilterMode.None || string.IsNullOrEmpty(pattern))
            return lines;

        (Regex? compiled, _) = _searchService.CompilePattern(pattern, isRegex, caseSensitive);
        if (compiled == null)
            return lines;

        return _searchService.ApplyFilter(lines, compiled, mode);
    }
}
