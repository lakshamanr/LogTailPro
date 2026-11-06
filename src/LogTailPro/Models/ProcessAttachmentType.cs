namespace LogTailPro.Models;

/// <summary>Which output stream to capture from a process.</summary>
public enum ProcessAttachmentType
{
    /// <summary>stdout — only available for processes launched by LogTailPro.</summary>
    StandardOutput,

    /// <summary>stderr — only available for processes launched by LogTailPro.</summary>
    StandardError,

    /// <summary>OutputDebugString via Win32 shared memory (works for ALL running processes).</summary>
    DebugOutput,

    /// <summary>Windows Event Log entries filtered to the selected process name.</summary>
    WindowsEventLog,

    /// <summary>stdout and stderr interleaved — only available for processes launched by LogTailPro.</summary>
    StandardOutputAndError
}
