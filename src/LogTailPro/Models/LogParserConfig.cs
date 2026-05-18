namespace LogTailPro.Models;

/// <summary>Defines a named regex-based log format parser that extracts structured columns.</summary>
public sealed class LogParserConfig
{
    /// <summary>Display name shown in the preset list (e.g. "Log4net", "NLog").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full-line regex with named capture groups:
    /// (?&lt;Timestamp&gt;…) (?&lt;Level&gt;…) (?&lt;Thread&gt;…) (?&lt;Class&gt;…) (?&lt;Message&gt;…)
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>Returns built-in presets for common log frameworks.</summary>
    public static IReadOnlyList<LogParserConfig> BuiltInPresets() =>
    [
        new LogParserConfig
        {
            Name = "Log4net / Log4j",
            Pattern = @"^(?<Timestamp>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}[,\.]\d{3})\s+\[(?<Thread>[^\]]*)\]\s+(?<Level>\w+)\s+(?<Class>\S+)\s+-\s+(?<Message>.*)$"
        },
        new LogParserConfig
        {
            Name = "NLog",
            Pattern = @"^(?<Timestamp>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}\.\d{4})\|(?<Level>\w+)\|(?<Class>[^|]*)\|(?<Message>.*)$"
        },
        new LogParserConfig
        {
            Name = "Serilog (JSON)",
            Pattern = @"^\{""@t"":""(?<Timestamp>[^""]+)"",""@l"":""(?<Level>[^""]+)"",""@m"":""(?<Message>[^""]+)"""
        },
        new LogParserConfig
        {
            Name = "IIS / W3C",
            Pattern = @"^(?<Timestamp>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2})\s+\S+\s+\S+\s+(?<Class>\S+)\s+\S+\s+(?<Level>\d+)\s+\S+\s+\S+\s+(?<Message>.*)$"
        },
    ];
}
