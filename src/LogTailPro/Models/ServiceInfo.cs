namespace LogTailPro.Models;

/// <summary>Snapshot of a Windows service as reported by the Service Control Manager.</summary>
public sealed class ServiceInfo
{
    /// <summary>Short service name (the key used by <c>sc.exe</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Display name shown in services.msc.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Current run state.</summary>
    public ServiceState State { get; init; }

    /// <summary>Configured start type.</summary>
    public ServiceStartType StartType { get; init; }

    /// <summary>Process id hosting the service, or 0 when the service is not running.</summary>
    public int Pid { get; init; }

    /// <summary>Account the service runs under, or empty when it could not be read.</summary>
    public string AccountName { get; init; } = string.Empty;

    /// <summary>Command line of the service binary, or empty when it could not be read.</summary>
    public string BinaryPath { get; init; } = string.Empty;

    /// <summary>True when the service currently has a hosting process.</summary>
    public bool IsRunning => Pid > 0;

    /// <summary>
    /// Executable of the service binary with arguments and quotes removed and environment variables expanded
    /// (e.g. <c>"C:\Program Files\X\x.exe" -k grp</c> becomes <c>C:\Program Files\X\x.exe</c>); empty when unknown.
    /// </summary>
    public string ExecutablePath
    {
        get
        {
            string cmd = Environment.ExpandEnvironmentVariables(BinaryPath.Trim());
            if (cmd.Length == 0) return string.Empty;

            if (cmd[0] == '"')
            {
                int close = cmd.IndexOf('"', 1);
                return close > 1 ? cmd[1..close] : cmd.Trim('"');
            }

            // Unquoted: the path may contain spaces, so cut at the first ".exe".
            int exe = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return exe >= 0 ? cmd[..(exe + 4)] : cmd;
        }
    }

    /// <summary>Human-readable state.</summary>
    public string StateText => State switch
    {
        ServiceState.Running      => "Running",
        ServiceState.Stopped      => "Stopped",
        ServiceState.Paused       => "Paused",
        ServiceState.StartPending => "Starting…",
        ServiceState.StopPending  => "Stopping…",
        _                         => "Pending…"
    };

    /// <summary>Human-readable start type.</summary>
    public string StartTypeText => StartType switch
    {
        ServiceStartType.Automatic => "Automatic",
        ServiceStartType.Manual    => "Manual",
        ServiceStartType.Disabled  => "Disabled",
        ServiceStartType.Boot      => "Boot",
        ServiceStartType.System    => "System",
        _                          => "—"
    };
}

/// <summary>Run state of a Windows service.</summary>
public enum ServiceState
{
    /// <summary>Not running.</summary>
    Stopped = 1,

    /// <summary>Starting up.</summary>
    StartPending = 2,

    /// <summary>Shutting down.</summary>
    StopPending = 3,

    /// <summary>Running.</summary>
    Running = 4,

    /// <summary>Continue is pending.</summary>
    ContinuePending = 5,

    /// <summary>Pause is pending.</summary>
    PausePending = 6,

    /// <summary>Paused.</summary>
    Paused = 7
}

/// <summary>Configured start type of a Windows service.</summary>
public enum ServiceStartType
{
    /// <summary>Unknown / unreadable.</summary>
    Unknown = -1,

    /// <summary>Started by the boot loader (driver).</summary>
    Boot = 0,

    /// <summary>Started by the I/O subsystem (driver).</summary>
    System = 1,

    /// <summary>Started automatically at boot.</summary>
    Automatic = 2,

    /// <summary>Started on demand.</summary>
    Manual = 3,

    /// <summary>Cannot be started.</summary>
    Disabled = 4
}
