namespace LogTailPro.Models;

/// <summary>How a log file was discovered for a process (combinable).</summary>
[Flags]
public enum LogSource
{
    /// <summary>Not set.</summary>
    None = 0,

    /// <summary>The running process currently has the file open.</summary>
    OpenHandle = 1,

    /// <summary>
    /// A configuration file that merely sits in the same folder as the executable references the file.
    /// In shared folders (e.g. a common <c>bin</c> directory) this may belong to a different application.
    /// </summary>
    Config = 2,

    /// <summary>Found by scanning the executable's usual log folders.</summary>
    FolderScan = 4,

    /// <summary>The executable's <em>own</em> configuration file (<c>&lt;exe&gt;.config</c> etc.) references the file.</summary>
    OwnConfig = 8
}

/// <summary>A log file that probably belongs to a process.</summary>
public sealed class LogCandidate
{
    /// <summary>Full path of the log file.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Size in bytes at discovery time.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Last write time (UTC).</summary>
    public DateTime LastWriteUtc { get; init; }

    /// <summary>All sources that pointed at this file.</summary>
    public LogSource Sources { get; set; }

    /// <summary>Human-readable explanation, e.g. "NLog fileName in app.exe.config".</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Confidence score; higher is more likely the process' main log.</summary>
    public int Score { get; set; }

    /// <summary>Name of the owning process.</summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>PID of the owning process.</summary>
    public int Pid { get; init; }

    /// <summary>True for the single best candidate of a process.</summary>
    public bool IsRecommended { get; set; }

    /// <summary>File name without directory.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>Directory containing the file.</summary>
    public string Directory => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;

    /// <summary>Formatted size.</summary>
    public string SizeText => SizeBytes switch
    {
        < 1024                => $"{SizeBytes} B",
        < 1024 * 1024         => $"{SizeBytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024):F1} MB",
        _                     => $"{SizeBytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    /// <summary>Formatted local last-write time.</summary>
    public string LastWriteText => LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>Short source badges, e.g. "handle + config".</summary>
    public string SourceText
    {
        get
        {
            List<string> parts = new();
            if (Sources.HasFlag(LogSource.OpenHandle)) parts.Add("open handle");
            if (Sources.HasFlag(LogSource.OwnConfig)) parts.Add("own config");
            if (Sources.HasFlag(LogSource.Config)) parts.Add("config");
            if (Sources.HasFlag(LogSource.FolderScan)) parts.Add("folder scan");
            return string.Join(" + ", parts);
        }
    }

    /// <summary>"name [pid]" shown in the picker.</summary>
    public string ProcessText => Pid > 0 ? $"{ProcessName} [{Pid}]" : ProcessName;
}
