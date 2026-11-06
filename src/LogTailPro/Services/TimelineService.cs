using System.Globalization;
using System.Text.RegularExpressions;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>An immutable snapshot of one selected source's loaded records.</summary>
public sealed record TimelineSource(string Path, string Name, IReadOnlyList<LogLine> Lines);

/// <summary>Builds deterministic chronological views without modifying source records.</summary>
public sealed class TimelineService
{
    private static readonly Regex DatedTimestamp = new(
        @"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d{1,7})?(?:Z|[+-]\d{2}:?\d{2})?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    /// <summary>Parses fully dated timestamps; offset-free input uses the local timezone.</summary>
    public static DateTimeOffset? ParseTimestamp(LogLine line)
    {
        if (line.TimelineTimestamp.HasValue)
            return line.TimelineTimestamp.Value;

        if (line.StructuredEvent?.Timestamp.HasValue == true)
            return line.StructuredEvent.Timestamp.Value.ToUniversalTime();

        // Check structured JSON record if not already parsed
        if (line.Text.AsSpan().TrimStart().StartsWith("{"))
        {
            StructuredLogEvent? ev = JsonLogParser.TryParse(line.Text);
            if (ev?.Timestamp.HasValue == true)
                return ev.Timestamp.Value.ToUniversalTime();
        }

        string head = line.Text[..Math.Min(line.Text.Length, 100)];
        Match match = DatedTimestamp.Match(head);
        if (match.Success)
        {
            string value = match.Value.Replace(',', '.');
            // Normalize compact offsets, e.g. +0530, before parsing.
            if (value.Length >= 5 && (value[^5] == '+' || value[^5] == '-') && value[^3] != ':')
                value = value.Insert(value.Length - 2, ":");
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out DateTimeOffset parsed))
                return parsed.ToUniversalTime();
            return null; // A malformed full date must not fall back to a partial time.
        }

        // The existing parser also recognizes day/month/year formats. Only use its
        // result when a full date is present; time-only input must remain undated.
        if (line.Timestamp is DateTime timestamp && timestamp.Year > 1 &&
            Regex.IsMatch(head, @"\b\d{2}/\d{2}/\d{4}\b", RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)))
            return new DateTimeOffset(timestamp).ToUniversalTime();
        return null;
    }

    /// <summary>Merges snapshots, normalizes timezones and resolves equal/unknown times deterministically.</summary>
    public IReadOnlyList<LogLine> Merge(IReadOnlyList<TimelineSource> sources, CancellationToken ct = default)
    {
        List<(LogLine Row, int Source, int Index)> rows = new();
        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            TimelineSource source = sources[sourceIndex];
            for (int index = 0; index < source.Lines.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                LogLine original = source.Lines[index];
                DateTimeOffset? timestamp = ParseTimestamp(original);
                rows.Add((new LogLine
                {
                    LineNumber = original.LineNumber,
                    Text = original.Text,
                    Timestamp = timestamp?.UtcDateTime,
                    TimelineTimestamp = timestamp,
                    SourceFileName = source.Name,
                    SourceFilePath = source.Path,
                    OriginalLine = original,
                    ByteOffset = original.ByteOffset,
                    ArrivedAt = original.ArrivedAt,
                    IsBookmarked = original.IsBookmarked,
                    StructuredEvent = original.StructuredEvent
                }, sourceIndex, index));
            }
        }
        // Stable tie-breakers preserve source/line identity even for repeated timestamps.
        List<LogLine> sorted = rows.OrderBy(row => row.Row.TimelineTimestamp ?? DateTimeOffset.MaxValue)
            .ThenBy(row => row.Source).ThenBy(row => row.Row.SourceLineNumber).ThenBy(row => row.Index)
            .Select(row => row.Row).ToList();
        ct.ThrowIfCancellationRequested();
        return sorted;
    }
}
