using System.IO;
using System.Text;

namespace LogTailPro.Models;

/// <summary>Metadata for an open log file.</summary>
public sealed class LogFile
{
    /// <summary>Full path to the log file on disk.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>File name without directory path.</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>Current size of the file in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Detected or assumed encoding of the file.</summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>True while the file watcher is running.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Last-modified timestamp from the filesystem.</summary>
    public DateTime LastModified { get; set; }
}
