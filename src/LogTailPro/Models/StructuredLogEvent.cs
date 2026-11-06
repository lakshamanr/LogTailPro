using System.Globalization;
using System.Text.Json;

namespace LogTailPro.Models;

/// <summary>Represents a structured log record parsed from JSON or structured logging format.</summary>
public sealed class StructuredLogEvent
{
    /// <summary>Original raw text of the line.</summary>
    public string RawText { get; init; } = string.Empty;

    /// <summary>Parsed timestamp preserving timezone offset, or null if missing/unparseable.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>Normalized or original severity / log level (e.g. Information, Warning, Error, Fatal, Debug, Verbose, Trace).</summary>
    public string? Level { get; init; }

    /// <summary>Rendered log message (e.g. from @m, message, msg).</summary>
    public string? Message { get; init; }

    /// <summary>Message template format string (e.g. from @mt, template, messageTemplate). Distinct from rendered message.</summary>
    public string? MessageTemplate { get; init; }

    /// <summary>Exception details or stack trace (e.g. from @x, exception, exc, error).</summary>
    public string? Exception { get; init; }

    /// <summary>All parsed top-level and custom properties, preserving their native primitive types, nested objects, and arrays.</summary>
    public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>True if JSON parsing succeeded for this record.</summary>
    public bool IsValidJson { get; init; }

    /// <summary>Description of the parse error if JSON was malformed.</summary>
    public string? ParseError { get; init; }

    /// <summary>
    /// Attempts to look up a property value by name or dot-notation path (e.g. "User.Id" or "StatusCode" or "@l").
    /// Checks common aliases for Level, Message, MessageTemplate, Exception, and Timestamp first.
    /// </summary>
    public bool TryGetProperty(string path, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(path)) return false;

        string normalized = path.Trim();

        // Check top-level well-known fields and aliases
        if (IsLevelField(normalized))
        {
            if (Level != null) { value = Level; return true; }
            if (Properties.TryGetValue(normalized, out value)) return true;
            return false;
        }

        if (IsMessageField(normalized))
        {
            if (Message != null) { value = Message; return true; }
            if (Properties.TryGetValue(normalized, out value)) return true;
            return false;
        }

        if (IsTemplateField(normalized))
        {
            if (MessageTemplate != null) { value = MessageTemplate; return true; }
            if (Properties.TryGetValue(normalized, out value)) return true;
            return false;
        }

        if (IsExceptionField(normalized))
        {
            if (Exception != null) { value = Exception; return true; }
            if (Properties.TryGetValue(normalized, out value)) return true;
            return false;
        }

        if (IsTimestampField(normalized))
        {
            if (Timestamp.HasValue) { value = Timestamp.Value; return true; }
            if (Properties.TryGetValue(normalized, out value)) return true;
            return false;
        }

        // Check properties dictionary (supporting dot-notation for nested objects)
        if (Properties.TryGetValue(normalized, out value))
            return true;

        if (normalized.Contains('.'))
        {
            string[] segments = normalized.Split('.');
            object? current = Properties;
            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];
                if (current is IReadOnlyDictionary<string, object?> dict)
                {
                    if (!dict.TryGetValue(seg, out current))
                        return false;
                }
                else if (current is IDictionary<string, object?> mdict)
                {
                    if (!mdict.TryGetValue(seg, out current))
                        return false;
                }
                else
                {
                    return false;
                }
            }
            value = current;
            return true;
        }

        return false;
    }

    private static bool IsLevelField(string name) =>
        name.Equals("Level", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("@l", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("severity", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("loglevel", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("log_level", StringComparison.OrdinalIgnoreCase);

    private static bool IsMessageField(string name) =>
        name.Equals("Message", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("@m", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("msg", StringComparison.OrdinalIgnoreCase);

    private static bool IsTemplateField(string name) =>
        name.Equals("MessageTemplate", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("@mt", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("template", StringComparison.OrdinalIgnoreCase);

    private static bool IsExceptionField(string name) =>
        name.Equals("Exception", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("@x", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("exc", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("error", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("stackTrace", StringComparison.OrdinalIgnoreCase);

    private static bool IsTimestampField(string name) =>
        name.Equals("Timestamp", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("@t", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("time", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("@timestamp", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("date", StringComparison.OrdinalIgnoreCase);
}
