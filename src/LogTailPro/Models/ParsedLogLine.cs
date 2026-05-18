namespace LogTailPro.Models;

/// <summary>A log line that has been parsed into structured columns by a <see cref="LogParserConfig"/>.</summary>
public sealed class ParsedLogLine
{
    public string RawText   { get; init; } = string.Empty;
    public string Timestamp { get; init; } = string.Empty;
    public string Level     { get; init; } = string.Empty;
    public string Thread    { get; init; } = string.Empty;
    public string Class     { get; init; } = string.Empty;
    public string Message   { get; init; } = string.Empty;
}
