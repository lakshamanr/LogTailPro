using System.Globalization;
using System.Text.Json;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>
/// Fast, robust parser for line-delimited JSON (JSONL) and Serilog-style structured log records.
/// Preserves native property types and nested structures. Never throws unhandled exceptions.
/// </summary>
public static class JsonLogParser
{
    private const int MaxLineLengthForJson = 2 * 1024 * 1024; // 2 MB guard against massive corrupted lines

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        MaxDepth = 32,
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Attempts to parse a single line into a <see cref="StructuredLogEvent"/>.
    /// Returns null if the line is plainly not JSON (e.g. does not begin with '{').
    /// Returns an event with <see cref="StructuredLogEvent.IsValidJson"/> = false if JSON parsing failed.
    /// </summary>
    public static StructuredLogEvent? TryParse(string lineText)
    {
        if (string.IsNullOrWhiteSpace(lineText)) return null;

        ReadOnlySpan<char> span = lineText.AsSpan().TrimStart();
        if (span.IsEmpty || span[0] != '{')
            return null; // Ordinary plain-text log line

        if (lineText.Length > MaxLineLengthForJson)
        {
            return new StructuredLogEvent
            {
                RawText = lineText,
                IsValidJson = false,
                ParseError = "Line length exceeds maximum JSON threshold (2 MB)."
            };
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(lineText, DocumentOptions);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new StructuredLogEvent
                {
                    RawText = lineText,
                    IsValidJson = false,
                    ParseError = "JSON root is not an object."
                };
            }

            DateTimeOffset? timestamp = null;
            string? level = null;
            string? message = null;
            string? messageTemplate = null;
            string? exception = null;
            Dictionary<string, object?> properties = new(StringComparer.OrdinalIgnoreCase);

            bool isSerilogClef = false;

            foreach (JsonProperty prop in root.EnumerateObject())
            {
                string name = prop.Name;
                JsonElement val = prop.Value;

                // 1. Serilog Compact CLEF and standard timestamp
                if (name == "@t" || IsTimestampName(name))
                {
                    if (val.ValueKind == JsonValueKind.String &&
                        DateTimeOffset.TryParse(val.GetString(), CultureInfo.InvariantCulture,
                            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out DateTimeOffset dto))
                    {
                        timestamp = dto;
                    }
                    if (name == "@t") isSerilogClef = true;
                }
                // 2. Serilog CLEF and standard Level
                else if (name == "@l" || IsLevelName(name))
                {
                    if (val.ValueKind == JsonValueKind.String)
                        level = val.GetString();
                    else
                        level = val.ToString();
                    if (name == "@l") isSerilogClef = true;
                }
                // 3. Rendered Message
                else if (name == "@m" || IsMessageName(name))
                {
                    if (val.ValueKind == JsonValueKind.String)
                        message = val.GetString();
                    else
                        message = val.ToString();
                    if (name == "@m") isSerilogClef = true;
                }
                // 4. Message Template (distinct from rendered message)
                else if (name == "@mt" || IsTemplateName(name))
                {
                    if (val.ValueKind == JsonValueKind.String)
                        messageTemplate = val.GetString();
                    else
                        messageTemplate = val.ToString();
                    if (name == "@mt") isSerilogClef = true;
                }
                // 5. Exception
                else if (name == "@x" || IsExceptionName(name))
                {
                    if (val.ValueKind == JsonValueKind.String)
                        exception = val.GetString();
                    else
                        exception = val.ToString();
                    if (name == "@x") isSerilogClef = true;
                }
                // 6. Custom or structured property
                else
                {
                    properties[name] = ConvertElement(val);
                }
            }

            // In Serilog CLEF, if @l is omitted, default level is "Information"
            if (isSerilogClef && string.IsNullOrEmpty(level) && (message != null || messageTemplate != null))
            {
                level = "Information";
            }

            return new StructuredLogEvent
            {
                RawText = lineText,
                Timestamp = timestamp,
                Level = NormalizeLevel(level),
                Message = message,
                MessageTemplate = messageTemplate,
                Exception = exception,
                Properties = properties,
                IsValidJson = true
            };
        }
        catch (JsonException jex)
        {
            return new StructuredLogEvent
            {
                RawText = lineText,
                IsValidJson = false,
                ParseError = $"Malformed JSON: {jex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new StructuredLogEvent
            {
                RawText = lineText,
                IsValidJson = false,
                ParseError = $"Parse error: {ex.Message}"
            };
        }
    }

    private static object? ConvertElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt64(out long l))
                    return l;
                if (element.TryGetDouble(out double d))
                    return d;
                return element.GetRawText();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;

            case JsonValueKind.Object:
                Dictionary<string, object?> obj = new(StringComparer.OrdinalIgnoreCase);
                foreach (JsonProperty p in element.EnumerateObject())
                    obj[p.Name] = ConvertElement(p.Value);
                return obj;

            case JsonValueKind.Array:
                List<object?> list = new();
                foreach (JsonElement item in element.EnumerateArray())
                    list.Add(ConvertElement(item));
                return list;

            default:
                return element.GetRawText();
        }
    }

    private static bool IsTimestampName(string name) =>
        name.Equals("timestamp", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("time", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("@timestamp", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("date", StringComparison.OrdinalIgnoreCase);

    private static bool IsLevelName(string name) =>
        name.Equals("level", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("severity", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("loglevel", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("log_level", StringComparison.OrdinalIgnoreCase);

    private static bool IsMessageName(string name) =>
        name.Equals("message", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("msg", StringComparison.OrdinalIgnoreCase);

    private static bool IsTemplateName(string name) =>
        name.Equals("messageTemplate", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("template", StringComparison.OrdinalIgnoreCase);

    private static bool IsExceptionName(string name) =>
        name.Equals("exception", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("exc", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("error", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("stackTrace", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level)) return null;
        string trimmed = level.Trim();

        if (trimmed.Equals("INF", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("INFO", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Information", StringComparison.OrdinalIgnoreCase))
            return "Information";

        if (trimmed.Equals("WRN", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("WARN", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Warning", StringComparison.OrdinalIgnoreCase))
            return "Warning";

        if (trimmed.Equals("ERR", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Error", StringComparison.OrdinalIgnoreCase))
            return "Error";

        if (trimmed.Equals("FTL", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("FATAL", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Fatal", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Critical", StringComparison.OrdinalIgnoreCase))
            return "Fatal";

        if (trimmed.Equals("DBG", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("DEBUG", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Debug", StringComparison.OrdinalIgnoreCase))
            return "Debug";

        if (trimmed.Equals("VRB", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("VERBOSE", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Verbose", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("TRACE", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Trace", StringComparison.OrdinalIgnoreCase))
            return "Verbose";

        return trimmed;
    }
}
