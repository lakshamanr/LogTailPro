using System.Globalization;
using System.Text.RegularExpressions;

namespace LogTailPro.Services;

/// <summary>
/// Extracts timestamps from log line text by trying common log format patterns.
/// Returns null if no recognized format is found.
/// </summary>
public sealed class TimestampParser
{
    private static readonly (Regex Pattern, string[] Formats)[] Patterns =
    [
        // 2024-01-15 14:23:45.123
        (new Regex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}", RegexOptions.Compiled),
         ["yyyy-MM-dd HH:mm:ss.fff"]),

        // 2024-01-15 14:23:45
        (new Regex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", RegexOptions.Compiled),
         ["yyyy-MM-dd HH:mm:ss"]),

        // 2024-01-15T14:23:45.123Z  /  2024-01-15T14:23:45
        (new Regex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?", RegexOptions.Compiled),
         ["yyyy-MM-ddTHH:mm:ss.fffZ", "yyyy-MM-ddTHH:mm:ssZ",
          "yyyy-MM-ddTHH:mm:ss.fff",  "yyyy-MM-ddTHH:mm:ss"]),

        // 15/01/2024 14:23:45
        (new Regex(@"\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2}", RegexOptions.Compiled),
         ["dd/MM/yyyy HH:mm:ss", "MM/dd/yyyy HH:mm:ss"]),

        // 14:23:45.123
        (new Regex(@"\d{2}:\d{2}:\d{2}\.\d{3}", RegexOptions.Compiled),
         ["HH:mm:ss.fff"]),

        // 14:23:45
        (new Regex(@"\d{2}:\d{2}:\d{2}", RegexOptions.Compiled),
         ["HH:mm:ss"]),
    ];

    private int _lastSuccessfulIndex = -1;

    /// <summary>
    /// Tries to extract and parse a timestamp from the beginning or early part of a log line.
    /// Returns null when no recognized timestamp format is found.
    /// </summary>
    public DateTime? TryParse(string lineText)
    {
        if (string.IsNullOrEmpty(lineText)) return null;

        // Only scan the first 60 characters — timestamps are always at the start
        ReadOnlySpan<char> head = lineText.Length > 60
            ? lineText.AsSpan(0, 60)
            : lineText.AsSpan();

        // Fast rejection: every supported timestamp format contains digits and at least one colon for time
        bool hasDigit = false;
        bool hasColon = false;
        for (int i = 0; i < head.Length; i++)
        {
            char c = head[i];
            if (char.IsAsciiDigit(c)) hasDigit = true;
            else if (c == ':') hasColon = true;
            if (hasDigit && hasColon) break;
        }
        if (!hasDigit || !hasColon) return null;

        string headStr = lineText.Length <= 60 ? lineText : lineText[..60];

        // Adaptive cache: try the pattern that matched the previous line first (99%+ hit rate in standard logs)
        int lastIdx = _lastSuccessfulIndex;
        if (lastIdx >= 0 && lastIdx < Patterns.Length)
        {
            DateTime? result = TryMatch(Patterns[lastIdx], headStr);
            if (result.HasValue) return result;
        }

        for (int i = 0; i < Patterns.Length; i++)
        {
            if (i == lastIdx) continue;
            DateTime? result = TryMatch(Patterns[i], headStr);
            if (result.HasValue)
            {
                _lastSuccessfulIndex = i;
                return result;
            }
        }

        return null;
    }

    private static DateTime? TryMatch((Regex Pattern, string[] Formats) entry, string headStr)
    {
        Match m = entry.Pattern.Match(headStr);
        if (!m.Success) return null;

        foreach (string fmt in entry.Formats)
        {
            if (DateTime.TryParseExact(
                m.Value, fmt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime dt))
            {
                return dt;
            }
        }
        return null;
    }
}
