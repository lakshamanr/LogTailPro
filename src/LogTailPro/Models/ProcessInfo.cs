namespace LogTailPro.Models;

/// <summary>Snapshot of a running operating system process.</summary>
public sealed class ProcessInfo
{
    /// <summary>Operating system process id.</summary>
    public int Pid { get; init; }

    /// <summary>Process name without extension.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Full path of the main executable, or empty when inaccessible.</summary>
    public string ExecutablePath { get; init; } = string.Empty;

    /// <summary>Working-set size in bytes.</summary>
    public long MemoryBytes { get; init; }

    /// <summary>CPU usage since the previous sample, 0–100 (normalised across all cores).</summary>
    public double CpuPercent { get; init; }

    /// <summary>Responsiveness state of the process.</summary>
    public ProcessStatus Status { get; init; }

    /// <summary>True when the process hosts the .NET runtime (coreclr.dll / clr.dll loaded).</summary>
    public bool IsDotNet { get; init; }

    /// <summary>Parent process id (0 when unknown).</summary>
    public int ParentPid { get; init; }

    /// <summary>Process start time, when it could be read.</summary>
    public DateTime? StartTime { get; init; }

    /// <summary>Account the process runs under (DOMAIN\user), or empty when inaccessible.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Mutable — populated by <c>ProcessSnapshotService.BuildTree</c>.</summary>
    public List<ProcessInfo> Children { get; } = new();

    /// <summary>True for kernel/service-level processes that are hidden by default.</summary>
    public bool IsSystemProcess =>
        Pid <= 4 ||
        string.IsNullOrEmpty(UserName) ||
        UserName.StartsWith("NT AUTHORITY\\", StringComparison.OrdinalIgnoreCase) ||
        UserName.StartsWith("Window Manager\\", StringComparison.OrdinalIgnoreCase) ||
        UserName.StartsWith("Font Driver Host\\", StringComparison.OrdinalIgnoreCase);

    /// <summary>Human-readable working set.</summary>
    public string MemoryText => MemoryBytes switch
    {
        0                     => "—",
        < 1024 * 1024         => $"{MemoryBytes / 1024} KB",
        < 1024L * 1024 * 1024 => $"{MemoryBytes / (1024.0 * 1024):F0} MB",
        _                     => $"{MemoryBytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    /// <summary>Human-readable CPU usage.</summary>
    public string CpuText => CpuPercent < 0.05 ? "—" : $"{CpuPercent:F1}%";

    /// <summary>"name [pid]" label used in the tree and tab titles.</summary>
    public string DisplayName => $"{Name} [{Pid}]";

    /// <summary>Formatted start time.</summary>
    public string StartTimeText => StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
}

/// <summary>Process execution state.</summary>
public enum ProcessStatus
{
    /// <summary>Process is running normally.</summary>
    Running,

    /// <summary>Process has a UI that is not pumping messages.</summary>
    NotResponding,

    /// <summary>Process is suspended.</summary>
    Suspended
}
