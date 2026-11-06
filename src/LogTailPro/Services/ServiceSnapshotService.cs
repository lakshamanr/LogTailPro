using System.ComponentModel;
using System.Runtime.InteropServices;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>
/// Enumerates and controls Windows services through the Service Control Manager (advapi32 P/Invoke,
/// so no extra NuGet package is needed). Rarely-changing configuration (start type, account, binary path)
/// is cached per service name.
/// </summary>
public sealed class ServiceSnapshotService
{
    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", EntryPoint = "EnumServicesStatusExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumServicesStatusEx(
        IntPtr hSCManager, int infoLevel, uint serviceType, uint serviceState,
        IntPtr services, uint bufSize, out uint bytesNeeded, out uint servicesReturned,
        ref uint resumeHandle, string? groupName);

    [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr hSCManager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", EntryPoint = "QueryServiceConfigW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryServiceConfig(IntPtr hService, IntPtr config, uint bufSize, out uint bytesNeeded);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatusEx(
        IntPtr hService, int infoLevel, out SERVICE_STATUS_PROCESS status, uint bufSize, out uint bytesNeeded);

    [DllImport("advapi32.dll", EntryPoint = "StartServiceW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool StartService(IntPtr hService, uint numArgs, IntPtr args);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ControlService(IntPtr hService, uint control, out SERVICE_STATUS_PROCESS status);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS_PROCESS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
        public uint dwProcessId;
        public uint dwServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ENUM_SERVICE_STATUS_PROCESS
    {
        public IntPtr lpServiceName;
        public IntPtr lpDisplayName;
        public SERVICE_STATUS_PROCESS ServiceStatusProcess;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct QUERY_SERVICE_CONFIG
    {
        public uint dwServiceType;
        public uint dwStartType;
        public uint dwErrorControl;
        public IntPtr lpBinaryPathName;
        public IntPtr lpLoadOrderGroup;
        public uint dwTagId;
        public IntPtr lpDependencies;
        public IntPtr lpServiceStartName;
        public IntPtr lpDisplayName;
    }

    private const uint SC_MANAGER_CONNECT = 0x0001;
    private const uint SC_MANAGER_ENUMERATE_SERVICE = 0x0004;
    private const uint SERVICE_QUERY_CONFIG = 0x0001;
    private const uint SERVICE_QUERY_STATUS = 0x0004;
    private const uint SERVICE_START = 0x0010;
    private const uint SERVICE_STOP = 0x0020;
    private const uint SERVICE_WIN32 = 0x00000030;
    private const uint SERVICE_STATE_ALL = 0x00000003;
    private const uint SERVICE_CONTROL_STOP = 1;
    private const int SC_ENUM_PROCESS_INFO = 0;
    private const int SC_STATUS_PROCESS_INFO = 0;
    private const int ERROR_MORE_DATA = 234;
    private const int ERROR_ACCESS_DENIED = 5;

    /// <summary>Start type, account and binary path of a service; read once per service name.</summary>
    private sealed record ServiceConfig(ServiceStartType StartType, string Account, string BinaryPath);

    private readonly Dictionary<string, ServiceConfig> _configCache = new(StringComparer.OrdinalIgnoreCase);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns every Win32 service (running or stopped), sorted by display name.</summary>
    /// <remarks>Not thread-safe; call from one thread at a time.</remarks>
    /// <exception cref="Win32Exception">The Service Control Manager could not be opened or enumerated.</exception>
    public IReadOnlyList<ServiceInfo> GetAll()
    {
        IntPtr scm = OpenSCManager(null, null, SC_MANAGER_CONNECT | SC_MANAGER_ENUMERATE_SERVICE);
        if (scm == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            List<ServiceInfo> result = new();
            int entrySize = Marshal.SizeOf<ENUM_SERVICE_STATUS_PROCESS>();
            uint resume = 0;
            uint bufSize = 64 * 1024;
            IntPtr buffer = Marshal.AllocHGlobal((int)bufSize);

            try
            {
                while (true)
                {
                    bool ok = EnumServicesStatusEx(scm, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_STATE_ALL,
                        buffer, bufSize, out uint needed, out uint returned, ref resume, null);
                    int error = ok ? 0 : Marshal.GetLastWin32Error();
                    if (!ok && error != ERROR_MORE_DATA) throw new Win32Exception(error);

                    for (int i = 0; i < returned; i++)
                    {
                        ENUM_SERVICE_STATUS_PROCESS entry =
                            Marshal.PtrToStructure<ENUM_SERVICE_STATUS_PROCESS>(buffer + i * entrySize);
                        string name = Marshal.PtrToStringUni(entry.lpServiceName) ?? string.Empty;
                        ServiceConfig config = GetConfig(scm, name);

                        result.Add(new ServiceInfo
                        {
                            Name = name,
                            DisplayName = Marshal.PtrToStringUni(entry.lpDisplayName) ?? name,
                            State = ToState(entry.ServiceStatusProcess.dwCurrentState),
                            Pid = (int)entry.ServiceStatusProcess.dwProcessId,
                            StartType = config.StartType,
                            AccountName = config.Account,
                            BinaryPath = config.BinaryPath
                        });
                    }

                    if (ok) break;
                    // More data: grow the buffer if a single entry batch did not fit, then continue from resume.
                    if (needed > bufSize)
                    {
                        bufSize = needed;
                        buffer = Marshal.ReAllocHGlobal(buffer, (IntPtr)bufSize);
                    }
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }

            result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            return result;
        }
        finally { CloseServiceHandle(scm); }
    }

    /// <summary>Starts the service and waits (up to 30 s) until it is running.</summary>
    /// <exception cref="InvalidOperationException">The operation failed; the message is user-readable.</exception>
    public void Start(string serviceName) => StartCore(serviceName);

    /// <summary>Stops the service and waits (up to 30 s) until it is stopped.</summary>
    /// <exception cref="InvalidOperationException">The operation failed; the message is user-readable.</exception>
    public void Stop(string serviceName) => StopCore(serviceName);

    /// <summary>Stops the service (if running) and starts it again.</summary>
    /// <exception cref="InvalidOperationException">The operation failed; the message is user-readable.</exception>
    public void Restart(string serviceName)
    {
        StopCore(serviceName);
        StartCore(serviceName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ServiceConfig GetConfig(IntPtr scm, string name)
    {
        if (_configCache.TryGetValue(name, out ServiceConfig? cached)) return cached;

        ServiceConfig config = new(ServiceStartType.Unknown, string.Empty, string.Empty);
        IntPtr svc = OpenService(scm, name, SERVICE_QUERY_CONFIG);
        if (svc != IntPtr.Zero)
        {
            try
            {
                QueryServiceConfig(svc, IntPtr.Zero, 0, out uint needed);
                if (needed > 0)
                {
                    IntPtr buf = Marshal.AllocHGlobal((int)needed);
                    try
                    {
                        if (QueryServiceConfig(svc, buf, needed, out _))
                        {
                            QUERY_SERVICE_CONFIG raw = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(buf);
                            config = new ServiceConfig(
                                Enum.IsDefined(typeof(ServiceStartType), (int)raw.dwStartType)
                                    ? (ServiceStartType)raw.dwStartType : ServiceStartType.Unknown,
                                Marshal.PtrToStringUni(raw.lpServiceStartName) ?? string.Empty,
                                Marshal.PtrToStringUni(raw.lpBinaryPathName) ?? string.Empty);
                        }
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
            }
            finally { CloseServiceHandle(svc); }
        }

        _configCache[name] = config;
        return config;
    }

    private static ServiceState ToState(uint raw) =>
        Enum.IsDefined(typeof(ServiceState), (int)raw) ? (ServiceState)(int)raw : ServiceState.Stopped;

    private void StartCore(string name)
    {
        _configCache.Remove(name);
        IntPtr svc = OpenForControl(name, SERVICE_START | SERVICE_QUERY_STATUS);
        try
        {
            if (!StartService(svc, 0, IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                const int ErrorAlreadyRunning = 1056;
                if (error != ErrorAlreadyRunning) throw Failure("start", name, error);
            }
            WaitForState(svc, ServiceState.Running, name, "start");
        }
        finally { CloseServiceHandle(svc); }
    }

    private void StopCore(string name)
    {
        IntPtr svc = OpenForControl(name, SERVICE_STOP | SERVICE_QUERY_STATUS);
        try
        {
            if (!ControlService(svc, SERVICE_CONTROL_STOP, out _))
            {
                int error = Marshal.GetLastWin32Error();
                const int ErrorNotActive = 1062;
                if (error != ErrorNotActive) throw Failure("stop", name, error);
            }
            WaitForState(svc, ServiceState.Stopped, name, "stop");
        }
        finally { CloseServiceHandle(svc); }
    }

    private static IntPtr OpenForControl(string name, uint access)
    {
        IntPtr scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero) throw Failure("open", name, Marshal.GetLastWin32Error());
        try
        {
            IntPtr svc = OpenService(scm, name, access);
            if (svc == IntPtr.Zero) throw Failure("open", name, Marshal.GetLastWin32Error());
            return svc;
        }
        finally { CloseServiceHandle(scm); }
    }

    private static void WaitForState(IntPtr svc, ServiceState target, string name, string verb)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (!QueryServiceStatusEx(svc, SC_STATUS_PROCESS_INFO, out SERVICE_STATUS_PROCESS status,
                    (uint)Marshal.SizeOf<SERVICE_STATUS_PROCESS>(), out _))
                throw Failure(verb, name, Marshal.GetLastWin32Error());

            if (ToState(status.dwCurrentState) == target) return;
            Thread.Sleep(250);
        }
        throw new InvalidOperationException($"Timed out waiting to {verb} service '{name}'.");
    }

    private static InvalidOperationException Failure(string verb, string name, int error) =>
        error == ERROR_ACCESS_DENIED
            ? new InvalidOperationException(
                $"Access denied while trying to {verb} service '{name}'. Run LogTailPro as administrator.")
            : new InvalidOperationException(
                $"Could not {verb} service '{name}': {new Win32Exception(error).Message}");
}
