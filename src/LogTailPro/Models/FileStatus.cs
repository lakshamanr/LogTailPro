namespace LogTailPro.Models;

/// <summary>Lifecycle state of a watched log file.</summary>
public enum FileStatus
{
    /// <summary>File is being actively tailed.</summary>
    Watching,

    /// <summary>Watching is paused (read-only / merged tab).</summary>
    Paused,

    /// <summary>An error occurred reading the file.</summary>
    Error,

    /// <summary>The file was deleted or moved away.</summary>
    Deleted
}
