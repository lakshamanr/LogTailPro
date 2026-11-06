using System.Globalization;
using System.Text.RegularExpressions;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>
/// Aggregation output data for the error timeline chart.
/// </summary>
public sealed class ErrorTimelineChartData
{
    public IReadOnlyList<TimeBucket> Buckets { get; init; } = Array.Empty<TimeBucket>();
    public int TotalEvents { get; init; }
    public int TotalErrors { get; init; }
    public int TotalWarnings { get; init; }
    public int TotalInfos { get; init; }
    public int TotalOthers { get; init; }

    public int UndatedCount { get; init; }
    public int UndatedErrors { get; init; }
    public int UndatedWarnings { get; init; }
    public int UndatedInfos { get; init; }
    public int UndatedOthers { get; init; }

    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public TimeSpan BucketInterval { get; init; }
    public string BucketIntervalDescription { get; init; } = string.Empty;
    public string SourceScope { get; init; } = string.Empty;
    public string TimezoneDescription { get; init; } = string.Empty;
}

/// <summary>
/// Aggregates log events into adaptive time buckets by severity (Error, Warning, Info, Other).
/// Accurately counts distinct root events without inflating counts for multiline continuation frames,
/// keeps bucket counts bounded, and explicitly handles undated records separately.
/// </summary>
public sealed class ErrorTimelineChartService
{
    private static readonly Regex ErrorRegex = new(
        @"(?:^|\s|\[)(?:error|fatal|critical|crit|err|crash|exception)(?:$|\s|\]|:)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WarningRegex = new(
        @"(?:^|\s|\[)(?:warning|warn|wrn)(?:$|\s|\]|:)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex InfoRegex = new(
        @"(?:^|\s|\[)(?:information|info|inf|debug|dbg|trace|trc|verbose)(?:$|\s|\]|:)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Normalizes event severity to <see cref="LogSeverity"/>.
    /// Uses structured event level if available; otherwise scans plain text keywords.
    /// </summary>
    public static LogSeverity NormalizeSeverity(LogLine line)
    {
        if (line.StructuredEvent != null && !string.IsNullOrWhiteSpace(line.StructuredEvent.Level))
        {
            string lvl = line.StructuredEvent.Level.Trim();
            if (string.Equals(lvl, "Error", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lvl, "Fatal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lvl, "Critical", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lvl, "Err", StringComparison.OrdinalIgnoreCase))
                return LogSeverity.Error;

            if (string.Equals(lvl, "Warning", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lvl, "Warn", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lvl, "Wrn", StringComparison.OrdinalIgnoreCase))
                return LogSeverity.Warning;

            if (string.Equals(lvl, "Information", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lvl, "Info", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lvl, "Debug", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lvl, "Trace", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lvl, "Verbose", StringComparison.OrdinalIgnoreCase))
                return LogSeverity.Information;

            return LogSeverity.Other;
        }

        // Check for exception on the line or attached continuations
        if (line.HasContinuations || (line.StructuredEvent != null && !string.IsNullOrEmpty(line.StructuredEvent.Exception)))
            return LogSeverity.Error;

        string text = line.Text;
        if (ErrorRegex.IsMatch(text)) return LogSeverity.Error;
        if (WarningRegex.IsMatch(text)) return LogSeverity.Warning;
        if (InfoRegex.IsMatch(text)) return LogSeverity.Information;

        return LogSeverity.Other;
    }

    /// <summary>
    /// Extracts the timestamp in UTC for a log event.
    /// </summary>
    public static DateTimeOffset? ExtractTimestamp(LogLine line)
    {
        if (line.TimelineTimestamp.HasValue) return line.TimelineTimestamp.Value;
        if (line.StructuredEvent?.Timestamp.HasValue == true)
            return line.StructuredEvent.Timestamp.Value.ToUniversalTime();

        DateTimeOffset? parsed = TimelineService.ParseTimestamp(line);
        if (parsed.HasValue) return parsed.Value;

        if (line.Timestamp is DateTime dt && dt.Year > 1900)
            return new DateTimeOffset(dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime());

        return null;
    }

    /// <summary>
    /// Computes a clean human-friendly time interval for the given duration to yield approximately
    /// <paramref name="targetBucketCount"/> buckets (bounded between 10 and 100).
    /// </summary>
    public static TimeSpan CalculateBucketInterval(TimeSpan duration, int targetBucketCount = 50)
    {
        if (duration <= TimeSpan.Zero) return TimeSpan.FromMinutes(1);

        double totalSeconds = Math.Max(1.0, duration.TotalSeconds);
        double rawStepSeconds = totalSeconds / Math.Max(10, Math.Min(100, targetBucketCount));

        // Friendly interval steps in seconds
        double[] steps =
        [
            1, 2, 5, 10, 15, 30,             // Seconds
            60, 120, 300, 600, 900, 1800,    // Minutes (1m, 2m, 5m, 10m, 15m, 30m)
            3600, 7200, 14400, 21600, 43200, // Hours (1h, 2h, 4h, 6h, 12h)
            86400, 172800, 604800            // Days (1d, 2d, 7d)
        ];

        foreach (double step in steps)
        {
            if (rawStepSeconds <= step)
                return TimeSpan.FromSeconds(step);
        }

        return TimeSpan.FromDays(7);
    }

    public static string FormatIntervalDescription(TimeSpan interval)
    {
        if (interval.TotalDays >= 1)
            return interval.TotalDays == 1 ? "1 day" : $"{interval.TotalDays:0.#} days";
        if (interval.TotalHours >= 1)
            return interval.TotalHours == 1 ? "1 hour" : $"{interval.TotalHours:0.#} hours";
        if (interval.TotalMinutes >= 1)
            return interval.TotalMinutes == 1 ? "1 minute" : $"{interval.TotalMinutes:0.#} minutes";
        return interval.TotalSeconds == 1 ? "1 second" : $"{interval.TotalSeconds:0.#} seconds";
    }

    /// <summary>
    /// Aggregates the collection of events into an adaptive timeline chart model.
    /// Skips multiline continuation frames so that exception groups count as 1 event.
    /// </summary>
    public ErrorTimelineChartData BuildChartData(
        IReadOnlyList<LogLine> lines,
        string sourceScope,
        CancellationToken ct = default)
    {
        int totalEvents = 0;
        int totalErrors = 0;
        int totalWarnings = 0;
        int totalInfos = 0;
        int totalOthers = 0;

        int undatedCount = 0;
        int undatedErrors = 0;
        int undatedWarnings = 0;
        int undatedInfos = 0;
        int undatedOthers = 0;

        List<(DateTimeOffset Timestamp, LogSeverity Severity)> datedEvents = new();

        DateTimeOffset minTime = DateTimeOffset.MaxValue;
        DateTimeOffset maxTime = DateTimeOffset.MinValue;

        for (int i = 0; i < lines.Count; i++)
        {
            if ((i & 0x7FFF) == 0) ct.ThrowIfCancellationRequested();
            LogLine line = lines[i];

            // Multiline continuation lines belong to their parent group root; never count them separately.
            if (line.IsContinuation) continue;

            totalEvents++;
            LogSeverity severity = NormalizeSeverity(line);

            switch (severity)
            {
                case LogSeverity.Error: totalErrors++; break;
                case LogSeverity.Warning: totalWarnings++; break;
                case LogSeverity.Information: totalInfos++; break;
                default: totalOthers++; break;
            }

            DateTimeOffset? timestamp = ExtractTimestamp(line);
            if (timestamp.HasValue)
            {
                DateTimeOffset utc = timestamp.Value.ToUniversalTime();
                datedEvents.Add((utc, severity));
                if (utc < minTime) minTime = utc;
                if (utc > maxTime) maxTime = utc;
            }
            else
            {
                undatedCount++;
                switch (severity)
                {
                    case LogSeverity.Error: undatedErrors++; break;
                    case LogSeverity.Warning: undatedWarnings++; break;
                    case LogSeverity.Information: undatedInfos++; break;
                    default: undatedOthers++; break;
                }
            }
        }

        ct.ThrowIfCancellationRequested();

        if (datedEvents.Count == 0 || minTime > maxTime)
        {
            return new ErrorTimelineChartData
            {
                Buckets = Array.Empty<TimeBucket>(),
                TotalEvents = totalEvents,
                TotalErrors = totalErrors,
                TotalWarnings = totalWarnings,
                TotalInfos = totalInfos,
                TotalOthers = totalOthers,
                UndatedCount = undatedCount,
                UndatedErrors = undatedErrors,
                UndatedWarnings = undatedWarnings,
                UndatedInfos = undatedInfos,
                UndatedOthers = undatedOthers,
                SourceScope = sourceScope,
                TimezoneDescription = TimeZoneInfo.Local.DisplayName
            };
        }

        TimeSpan duration = maxTime - minTime;
        TimeSpan interval = CalculateBucketInterval(duration);

        // Align start time to clean interval boundary
        long stepSeconds = Math.Max(1, (long)interval.TotalSeconds);
        long startUnix = (minTime.ToUnixTimeSeconds() / stepSeconds) * stepSeconds;
        DateTimeOffset bucketStart = DateTimeOffset.FromUnixTimeSeconds(startUnix);

        long endUnix = maxTime.ToUnixTimeSeconds();
        int bucketCount = (int)Math.Max(1, ((endUnix - startUnix) / stepSeconds) + 1);

        // Cap bucket count for safety (maximum 120 buckets)
        if (bucketCount > 120)
        {
            interval = CalculateBucketInterval(duration, targetBucketCount: 60);
            stepSeconds = Math.Max(1, (long)interval.TotalSeconds);
            startUnix = (minTime.ToUnixTimeSeconds() / stepSeconds) * stepSeconds;
            bucketStart = DateTimeOffset.FromUnixTimeSeconds(startUnix);
            bucketCount = (int)Math.Max(1, ((endUnix - startUnix) / stepSeconds) + 1);
        }

        TimeBucket[] buckets = new TimeBucket[bucketCount];
        for (int b = 0; b < bucketCount; b++)
        {
            DateTimeOffset bStart = bucketStart.AddSeconds(b * stepSeconds);
            DateTimeOffset bEnd = bStart.AddSeconds(stepSeconds);

            // Format label for display in user's local timezone
            DateTimeOffset localStart = bStart.ToLocalTime();
            string label = stepSeconds < 60
                ? localStart.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                : (stepSeconds < 86400
                    ? localStart.ToString("HH:mm", CultureInfo.InvariantCulture)
                    : localStart.ToString("MM-dd", CultureInfo.InvariantCulture));

            buckets[b] = new TimeBucket
            {
                StartTime = bStart,
                EndTime = bEnd,
                TimeLabel = label
            };
        }

        // Fill buckets
        foreach ((DateTimeOffset time, LogSeverity sev) in datedEvents)
        {
            long offsetSec = time.ToUnixTimeSeconds() - startUnix;
            int idx = (int)(offsetSec / stepSeconds);
            if (idx < 0) idx = 0;
            if (idx >= bucketCount) idx = bucketCount - 1;

            TimeBucket b = buckets[idx];
            switch (sev)
            {
                case LogSeverity.Error: b.ErrorCount++; break;
                case LogSeverity.Warning: b.WarningCount++; break;
                case LogSeverity.Information: b.InfoCount++; break;
                default: b.OtherCount++; break;
            }
        }

        // Compute max total for proportional bar heights and format tooltips
        int maxTotal = 1;
        for (int b = 0; b < bucketCount; b++)
        {
            if (buckets[b].TotalCount > maxTotal)
                maxTotal = buckets[b].TotalCount;
        }

        for (int b = 0; b < bucketCount; b++)
        {
            TimeBucket bucket = buckets[b];
            bucket.MaxBucketTotal = maxTotal;

            string sTime = bucket.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string eTime = bucket.EndTime.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            bucket.ToolTipText = $"{sTime} – {eTime}\n" +
                                 $"Errors: {bucket.ErrorCount:N0}\n" +
                                 $"Warnings: {bucket.WarningCount:N0}\n" +
                                 $"Information: {bucket.InfoCount:N0}\n" +
                                 $"Other: {bucket.OtherCount:N0}\n" +
                                 $"Total: {bucket.TotalCount:N0} events";
        }

        return new ErrorTimelineChartData
        {
            Buckets = buckets,
            TotalEvents = totalEvents,
            TotalErrors = totalErrors,
            TotalWarnings = totalWarnings,
            TotalInfos = totalInfos,
            TotalOthers = totalOthers,
            UndatedCount = undatedCount,
            UndatedErrors = undatedErrors,
            UndatedWarnings = undatedWarnings,
            UndatedInfos = undatedInfos,
            UndatedOthers = undatedOthers,
            StartTime = minTime,
            EndTime = maxTime,
            BucketInterval = interval,
            BucketIntervalDescription = FormatIntervalDescription(interval),
            SourceScope = sourceScope,
            TimezoneDescription = $"{TimeZoneInfo.Local.DisplayName} (UTC{(TimeZoneInfo.Local.GetUtcOffset(DateTime.Now) >= TimeSpan.Zero ? "+" : "-")}{Math.Abs(TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).Hours):D2}:{Math.Abs(TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).Minutes):D2})"
        };
    }
}
