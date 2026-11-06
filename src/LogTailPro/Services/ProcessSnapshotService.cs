using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>
/// Enumerates running Windows processes, builds a parent-child tree, and
/// computes per-process CPU% and working-set metrics.
/// Slow-to-query, rarely-changing data (path, user, .NET detection) is cached per PID.
/// </summary>
public sealed class ProcessSnapshotService
{
    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModulesEx(IntPtr hProcess, IntPtr[] lphModule, uint cb,
        out uint lpcbNeeded, uint dwFilterFlag);

    [DllImport("psapi.dll", EntryPoint = "GetModuleBaseNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, StringBuilder lpBaseName, uint nSize);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint LIST_MODULES_ALL = 0x03;
    private static readonly IntPtr InvalidHandle = new(-1);

    /// <summary>Data that does not change for the lifetime of a process.</summary>
    private sealed record StaticInfo(DateTime? StartTime, string Path, bool IsDotNet, string User);

    private readonly Dictionary<int, StaticInfo> _staticCache = new();

    /// <summary>SID → "DOMAIN\user". Account lookups are slow, and few distinct accounts own processes.</summary>
    private readonly ConcurrentDictionary<SecurityIdentifier, string> _accountNames = new();
    private Dictionary<int, TimeSpan> _prevCpuTimes = new();
    private DateTime _prevSampleTime = DateTime.UtcNow;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns a flat snapshot of all running processes with live metrics.</summary>
    /// <remarks>Not thread-safe; call from one thread at a time.</remarks>
    public IReadOnlyList<ProcessInfo> GetAll()
    {
        Dictionary<int, int> parentPids = GetParentPidMap();
        HashSet<int> windowOwners = GetVisibleWindowOwners();
        Process[] procs = Process.GetProcesses();
        DateTime now = DateTime.UtcNow;
        double elapsedSec = Math.Max(0.1, (now - _prevSampleTime).TotalSeconds);
        int cpuCount = Environment.ProcessorCount;
        List<ProcessInfo> result = new(procs.Length);
        Dictionary<int, TimeSpan> newCpu = new(procs.Length);
        HashSet<int> alivePids = new(procs.Length);

        PrefetchStaticInfo(procs);

        foreach (Process p in procs)
        {
            try
            {
                int pid = p.Id;
                alivePids.Add(pid);

                double cpuPct = 0;
                try
                {
                    TimeSpan total = p.TotalProcessorTime;
                    if (_prevCpuTimes.TryGetValue(pid, out TimeSpan prev))
                        cpuPct = Math.Clamp((total - prev).TotalSeconds / elapsedSec / cpuCount * 100.0, 0, 100);
                    newCpu[pid] = total;
                }
                catch { /* access denied on TotalProcessorTime */ }

                StaticInfo info = GetStaticInfo(p, pid);

                result.Add(new ProcessInfo
                {
                    Pid            = pid,
                    Name           = p.ProcessName,
                    ExecutablePath = info.Path,
                    MemoryBytes    = SafeWorkingSet(p),
                    CpuPercent     = cpuPct,
                    Status         = windowOwners.Contains(pid) ? SafeStatus(p) : ProcessStatus.Running,
                    IsDotNet       = info.IsDotNet,
                    ParentPid      = parentPids.TryGetValue(pid, out int ppid) ? ppid : 0,
                    StartTime      = info.StartTime,
                    UserName       = info.User
                });
            }
            catch { /* process exited during enumeration */ }
            finally { p.Dispose(); }
        }

        // Drop cache entries for processes that no longer exist.
        foreach (int dead in _staticCache.Keys.Where(k => !alivePids.Contains(k)).ToList())
            _staticCache.Remove(dead);

        _prevCpuTimes = newCpu;
        _prevSampleTime = now;
        return result;
    }

    /// <summary>
    /// Arranges a flat list into a parent-child tree. Orphans (parent missing, filtered out,
    /// or PID re-used by a newer process) become roots.
    /// </summary>
    public IReadOnlyList<ProcessInfo> BuildTree(IReadOnlyList<ProcessInfo> flat)
    {
        Dictionary<int, ProcessInfo> byPid = new(flat.Count);
        foreach (ProcessInfo p in flat) byPid[p.Pid] = p;

        List<ProcessInfo> roots = new();
        foreach (ProcessInfo p in flat)
        {
            bool hasParent =
                p.ParentPid != 0 &&
                p.ParentPid != p.Pid &&
                byPid.TryGetValue(p.ParentPid, out ProcessInfo? parent) &&
                // A parent that started after its child means the parent PID was recycled.
                !(parent.StartTime.HasValue && p.StartTime.HasValue && parent.StartTime > p.StartTime);

            if (hasParent) byPid[p.ParentPid].Children.Add(p);
            else roots.Add(p);
        }

        foreach (ProcessInfo p in flat)
            p.Children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        return roots.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fills <see cref="_staticCache"/> for processes not seen before (or whose PID was recycled).
    /// The per-process queries are independent and ~15 ms each, so they run in parallel;
    /// afterwards warm refreshes are served entirely from the cache.
    /// </summary>
    private void PrefetchStaticInfo(Process[] procs)
    {
        List<(int Pid, DateTime? Start)> misses = new();
        foreach (Process p in procs)
        {
            try
            {
                int pid = p.Id;
                DateTime? start = SafeStartTime(p);
                if (!_staticCache.TryGetValue(pid, out StaticInfo? cached) || cached.StartTime != start)
                    misses.Add((pid, start));
            }
            catch { /* exited */ }
        }
        if (misses.Count == 0) return;

        ConcurrentBag<(int Pid, StaticInfo Info)> computed = new();
        Parallel.ForEach(misses, m =>
        {
            string path = QueryImagePath(m.Pid);
            computed.Add((m.Pid, new StaticInfo(
                m.Start, path, !string.IsNullOrEmpty(path) && IsDotNetProcess(m.Pid), QueryUserName(m.Pid))));
        });

        foreach ((int pid, StaticInfo info) in computed)
            _staticCache[pid] = info;
    }

    private StaticInfo GetStaticInfo(Process p, int pid)
    {
        DateTime? start = SafeStartTime(p);
        if (_staticCache.TryGetValue(pid, out StaticInfo? cached) && cached.StartTime == start)
            return cached;

        // Process appeared after PrefetchStaticInfo ran — compute inline.
        string path = QueryImagePath(pid);
        StaticInfo info = new(start, path, !string.IsNullOrEmpty(path) && IsDotNetProcess(pid), QueryUserName(pid));
        _staticCache[pid] = info;
        return info;
    }

    private static Dictionary<int, int> GetParentPidMap()
    {
        Dictionary<int, int> map = new();
        IntPtr hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (hSnap == InvalidHandle || hSnap == IntPtr.Zero) return map;
        try
        {
            PROCESSENTRY32 entry = new() { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32First(hSnap, ref entry))
            {
                do { map[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID; }
                while (Process32Next(hSnap, ref entry));
            }
        }
        finally { CloseHandle(hSnap); }
        return map;
    }

    /// <summary>
    /// PIDs owning at least one visible top-level window. <c>Process.Responding</c> is expensive
    /// (it enumerates windows per call), so it is only evaluated for these processes.
    /// </summary>
    private static HashSet<int> GetVisibleWindowOwners()
    {
        HashSet<int> owners = new();
        EnumWindows((hWnd, _) =>
        {
            if (IsWindowVisible(hWnd))
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                owners.Add((int)pid);
            }
            return true;
        }, IntPtr.Zero);
        return owners;
    }

    /// <summary>Reads the image path with limited rights, so it works for most processes (unlike MainModule).</summary>
    private static string QueryImagePath(int pid)
    {
        IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return string.Empty;
        try
        {
            StringBuilder sb = new(1024);
            uint size = (uint)sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : string.Empty;
        }
        finally { CloseHandle(h); }
    }

    /// <summary>Resolves the owning account from the process token (needs only limited query rights).</summary>
    private string QueryUserName(int pid)
    {
        IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProc == IntPtr.Zero) return string.Empty;
        try
        {
            if (!OpenProcessToken(hProc, TOKEN_QUERY, out IntPtr hToken)) return string.Empty;
            try
            {
                using WindowsIdentity id = new(hToken);
                SecurityIdentifier? sid = id.User;
                if (sid == null) return string.Empty;
                return _accountNames.GetOrAdd(sid, static s =>
                {
                    try { return s.Translate(typeof(NTAccount)).Value; }
                    catch (IdentityNotMappedException) { return s.Value; }
                });
            }
            finally { CloseHandle(hToken); }
        }
        catch { return string.Empty; }
        finally { CloseHandle(hProc); }
    }

    private static long SafeWorkingSet(Process p)
    {
        try { return p.WorkingSet64; }
        catch { return 0; }
    }

    private static ProcessStatus SafeStatus(Process p)
    {
        try { return p.Responding ? ProcessStatus.Running : ProcessStatus.NotResponding; }
        catch { return ProcessStatus.Running; }
    }

    /// <summary>
    /// Detects the .NET runtime by looking for coreclr.dll / clr.dll among the loaded modules.
    /// Uses psapi directly: <c>Process.Modules</c> is ~10x slower and throws for protected processes.
    /// </summary>
    private static bool IsDotNetProcess(int pid)
    {
        IntPtr h = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid);
        if (h == IntPtr.Zero) return false;
        try
        {
            IntPtr[] modules = new IntPtr[1024];
            if (!EnumProcessModulesEx(h, modules, (uint)(modules.Length * IntPtr.Size),
                                      out uint needed, LIST_MODULES_ALL))
                return false;

            int count = (int)Math.Min(needed / (uint)IntPtr.Size, (uint)modules.Length);
            StringBuilder name = new(260);
            for (int i = 0; i < count; i++)
            {
                name.Clear();
                if (GetModuleBaseName(h, modules[i], name, (uint)name.Capacity) == 0) continue;
                string n = name.ToString();
                if (n.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("clr.dll", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        finally { CloseHandle(h); }
    }

    private static DateTime? SafeStartTime(Process p)
    {
        try { return p.StartTime; }
        catch { return null; }
    }
}
