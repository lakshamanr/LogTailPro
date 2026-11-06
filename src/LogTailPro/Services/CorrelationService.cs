using System.Text.RegularExpressions;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>Result returned by a multi-source correlation search.</summary>
public sealed record CorrelationResult(
    IReadOnlyList<LogLine> Rows,
    int TotalMatches,
    bool IsTruncated,
    int SearchedSourcesCount);

/// <summary>
/// Correlates log events across multiple sources by exact Request ID, Trace ID, or correlation token.
/// Preserves exact values, excludes partial substring matches, searches nested JSON properties and plain-text tokens.
/// </summary>
public sealed class CorrelationService
{
    private const int DefaultMaxResults = 50_000;

    private static readonly string[] CommonCorrelationKeys =
    [
        "TraceId", "@tr", "trace_id", "traceId",
        "RequestId", "request_id", "requestId", "reqId", "req_id", "x-request-id",
        "CorrelationId", "correlation_id", "correlationId", "x-correlation-id",
        "SpanId", "@sp", "span_id", "spanId",
        "TransactionId", "transaction_id", "transId"
    ];

    private static readonly Regex GuidRegex = new(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.Compiled);

    private static readonly Regex W3CTraceRegex = new(
        @"\b[0-9a-fA-F]{32}\b",
        RegexOptions.Compiled);

    private static readonly Regex KeyValueIdRegex = new(
        @"(?:trace_?id|request_?id|correlation_?id|req_?id|span_?id)[=:\s]+[""']?([a-zA-Z0-9_\-\.]{4,64})[""']?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PrefixedTokenRegex = new(
        @"\b(?:req|trace|corr|txn|span)[-_][a-zA-Z0-9_\-]{3,64}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Attempts to extract a likely correlation ID (TraceId, RequestId, GUID, W3C trace, or prefixed token) from a log line.
    /// </summary>
    public static string? ExtractSuggestedId(LogLine line)
    {
        if (line.StructuredEvent != null && line.StructuredEvent.IsValidJson)
        {
            foreach (string key in CommonCorrelationKeys)
            {
                if (line.StructuredEvent.TryGetProperty(key, out object? val) && val != null)
                {
                    string str = val.ToString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(str)) return str;
                }
            }
        }
        else if (line.Text.AsSpan().TrimStart().StartsWith("{"))
        {
            StructuredLogEvent? ev = JsonLogParser.TryParse(line.Text);
            if (ev != null && ev.IsValidJson)
            {
                foreach (string key in CommonCorrelationKeys)
                {
                    if (ev.TryGetProperty(key, out object? val) && val != null)
                    {
                        string str = val.ToString()?.Trim() ?? string.Empty;
                        if (!string.IsNullOrEmpty(str)) return str;
                    }
                }
            }
        }

        // Try extracting known patterns from raw text
        Match kv = KeyValueIdRegex.Match(line.Text);
        if (kv.Success && kv.Groups[1].Length > 0)
            return kv.Groups[1].Value;

        Match pref = PrefixedTokenRegex.Match(line.Text);
        if (pref.Success)
            return pref.Value;

        Match guid = GuidRegex.Match(line.Text);
        if (guid.Success)
            return guid.Value;

        Match w3c = W3CTraceRegex.Match(line.Text);
        if (w3c.Success)
            return w3c.Value;

        if (line.HasContinuations)
        {
            foreach (LogLine cont in line.Continuations)
            {
                string? contId = ExtractSuggestedId(cont);
                if (!string.IsNullOrEmpty(contId)) return contId;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks whether a log line matches the target correlation ID.
    /// Uses exact value matching on structured properties and word-boundary matching on plain text.
    /// Never matches substring prefixes/suffixes (e.g. 'abc' will not match 'abcdef').
    /// </summary>
    public static bool MatchesCorrelationId(LogLine line, string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId)) return false;
        string id = targetId.Trim();
        if ((id.StartsWith('"') && id.EndsWith('"')) || (id.StartsWith('\'') && id.EndsWith('\'')))
        {
            if (id.Length >= 2) id = id[1..^1].Trim();
        }

        // 1. Structured JSON properties check (exact match)
        StructuredLogEvent? ev = line.StructuredEvent;
        if ((ev == null || !ev.IsValidJson) && line.Text.AsSpan().TrimStart().StartsWith("{"))
        {
            ev = JsonLogParser.TryParse(line.Text);
        }

        if (ev != null && ev.IsValidJson)
        {
            // First check common correlation keys directly
            foreach (string key in CommonCorrelationKeys)
            {
                if (ev.TryGetProperty(key, out object? val) && val != null)
                {
                    if (string.Equals(val.ToString()?.Trim(), id, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // Check all other custom properties recursively
            if (CheckPropertiesMatch(ev.Properties, id))
                return true;
        }

        // 2. Plain text / raw line check with strict token boundary
        // Pattern matches id only when not preceded or followed by word characters or dashes
        bool isMatch = false;
        try
        {
            string pattern = $@"(?<![\w\-]){Regex.Escape(id)}(?![\w\-])";
            isMatch = Regex.IsMatch(line.Text, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
        }
        catch (RegexMatchTimeoutException)
        {
        }

        if (isMatch) return true;

        if (line.HasContinuations)
        {
            foreach (LogLine cont in line.Continuations)
            {
                if (MatchesCorrelationId(cont, id))
                    return true;
            }
        }

        return false;
    }

    private static bool CheckPropertiesMatch(IReadOnlyDictionary<string, object?> properties, string targetId)
    {
        foreach (KeyValuePair<string, object?> kvp in properties)
        {
            if (kvp.Value == null) continue;

            if (kvp.Value is string or sbyte or byte or short or ushort or int or uint or long or ulong)
            {
                if (string.Equals(kvp.Value.ToString()?.Trim(), targetId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (kvp.Value is IReadOnlyDictionary<string, object?> dict)
            {
                if (CheckPropertiesMatch(dict, targetId)) return true;
            }
            else if (kvp.Value is IReadOnlyList<object?> list)
            {
                foreach (object? item in list)
                {
                    if (item == null) continue;
                    if (item is IReadOnlyDictionary<string, object?> itemDict)
                    {
                        if (CheckPropertiesMatch(itemDict, targetId)) return true;
                    }
                    else if (string.Equals(item.ToString()?.Trim(), targetId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Searches loaded records across all provided sources for matching correlation IDs,
    /// and returns them sorted chronologically in UTC with stable tie-breaking.
    /// </summary>
    public CorrelationResult MergeCorrelated(
        IReadOnlyList<TimelineSource> sources,
        string correlationId,
        int maxResults = DefaultMaxResults,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || sources.Count == 0)
            return new CorrelationResult(Array.Empty<LogLine>(), 0, false, sources.Count);

        string id = correlationId.Trim();
        if ((id.StartsWith('"') && id.EndsWith('"')) || (id.StartsWith('\'') && id.EndsWith('\'')))
        {
            if (id.Length >= 2) id = id[1..^1].Trim();
        }

        List<(LogLine Row, int Source, int Index)> matches = new();
        int totalFound = 0;

        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            TimelineSource source = sources[sourceIndex];
            for (int index = 0; index < source.Lines.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                LogLine original = source.Lines[index];

                if (MatchesCorrelationId(original, id))
                {
                    totalFound++;
                    if (matches.Count < maxResults)
                    {
                        DateTimeOffset? timestamp = TimelineService.ParseTimestamp(original);
                        matches.Add((new LogLine
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
            }
        }

        // Deterministic chronological ordering with source and original line ties
        List<LogLine> sorted = matches
            .OrderBy(row => row.Row.TimelineTimestamp ?? DateTimeOffset.MaxValue)
            .ThenBy(row => row.Source)
            .ThenBy(row => row.Row.SourceLineNumber)
            .ThenBy(row => row.Index)
            .Select(row => row.Row)
            .ToList();

        ct.ThrowIfCancellationRequested();

        return new CorrelationResult(
            sorted,
            totalFound,
            totalFound > maxResults,
            sources.Count);
    }
}
