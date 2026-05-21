# PHASE 7 — Process Explorer (JetBrains dotPeek-style Side Panel)

## Goal
By the end of this phase:
- A collapsible left side panel lists every running Windows process in a **tree view** (parent → child)
- The panel has a search box, toolbar (Refresh, Auto-refresh, .NET-only filter), and a details strip
- Right-clicking a process shows a context menu: Capture Debug Output, Monitor Event Log, Launch & Attach, Open in Explorer, Copy PID, Copy Path, Kill Process
- Selecting **"Launch & Attach"** opens a mini-dialog to start any EXE and stream its stdout or stderr into a new log tab
- Selecting **"Capture Debug Output"** attaches to OutputDebugString output (DebugView technique — works for all running processes) and streams it into a new tab
- Selecting **"Monitor Event Log"** streams matching Windows Event Log entries into a new tab
- The panel is toggled via **Ctrl+Shift+P** or View menu → "Process Explorer"
- Panel width and visibility persist in settings
- All captured process output uses the **existing** highlight, search, filter, and bookmark infrastructure
- The feature ships with 0 build errors, 0 warnings

---

## Pre-flight Checklist
- [ ] Phase 6 is fully complete and built without errors
- [ ] Re-read SKILL/technical skill sections on services, MVVM, and disposable patterns
- [ ] Verify `LogTabViewModel` is still `sealed` — the new constructor will be added, do NOT unseal it

---

## Implementation Order
> Always build in this order: Models → Services → ViewModels → Views → MainWindow wiring.
> Do NOT start a later step until the earlier one compiles clean.

---

## Step 1: New Models

### `Models/ProcessInfo.cs` — create new file

```csharp
using System;
using System.Collections.Generic;

namespace LogTailPro.Models;

/// <summary>Snapshot of a running operating system process.</summary>
public sealed class ProcessInfo
{
    public int Pid { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public long MemoryBytes { get; init; }
    public double CpuPercent { get; init; }
    public ProcessStatus Status { get; init; }
    public bool IsDotNet { get; init; }
    public int ParentPid { get; init; }
    public DateTime? StartTime { get; init; }
    public string UserName { get; init; } = string.Empty;

    /// <summary>Mutable — populated by BuildTree in ProcessSnapshotService.</summary>
    public List<ProcessInfo> Children { get; } = new();

    public string MemoryText => MemoryBytes switch
    {
        0                     => "—",
        < 1024 * 1024         => $"{MemoryBytes / 1024} KB",
        < 1024L * 1024 * 1024 => $"{MemoryBytes / (1024.0 * 1024):F0} MB",
        _                     => $"{MemoryBytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    public string CpuText => CpuPercent < 0.05 ? "—" : $"{CpuPercent:F1}%";

    public string DisplayName => $"{Name} [{Pid}]";

    public string StartTimeText => StartTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
}

/// <summary>Process execution state.</summary>
public enum ProcessStatus
{
    Running,
    NotResponding,
    Suspended
}
```

### `Models/ProcessAttachmentType.cs` — create new file

```csharp
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
    WindowsEventLog
}
```

---

## Step 2: New Services

### `Services/ProcessSnapshotService.cs` — create new file

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>
/// Enumerates running Windows processes, builds a parent-child tree, and
/// computes per-process CPU% and working-set metrics.
/// </summary>
public sealed class ProcessSnapshotService : IDisposable
{
    // ── P/Invoke: CreateToolhelp32Snapshot for parent PIDs ────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll")]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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
    private static readonly IntPtr INVALID_HANDLE = new(-1);

    private Dictionary<int, TimeSpan> _prevCpuTimes = new();
    private DateTime _prevSampleTime = DateTime.UtcNow;
    private bool _disposed;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns a flat snapshot of all running processes with live metrics.</summary>
    public IReadOnlyList<ProcessInfo> GetAll()
    {
        Dictionary<int, int> parentPids = GetParentPidMap();
        Process[] procs = Process.GetProcesses();
        DateTime now = DateTime.UtcNow;
        double elapsedSec = Math.Max(0.1, (now - _prevSampleTime).TotalSeconds);
        int cpuCount = Environment.ProcessorCount;
        List<ProcessInfo> result = new(procs.Length);
        Dictionary<int, TimeSpan> newCpu = new(procs.Length);

        foreach (Process p in procs)
        {
            try
            {
                double cpuPct = 0;
                try
                {
                    TimeSpan total = p.TotalProcessorTime;
                    if (_prevCpuTimes.TryGetValue(p.Id, out TimeSpan prev))
                        cpuPct = Math.Clamp(((total - prev).TotalSeconds / elapsedSec / cpuCount) * 100.0, 0, 100);
                    newCpu[p.Id] = total;
                }
                catch { /* access denied on TotalProcessorTime */ }

                result.Add(new ProcessInfo
                {
                    Pid            = p.Id,
                    Name           = p.ProcessName,
                    ExecutablePath = SafeMainModule(p),
                    MemoryBytes    = SafeWorkingSet(p),
                    CpuPercent     = cpuPct,
                    Status         = SafeStatus(p),
                    IsDotNet       = SafeIsDotNet(p),
                    ParentPid      = parentPids.TryGetValue(p.Id, out int ppid) ? ppid : 0,
                    StartTime      = SafeStartTime(p),
                    UserName       = SafeUserName(p)
                });
            }
            catch { /* process exited during enumeration */ }
            finally { p.Dispose(); }
        }

        _prevCpuTimes = newCpu;
        _prevSampleTime = now;
        return result;
    }

    /// <summary>Arranges a flat list into a parent-child tree. Orphans become roots.</summary>
    public IReadOnlyList<ProcessInfo> BuildTree(IReadOnlyList<ProcessInfo> flat)
    {
        Dictionary<int, ProcessInfo> byPid = flat.ToDictionary(p => p.Pid);
        List<ProcessInfo> roots = new();

        foreach (ProcessInfo p in flat)
        {
            if (p.ParentPid != 0 && byPid.TryGetValue(p.ParentPid, out ProcessInfo? parent))
                parent.Children.Add(p);
            else
                roots.Add(p);
        }

        return roots.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Dictionary<int, int> GetParentPidMap()
    {
        Dictionary<int, int> map = new();
        IntPtr hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (hSnap == INVALID_HANDLE) return map;
        try
        {
            PROCESSENTRY32 entry = new() { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32First(hSnap, ref entry))
                do { map[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID; }
                while (Process32Next(hSnap, ref entry));
        }
        finally { CloseHandle(hSnap); }
        return map;
    }

    private static string SafeMainModule(Process p)
    {
        try { return p.MainModule?.FileName ?? string.Empty; }
        catch { return string.Empty; }
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

    private static bool SafeIsDotNet(Process p)
    {
        try
        {
            foreach (ProcessModule m in p.Modules)
                if (m.ModuleName.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase) ||
                    m.ModuleName.Equals("clr.dll",     StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        catch { }
        return false;
    }

    private static DateTime? SafeStartTime(Process p)
    {
        try { return p.StartTime; }
        catch { return null; }
    }

    private static string SafeUserName(Process p)
    {
        try { using WindowsIdentity id = new(p.Handle); return id.Name; }
        catch { return string.Empty; }
    }

    public void Dispose() { _disposed = true; }
}
```

### `Services/ProcessOutputCaptureService.cs` — create new file

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>
/// Launches a child process with redirected stdout and/or stderr, and
/// streams lines via the same event pattern as <see cref="FileWatcherService"/>.
/// </summary>
public sealed class ProcessOutputCaptureService : IDisposable
{
    private Process? _process;
    private bool _disposed;

    /// <summary>Raised on the thread pool when new lines arrive. Same contract as FileWatcherService.</summary>
    public event EventHandler<IReadOnlyList<string>>? NewLinesAvailable;

    /// <summary>Raised when the child process exits. int = exit code.</summary>
    public event EventHandler<int>? ProcessExited;

    public bool IsRunning => _process is { HasExited: false };
    public int? ProcessId  => _process?.Id;

    /// <summary>
    /// Launches <paramref name="exePath"/> and begins streaming output.
    /// Call before wiring to a LogTabViewModel.
    /// </summary>
    public void Start(string exePath, string arguments, ProcessAttachmentType type,
                      string? workingDirectory = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        bool captureOut = type is ProcessAttachmentType.StandardOutput;
        bool captureErr = type is ProcessAttachmentType.StandardError;

        ProcessStartInfo si = new()
        {
            FileName               = exePath,
            Arguments              = arguments,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = captureOut,
            RedirectStandardError  = captureErr,
            WorkingDirectory       = workingDirectory ?? string.Empty
        };

        _process = new Process { StartInfo = si, EnableRaisingEvents = true };

        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) NewLinesAvailable?.Invoke(this, new[] { e.Data });
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) NewLinesAvailable?.Invoke(this, new[] { e.Data });
        };
        _process.Exited += (_, _) =>
        {
            int code = 0;
            try { code = _process.ExitCode; } catch { }
            ProcessExited?.Invoke(this, code);
        };

        _process.Start();
        if (captureOut) _process.BeginOutputReadLine();
        if (captureErr) _process.BeginErrorReadLine();
    }

    /// <summary>Stops reading output but does NOT kill the child process.</summary>
    public void Detach()
    {
        if (_process == null || _process.HasExited) return;
        try { _process.CancelOutputRead(); } catch { }
        try { _process.CancelErrorRead();  } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
        _process?.Dispose();
        _process = null;
    }
}
```

### `Services/DebugOutputMonitorService.cs` — create new file

```csharp
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace LogTailPro.Services;

/// <summary>
/// Captures OutputDebugString / OutputDebugStringW output from ALL running processes
/// using the same Win32 named shared-memory technique as Sysinternals DebugView.
/// Only one instance can run per machine at a time (conflicts with DebugView).
/// </summary>
public sealed class DebugOutputMonitorService : IDisposable
{
    // ── Win32 P/Invoke ────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpAttr,
        uint flProtect, uint dwMaxSizeHigh, uint dwMaxSizeLow, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(IntPtr hMap, uint dwAccess,
        uint dwOffHigh, uint dwOffLow, UIntPtr dwBytes);

    [DllImport("kernel32.dll")]
    private static extern bool UnmapViewOfFile(IntPtr lpBase);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateEvent(IntPtr lpAttr, bool bManualReset,
        bool bInitialState, string lpName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateMutex(IntPtr lpAttr, bool bOwner, string lpName);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr h, uint dwMs);

    [DllImport("kernel32.dll")]
    private static extern bool SetEvent(IntPtr h);

    private const int BUFFER_SIZE  = 4096;
    private const uint PAGE_RW     = 0x04;
    private const uint FILE_MAP_RD = 0x0004;
    private const uint WAIT_OK     = 0;

    private IntPtr _hMap   = IntPtr.Zero;
    private IntPtr _pView  = IntPtr.Zero;
    private IntPtr _hReady = IntPtr.Zero;
    private IntPtr _hData  = IntPtr.Zero;
    private IntPtr _hMutex = IntPtr.Zero;

    private Thread? _thread;
    private volatile bool _running;
    private bool _disposed;

    /// <summary>Raised when a debug string is received. (pid, message) — runs on background thread.</summary>
    public event EventHandler<(int Pid, string Message)>? LineReceived;

    public bool IsCapturing => _running;

    /// <summary>
    /// Starts the capture thread.
    /// Throws <see cref="InvalidOperationException"/> if another DebugView instance already holds the buffer.
    /// </summary>
    public void Start()
    {
        if (_running || _disposed) return;

        _hMap = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, PAGE_RW, 0, BUFFER_SIZE, "DBWIN_BUFFER");
        if (_hMap == IntPtr.Zero)
            throw new InvalidOperationException(
                "Cannot create DBWIN_BUFFER shared memory. Sysinternals DebugView may already be running.");

        _pView = MapViewOfFile(_hMap, FILE_MAP_RD, 0, 0, new UIntPtr(BUFFER_SIZE));
        if (_pView == IntPtr.Zero)
        {
            CloseHandle(_hMap); _hMap = IntPtr.Zero;
            throw new InvalidOperationException("Cannot map DBWIN_BUFFER view.");
        }

        _hReady = CreateEvent(IntPtr.Zero, false, false, "DBWIN_BUFFER_READY");
        _hData  = CreateEvent(IntPtr.Zero, false, false, "DBWIN_DATA_READY");
        _hMutex = CreateMutex(IntPtr.Zero, false, "DBWINMutex");

        _running = true;
        _thread = new Thread(CaptureLoop) { IsBackground = true, Name = "DebugOutputMonitor" };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        if (_hReady != IntPtr.Zero) SetEvent(_hReady);
    }

    private void CaptureLoop()
    {
        while (_running)
        {
            if (_hReady != IntPtr.Zero) SetEvent(_hReady);

            uint r = WaitForSingleObject(_hData, 500);
            if (!_running) break;
            if (r != WAIT_OK) continue;

            int pid = Marshal.ReadInt32(_pView);
            string msg = Marshal.PtrToStringAnsi(new IntPtr(_pView.ToInt64() + 4)) ?? string.Empty;
            if (!string.IsNullOrEmpty(msg))
                LineReceived?.Invoke(this, (pid, msg.TrimEnd('\r', '\n')));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _thread?.Join(1000);
        if (_pView  != IntPtr.Zero) { UnmapViewOfFile(_pView);   _pView  = IntPtr.Zero; }
        if (_hMap   != IntPtr.Zero) { CloseHandle(_hMap);         _hMap   = IntPtr.Zero; }
        if (_hReady != IntPtr.Zero) { CloseHandle(_hReady);       _hReady = IntPtr.Zero; }
        if (_hData  != IntPtr.Zero) { CloseHandle(_hData);        _hData  = IntPtr.Zero; }
        if (_hMutex != IntPtr.Zero) { CloseHandle(_hMutex);       _hMutex = IntPtr.Zero; }
    }
}
```

---

## Step 3: New ViewModels

### `ViewModels/ProcessItemViewModel.cs` — create new file

```csharp
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;

namespace LogTailPro.ViewModels;

/// <summary>Single node in the Process Explorer tree view.</summary>
public sealed partial class ProcessItemViewModel : ObservableObject
{
    public ProcessItemViewModel(ProcessInfo info)
    {
        Info = info;
        Children = new ObservableCollection<ProcessItemViewModel>(
            info.Children.Select(c => new ProcessItemViewModel(c)));
    }

    public ProcessInfo Info          { get; }
    public int         Pid           => Info.Pid;
    public string      Name          => Info.Name;
    public string      DisplayName   => Info.DisplayName;
    public string      CpuText       => Info.CpuText;
    public string      MemoryText    => Info.MemoryText;
    public string      ExecutablePath => Info.ExecutablePath;
    public string      UserName      => Info.UserName;
    public string      StartTimeText => Info.StartTimeText;
    public bool        IsDotNet      => Info.IsDotNet;

    public string StatusIcon => Info.Status switch
    {
        ProcessStatus.NotResponding => "⚠",
        ProcessStatus.Suspended     => "⏸",
        _                           => "●"
    };

    public string StatusDot => Info.Status switch
    {
        ProcessStatus.NotResponding => "#CC3333",
        ProcessStatus.Suspended     => "#CC7A00",
        _                           => "#4CAF50"
    };

    [ObservableProperty] private bool _isExpanded = false;
    [ObservableProperty] private bool _isSelected;

    public ObservableCollection<ProcessItemViewModel> Children { get; }

    /// <summary>Raised when the user requests a capture action from the context menu.</summary>
    public event EventHandler<(ProcessItemViewModel Item, ProcessAttachmentType Type)>? AttachRequested;

    /// <summary>Raised when the user wants to launch a new EXE and capture its output.</summary>
    public event EventHandler? LaunchAndAttachRequested;

    /// <summary>Raised when the user confirms killing this process.</summary>
    public event EventHandler<ProcessItemViewModel>? KillRequested;

    [RelayCommand]
    private void AttachDebugOutput() =>
        AttachRequested?.Invoke(this, (this, ProcessAttachmentType.DebugOutput));

    [RelayCommand]
    private void AttachEventLog() =>
        AttachRequested?.Invoke(this, (this, ProcessAttachmentType.WindowsEventLog));

    [RelayCommand]
    private void LaunchAndAttach() => LaunchAndAttachRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenFileLocation()
    {
        if (File.Exists(ExecutablePath))
            Process.Start("explorer.exe", $"/select,\"{ExecutablePath}\"");
    }

    [RelayCommand]
    private void CopyPid() => Clipboard.SetText(Pid.ToString());

    [RelayCommand]
    private void CopyPath() => Clipboard.SetText(ExecutablePath);

    [RelayCommand]
    private void KillProcess()
    {
        MessageBoxResult r = MessageBox.Show(
            $"Kill {DisplayName}?\n\nThis immediately terminates the process and all its children.",
            "Kill Process", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r == MessageBoxResult.Yes)
            KillRequested?.Invoke(this, this);
    }
}
```

### `ViewModels/ProcessExplorerViewModel.cs` — create new file

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.Models;
using LogTailPro.Services;

namespace LogTailPro.ViewModels;

/// <summary>Root ViewModel for the Process Explorer side panel.</summary>
public sealed partial class ProcessExplorerViewModel : ObservableObject, IDisposable
{
    private readonly ProcessSnapshotService _snapshot = new();
    private readonly DispatcherTimer _refreshTimer;
    private bool _disposed;

    public ProcessExplorerViewModel()
    {
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
    }

    [ObservableProperty] private ObservableCollection<ProcessItemViewModel> _processes = new();
    [ObservableProperty] private ProcessItemViewModel? _selectedProcess;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isAutoRefresh;
    [ObservableProperty] private bool _showSystemProcesses;
    [ObservableProperty] private bool _showDotNetOnly;
    [ObservableProperty] private string _statusText = "Click Refresh to load processes";

    partial void OnSearchTextChanged(string _) => _ = RefreshAsync();
    partial void OnShowSystemProcessesChanged(bool _) => _ = RefreshAsync();
    partial void OnShowDotNetOnlyChanged(bool _) => _ = RefreshAsync();

    partial void OnIsAutoRefreshChanged(bool value)
    {
        if (value) _refreshTimer.Start();
        else       _refreshTimer.Stop();
    }

    /// <summary>Raised when the user picks a process + capture type. Handled by MainViewModel.</summary>
    public event EventHandler<(ProcessInfo Info, ProcessAttachmentType Type)>? AttachRequested;

    /// <summary>Raised when the user clicks Launch &amp; Attach. Handled by MainViewModel.</summary>
    public event EventHandler? LaunchAndAttachRequested;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        StatusText = "Refreshing…";
        IReadOnlyList<ProcessInfo> flat = await Task.Run(() => _snapshot.GetAll());

        IEnumerable<ProcessInfo> filtered = flat;
        if (!ShowSystemProcesses)
            filtered = filtered.Where(p => p.Pid > 4 && !string.IsNullOrEmpty(p.UserName));
        if (ShowDotNetOnly)
            filtered = filtered.Where(p => p.IsDotNet);
        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(p =>
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.Pid.ToString().Contains(SearchText));

        IReadOnlyList<ProcessInfo> tree = _snapshot.BuildTree(filtered.ToList());

        Processes.Clear();
        foreach (ProcessInfo root in tree)
            Processes.Add(BuildItemVm(root));

        StatusText = $"{flat.Count} processes  •  {DateTime.Now:HH:mm:ss}";
    }

    private ProcessItemViewModel BuildItemVm(ProcessInfo info)
    {
        var vm = new ProcessItemViewModel(info);
        vm.AttachRequested        += (_, e) => AttachRequested?.Invoke(this, (e.Item.Info, e.Type));
        vm.LaunchAndAttachRequested += (_, _) => LaunchAndAttachRequested?.Invoke(this, EventArgs.Empty);
        vm.KillRequested          += (_, target) =>
        {
            try { Process.GetProcessById(target.Pid).Kill(); } catch { }
            _ = RefreshAsync();
        };
        return vm;
    }

    [RelayCommand]
    private async Task AttachSelectedAsync()
    {
        if (SelectedProcess == null) return;
        AttachRequested?.Invoke(this, (SelectedProcess.Info, ProcessAttachmentType.DebugOutput));
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void LaunchAndAttach() => LaunchAndAttachRequested?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer.Stop();
        _snapshot.Dispose();
    }
}
```

### Modify `ViewModels/LogTabViewModel.cs` — add process-capture constructor

**After the second constructor (the one for merged/preloaded tabs, ending around line 274), insert this third constructor:**

```csharp
/// <summary>
/// Creates a tab that streams lines from a <see cref="ProcessOutputCaptureService"/>
/// instead of a file. The capture service must already be running.
/// </summary>
public LogTabViewModel(ProcessInfo process, ProcessAttachmentType captureType,
                       ProcessOutputCaptureService captureService, AppSettings settings)
{
    _settings = settings;
    IsProcessTab       = true;
    AttachedProcessId  = process.Pid;
    ProcessCaptureType = captureType;
    _captureService    = captureService;

    string modeLabel = captureType switch
    {
        ProcessAttachmentType.StandardOutput => "stdout",
        ProcessAttachmentType.StandardError  => "stderr",
        _                                    => "output"
    };
    LogFile = new LogFile { FilePath = $"{process.Name} [{process.Pid}] — {modeLabel}" };
    _displayName = LogFile.FilePath;
    _isFollowTail = true;
    _compiledHighlightRules = _highlightService.CompileRules(settings.HighlightRules);
    FileStatus = FileStatus.Watching;
    IsLoaded = true;

    _captureService.NewLinesAvailable += OnNewLinesAvailable;
    _captureService.ProcessExited     += OnCapturedProcessExited;
    UpdateStatus();
}
```

**Also add these fields and properties alongside the existing private fields (near top of class, after `_isReadOnly`):**

```csharp
/// <summary>Non-null when this tab streams from a process instead of a file.</summary>
private ProcessOutputCaptureService? _captureService;

/// <summary>True when this tab was opened via Process Explorer (not a file).</summary>
public bool IsProcessTab { get; private init; }

/// <summary>PID of the attached process, if this is a process tab.</summary>
public int? AttachedProcessId { get; private init; }

/// <summary>Capture mode for process tabs.</summary>
public ProcessAttachmentType? ProcessCaptureType { get; private init; }
```

**Add this private handler alongside the other event handlers (after OnFileTruncated):**

```csharp
private void OnCapturedProcessExited(object? sender, int exitCode)
{
    Application.Current.Dispatcher.InvokeAsync(() =>
    {
        FileStatus = FileStatus.Deleted;
        StatusText = $"Process exited (code {exitCode})";
        WarningMessage = $"Process exited with code {exitCode}.";
    });
}
```

**In the `Dispose()` method, add cleanup for the capture service (before or alongside `_watcher.Dispose()`):**

```csharp
_captureService?.ProcessExited     -= OnCapturedProcessExited;
_captureService?.NewLinesAvailable -= OnNewLinesAvailable;
_captureService?.Dispose();
```

### Modify `ViewModels/MainViewModel.cs` — wire the Process Explorer

**Add this field near the top with other fields:**

```csharp
/// <summary>Process Explorer panel ViewModel.</summary>
public ProcessExplorerViewModel ProcessExplorer { get; } = new();
```

**Add these properties (after `IsHighlightingEnabled`):**

```csharp
/// <summary>Whether the Process Explorer left panel is visible.</summary>
[ObservableProperty]
private bool _isProcessExplorerVisible;

partial void OnIsProcessExplorerVisibleChanged(bool value)
{
    Settings.IsProcessExplorerVisible = value;
    _settingsService.Save(Settings);
}
```

**In the constructor, after `_directoryMonitor.NewFileDetected +=` wiring, add:**

```csharp
_isProcessExplorerVisible = Settings.IsProcessExplorerVisible;

ProcessExplorer.AttachRequested       += async (_, e) => await OpenProcessTabAsync(e.Info, e.Type);
ProcessExplorer.LaunchAndAttachRequested += (_, _) => OpenLaunchDialog();
```

**Add these commands (alongside the other commands):**

```csharp
[RelayCommand]
private void ToggleProcessExplorer() => IsProcessExplorerVisible = !IsProcessExplorerVisible;

[RelayCommand]
private void OpenProcessExplorer()
{
    IsProcessExplorerVisible = true;
    _ = ProcessExplorer.RefreshAsync();
}
```

**Add the process tab factory method (alongside `OpenFileTabAsync`):**

```csharp
/// <summary>Opens a new tab capturing output from <paramref name="process"/>.</summary>
public async Task OpenProcessTabAsync(ProcessInfo process, ProcessAttachmentType type)
{
    if (type is ProcessAttachmentType.DebugOutput)
    {
        OpenDebugOutputTab(process);
        return;
    }

    if (type is ProcessAttachmentType.WindowsEventLog)
    {
        OpenEventLogTab(process);
        return;
    }

    // For stdout/stderr: use LaunchProcessDialog instead
    OpenLaunchDialog();
    await Task.CompletedTask;
}

private void OpenDebugOutputTab(ProcessInfo process)
{
    var monitor = new DebugOutputMonitorService();
    try { monitor.Start(); }
    catch (InvalidOperationException ex)
    {
        MessageBox.Show(ex.Message, "Process Explorer", MessageBoxButton.OK, MessageBoxImage.Warning);
        monitor.Dispose();
        return;
    }

    // Create a thin adapter: DebugOutputMonitorService → IReadOnlyList<string> events
    // filtered to the selected PID (or all if pid==0)
    var captureService = new ProcessOutputCaptureService();   // used only as line-buffer
    int targetPid = process.Pid;
    monitor.LineReceived += (_, e) =>
    {
        if (targetPid == 0 || e.Pid == targetPid)
            captureService.NewLinesAvailable?.Invoke(captureService, new[] { $"[{e.Pid}] {e.Message}" });
    };

    // NOTE: DebugOutputMonitorService owns its own lifetime; attach it as a field
    // on the tab so it's disposed when the tab is closed.
    // Simplest approach: store monitor in a local and dispose via the captureService wrapper.
    // Use the process-mode constructor:
    var tabVm = new LogTabViewModel(process, ProcessAttachmentType.DebugOutput,
                                    captureService, Settings);

    // Expose the monitor for disposal — attach to tab Dispose via event
    void OnTabDisposed(object? s, EventArgs _) { monitor.Dispose(); }
    // (LogTabViewModel does not currently expose Disposed event — add it, OR
    //  track monitor in a list on MainViewModel and dispose when tab is removed)
    _activeMonitors[tabVm] = monitor;

    Tabs.Add(tabVm);
    SelectedTab = tabVm;
}

/// <summary>Tracks active DebugOutputMonitorService instances keyed by their owning tab.</summary>
private readonly Dictionary<LogTabViewModel, DebugOutputMonitorService> _activeMonitors = new();

private void OpenEventLogTab(ProcessInfo process)
{
    // Use System.Diagnostics.EventLog with EntryWritten event
    // Create a synthetic captureService that routes EventLog entries
    var captureService = new ProcessOutputCaptureService();
    var log = new System.Diagnostics.EventLog("Application");
    log.EnableRaisingEvents = true;
    log.EntryWritten += (_, e) =>
    {
        if (e.Entry.Source.Contains(process.Name, StringComparison.OrdinalIgnoreCase))
        {
            string line = $"[{e.Entry.EntryType}] [{e.Entry.TimeWritten:HH:mm:ss}] {e.Entry.Message}";
            captureService.NewLinesAvailable?.Invoke(captureService, new[] { line });
        }
    };

    var tabVm = new LogTabViewModel(process, ProcessAttachmentType.WindowsEventLog,
                                    captureService, Settings);
    _activeEventLogs[tabVm] = log;
    Tabs.Add(tabVm);
    SelectedTab = tabVm;
}

private readonly Dictionary<LogTabViewModel, System.Diagnostics.EventLog> _activeEventLogs = new();

private void OpenLaunchDialog()
{
    LaunchProcessDialog dlg = new() { Owner = Application.Current.MainWindow };
    if (dlg.ShowDialog() != true) return;

    string exePath   = dlg.ExecutablePath;
    string arguments = dlg.Arguments;
    string? workDir  = dlg.WorkingDirectory;
    ProcessAttachmentType type = dlg.CaptureType;

    ProcessInfo synthetic = new()
    {
        Name = System.IO.Path.GetFileNameWithoutExtension(exePath),
        Pid  = 0,
        ExecutablePath = exePath
    };

    var captureService = new ProcessOutputCaptureService();
    captureService.Start(exePath, arguments, type, workDir);

    var tabVm = new LogTabViewModel(synthetic, type, captureService, Settings);
    tabVm.PropertyChanged += (_, e) =>
    {
        // When PID becomes known after start, update tab identity
    };

    Tabs.Add(tabVm);
    SelectedTab = tabVm;
}
```

**Override `CloseTab` to dispose monitors/event-logs when a process tab is removed:**

```csharp
// In CloseTab command — after tab.Dispose(), add:
if (_activeMonitors.TryGetValue(tab, out var monitor))
{
    monitor.Dispose();
    _activeMonitors.Remove(tab);
}
if (_activeEventLogs.TryGetValue(tab, out var eventLog))
{
    eventLog.Dispose();
    _activeEventLogs.Remove(tab);
}
```

---

## Step 4: New Views

### `Views/ProcessExplorerPanel.xaml` — create new file

```xml
<UserControl x:Class="LogTailPro.Views.ProcessExplorerPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:LogTailPro.ViewModels"
             d:DataContext="{d:DesignInstance vm:ProcessExplorerViewModel}"
             Background="{StaticResource BgSurface}"
             MinWidth="220">

    <UserControl.Resources>
        <!-- Converter: hex string → SolidColorBrush for status dot -->
        <conv:HexToColorConverter xmlns:conv="clr-namespace:LogTailPro.Converters"
                                   x:Key="HexToColor"/>
    </UserControl.Resources>

    <DockPanel>

        <!-- ── Header ──────────────────────────────────────────────────── -->
        <Border DockPanel.Dock="Top"
                Background="{StaticResource BgElevated}"
                BorderBrush="{StaticResource BorderNormal}"
                BorderThickness="0,0,0,1"
                Padding="8,6">
            <Grid>
                <TextBlock Text="PROCESS EXPLORER"
                           Foreground="{StaticResource TextSecondary}"
                           FontFamily="{StaticResource UiFont}"
                           FontSize="{StaticResource FontSizeSmall}"
                           FontWeight="SemiBold"
                           VerticalAlignment="Center"/>
            </Grid>
        </Border>

        <!-- ── Search Box ───────────────────────────────────────────────── -->
        <Border DockPanel.Dock="Top"
                Background="{StaticResource BgElevated}"
                BorderBrush="{StaticResource BorderNormal}"
                BorderThickness="0,0,0,1"
                Padding="6,5">
            <TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"
                     Background="{StaticResource BgInput}"
                     Foreground="{StaticResource TextPrimary}"
                     CaretBrush="{StaticResource TextPrimary}"
                     BorderBrush="{StaticResource BorderNormal}"
                     BorderThickness="1"
                     Padding="6,3"
                     FontFamily="{StaticResource UiFont}"
                     FontSize="{StaticResource FontSizeBody}">
                <TextBox.Style>
                    <Style TargetType="TextBox">
                        <Style.Resources>
                            <VisualBrush x:Key="PlaceHolder" TileMode="None" Stretch="None" AlignmentX="Left">
                                <VisualBrush.Visual>
                                    <TextBlock Text="🔍  Filter by name or PID…"
                                               Foreground="{StaticResource TextMuted}"
                                               FontFamily="{StaticResource UiFont}"
                                               FontSize="{StaticResource FontSizeBody}"
                                               Margin="2,0,0,0"/>
                                </VisualBrush.Visual>
                            </VisualBrush>
                        </Style.Resources>
                        <Style.Triggers>
                            <Trigger Property="Text" Value="">
                                <Setter Property="Background">
                                    <Setter.Value>
                                        <MultiBinding>
                                            <MultiBinding.Converter>
                                                <conv:NotEmptyConverter xmlns:conv="clr-namespace:LogTailPro.Converters"/>
                                            </MultiBinding.Converter>
                                        </MultiBinding>
                                    </Setter.Value>
                                </Setter>
                            </Trigger>
                        </Style.Triggers>
                    </Style>
                </TextBox.Style>
            </TextBox>
        </Border>

        <!-- ── Toolbar ──────────────────────────────────────────────────── -->
        <Border DockPanel.Dock="Top"
                Background="{StaticResource BgElevated}"
                BorderBrush="{StaticResource BorderNormal}"
                BorderThickness="0,0,0,1"
                Padding="4,4">
            <WrapPanel Orientation="Horizontal">
                <Button Content="↻ Refresh"
                        Command="{Binding RefreshCommand}"
                        Style="{StaticResource SecondaryButton}"
                        Padding="8,3" Margin="2,0"
                        ToolTip="Refresh process list (F5)"/>

                <CheckBox Content=".NET"
                          IsChecked="{Binding ShowDotNetOnly}"
                          VerticalAlignment="Center"
                          Margin="6,0,0,0"
                          ToolTip="Show only .NET processes"/>

                <CheckBox Content="Sys"
                          IsChecked="{Binding ShowSystemProcesses}"
                          VerticalAlignment="Center"
                          Margin="6,0,0,0"
                          ToolTip="Include system processes"/>

                <CheckBox Content="Auto"
                          IsChecked="{Binding IsAutoRefresh}"
                          VerticalAlignment="Center"
                          Margin="6,0,0,0"
                          ToolTip="Auto-refresh every 2 seconds"/>
            </WrapPanel>
        </Border>

        <!-- ── Details Strip — bottom ───────────────────────────────────── -->
        <Border DockPanel.Dock="Bottom"
                Background="{StaticResource BgElevated}"
                BorderBrush="{StaticResource BorderNormal}"
                BorderThickness="0,1,0,0"
                Padding="8,6"
                Visibility="{Binding SelectedProcess, Converter={StaticResource BoolToVisibility}}">
            <StackPanel>
                <TextBlock Foreground="{StaticResource TextPrimary}"
                           FontFamily="{StaticResource MonoFont}"
                           FontSize="{StaticResource FontSizeSmall}"
                           Text="{Binding SelectedProcess.DisplayName, FallbackValue=''}"/>
                <TextBlock Foreground="{StaticResource TextSecondary}"
                           FontFamily="{StaticResource UiFont}"
                           FontSize="{StaticResource FontSizeSmall}"
                           Text="{Binding SelectedProcess.ExecutablePath, FallbackValue=''}"
                           TextTrimming="CharacterEllipsis"
                           ToolTip="{Binding SelectedProcess.ExecutablePath}"
                           Margin="0,2,0,0"/>
                <Grid Margin="0,4,0,0">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    <TextBlock Grid.Row="0" Grid.Column="0" Text="CPU" Foreground="{StaticResource TextMuted}"   FontSize="{StaticResource FontSizeSmall}" Margin="0,0,4,2"/>
                    <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding SelectedProcess.CpuText}"    Foreground="{StaticResource TextPrimary}"  FontSize="{StaticResource FontSizeSmall}" FontFamily="{StaticResource MonoFont}"/>
                    <TextBlock Grid.Row="0" Grid.Column="2" Text="MEM" Foreground="{StaticResource TextMuted}"   FontSize="{StaticResource FontSizeSmall}" Margin="8,0,4,2"/>
                    <TextBlock Grid.Row="0" Grid.Column="3" Text="{Binding SelectedProcess.MemoryText}" Foreground="{StaticResource TextPrimary}"  FontSize="{StaticResource FontSizeSmall}" FontFamily="{StaticResource MonoFont}"/>
                    <TextBlock Grid.Row="1" Grid.Column="0" Text="User" Foreground="{StaticResource TextMuted}"  FontSize="{StaticResource FontSizeSmall}" Margin="0,0,4,0"/>
                    <TextBlock Grid.Row="1" Grid.Column="1" Grid.ColumnSpan="3" Text="{Binding SelectedProcess.UserName}" Foreground="{StaticResource TextPrimary}" FontSize="{StaticResource FontSizeSmall}" TextTrimming="CharacterEllipsis"/>
                </Grid>
                <TextBlock Text="{Binding SelectedProcess.StartTimeText, StringFormat='Started  {0}', FallbackValue=''}"
                           Foreground="{StaticResource TextMuted}"
                           FontSize="{StaticResource FontSizeSmall}"
                           Margin="0,2,0,0"/>
            </StackPanel>
        </Border>

        <!-- ── Status bar ────────────────────────────────────────────────── -->
        <Border DockPanel.Dock="Bottom"
                Background="{StaticResource BgBase}"
                BorderBrush="{StaticResource BorderSubtle}"
                BorderThickness="0,1,0,0"
                Padding="8,3">
            <TextBlock Text="{Binding StatusText}"
                       Foreground="{StaticResource TextMuted}"
                       FontSize="{StaticResource FontSizeSmall}"/>
        </Border>

        <!-- ── Process Tree ──────────────────────────────────────────────── -->
        <TreeView ItemsSource="{Binding Processes}"
                  SelectedItemChanged="TreeView_SelectedItemChanged"
                  Background="{StaticResource BgBase}"
                  BorderThickness="0"
                  VirtualizingPanel.IsVirtualizing="True"
                  VirtualizingPanel.VirtualizationMode="Recycling"
                  KeyDown="TreeView_KeyDown">

            <TreeView.Resources>
                <HierarchicalDataTemplate DataType="{x:Type vm:ProcessItemViewModel}"
                                           ItemsSource="{Binding Children}">
                    <Grid Margin="2,1">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="10"/>
                            <ColumnDefinition Width="6"/>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>

                        <!-- Status dot -->
                        <Ellipse Grid.Column="0"
                                 Width="6" Height="6"
                                 VerticalAlignment="Center"
                                 Fill="{Binding StatusDot, Converter={StaticResource HexToColor}}"/>

                        <!-- .NET badge -->
                        <TextBlock Grid.Column="1"
                                   Text="ᴺ"
                                   Foreground="{StaticResource Accent}"
                                   FontSize="9"
                                   VerticalAlignment="Center"
                                   Margin="2,0,0,0"
                                   Visibility="{Binding IsDotNet, Converter={StaticResource BoolToVisibility}}"
                                   ToolTip=".NET process"/>

                        <!-- Process name + PID -->
                        <TextBlock Grid.Column="2"
                                   Text="{Binding DisplayName}"
                                   Foreground="{StaticResource TextPrimary}"
                                   FontFamily="{StaticResource MonoFont}"
                                   FontSize="{StaticResource FontSizeSmall}"
                                   VerticalAlignment="Center"
                                   Margin="4,0,8,0"
                                   TextTrimming="CharacterEllipsis"/>

                        <!-- CPU + Memory metrics -->
                        <StackPanel Grid.Column="3"
                                    Orientation="Horizontal"
                                    VerticalAlignment="Center">
                            <TextBlock Text="{Binding CpuText}"
                                       Foreground="{StaticResource TextMuted}"
                                       FontFamily="{StaticResource MonoFont}"
                                       FontSize="10"
                                       MinWidth="36"
                                       TextAlignment="Right"
                                       Margin="0,0,6,0"/>
                            <TextBlock Text="{Binding MemoryText}"
                                       Foreground="{StaticResource TextMuted}"
                                       FontFamily="{StaticResource MonoFont}"
                                       FontSize="10"
                                       MinWidth="52"
                                       TextAlignment="Right"/>
                        </StackPanel>
                    </Grid>
                </HierarchicalDataTemplate>
            </TreeView.Resources>

            <TreeView.ItemContainerStyle>
                <Style TargetType="TreeViewItem">
                    <Setter Property="IsExpanded" Value="{Binding IsExpanded, Mode=TwoWay}"/>
                    <Setter Property="IsSelected" Value="{Binding IsSelected, Mode=TwoWay}"/>
                    <Setter Property="Padding"    Value="2,1"/>
                    <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
                    <Setter Property="Background" Value="Transparent"/>
                    <Style.Triggers>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter Property="Background" Value="{StaticResource BgOverlay}"/>
                            <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
                        </Trigger>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter Property="Background" Value="{StaticResource BgElevated}"/>
                        </Trigger>
                    </Style.Triggers>
                    <Setter Property="ContextMenu">
                        <Setter.Value>
                            <ContextMenu Background="{StaticResource BgSurface}"
                                         Foreground="{StaticResource TextPrimary}"
                                         BorderBrush="{StaticResource BorderNormal}">
                                <MenuItem Header="Capture Debug Output (OutputDebugString)"
                                          Command="{Binding AttachDebugOutputCommand}"/>
                                <MenuItem Header="Monitor Windows Event Log"
                                          Command="{Binding AttachEventLogCommand}"/>
                                <Separator/>
                                <MenuItem Header="Launch &amp; Attach stdout/stderr…"
                                          Command="{Binding LaunchAndAttachCommand}"/>
                                <Separator/>
                                <MenuItem Header="Open in Explorer"
                                          Command="{Binding OpenFileLocationCommand}"/>
                                <MenuItem Header="Copy PID"
                                          Command="{Binding CopyPidCommand}"/>
                                <MenuItem Header="Copy Path"
                                          Command="{Binding CopyPathCommand}"/>
                                <Separator/>
                                <MenuItem Header="Kill Process…"
                                          Command="{Binding KillProcessCommand}"
                                          Foreground="{StaticResource StatusError}"/>
                            </ContextMenu>
                        </Setter.Value>
                    </Setter>
                </Style>
            </TreeView.ItemContainerStyle>

        </TreeView>
    </DockPanel>
</UserControl>
```

### `Views/ProcessExplorerPanel.xaml.cs` — create new file

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LogTailPro.ViewModels;

namespace LogTailPro.Views;

public partial class ProcessExplorerPanel : UserControl
{
    public ProcessExplorerPanel() => InitializeComponent();

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is ProcessExplorerViewModel vm && e.NewValue is ProcessItemViewModel item)
            vm.SelectedProcess = item;
    }

    private void TreeView_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ProcessExplorerViewModel vm) return;
        if (e.Key == Key.F5) { _ = vm.RefreshAsync(); e.Handled = true; }
        if (e.Key == Key.Delete && vm.SelectedProcess != null)
        {
            vm.SelectedProcess.KillProcessCommand.Execute(null);
            e.Handled = true;
        }
    }
}
```

### `Views/LaunchProcessDialog.xaml` — create new file

```xml
<Window x:Class="LogTailPro.Views.LaunchProcessDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Launch &amp; Capture Process"
        Background="{StaticResource BgBase}"
        Foreground="{StaticResource TextPrimary}"
        FontFamily="{StaticResource UiFont}"
        FontSize="{StaticResource UiSize}"
        Width="520" Height="260"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize">
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="8"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="8"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="8"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Executable -->
        <Grid Grid.Row="0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="90"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="80"/>
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="Executable:" VerticalAlignment="Center" Foreground="{StaticResource TextSecondary}"/>
            <TextBox   Grid.Column="1" x:Name="ExeBox"
                       Background="{StaticResource BgInput}"
                       Foreground="{StaticResource TextPrimary}"
                       CaretBrush="{StaticResource TextPrimary}"
                       BorderBrush="{StaticResource BorderNormal}"
                       Padding="6,4" FontFamily="{StaticResource MonoFont}"/>
            <Button    Grid.Column="2" Content="Browse…" Margin="8,0,0,0"
                       Style="{StaticResource SecondaryButton}" Click="Browse_Click"/>
        </Grid>

        <!-- Arguments -->
        <Grid Grid.Row="2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="90"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="Arguments:" VerticalAlignment="Center" Foreground="{StaticResource TextSecondary}"/>
            <TextBox   Grid.Column="1" x:Name="ArgsBox"
                       Background="{StaticResource BgInput}"
                       Foreground="{StaticResource TextPrimary}"
                       CaretBrush="{StaticResource TextPrimary}"
                       BorderBrush="{StaticResource BorderNormal}"
                       Padding="6,4" FontFamily="{StaticResource MonoFont}"/>
        </Grid>

        <!-- Working dir -->
        <Grid Grid.Row="4">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="90"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="80"/>
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="Working dir:" VerticalAlignment="Center" Foreground="{StaticResource TextSecondary}"/>
            <TextBox   Grid.Column="1" x:Name="WorkDirBox"
                       Background="{StaticResource BgInput}"
                       Foreground="{StaticResource TextPrimary}"
                       CaretBrush="{StaticResource TextPrimary}"
                       BorderBrush="{StaticResource BorderNormal}"
                       Padding="6,4" FontFamily="{StaticResource MonoFont}"/>
            <Button    Grid.Column="2" Content="Browse…" Margin="8,0,0,0"
                       Style="{StaticResource SecondaryButton}" Click="BrowseDir_Click"/>
        </Grid>

        <!-- Capture mode -->
        <StackPanel Grid.Row="6" Orientation="Horizontal">
            <TextBlock Text="Capture:" VerticalAlignment="Center" Foreground="{StaticResource TextSecondary}" Width="90"/>
            <RadioButton x:Name="RbStdout"  Content="stdout"  IsChecked="True" Margin="0,0,16,0" VerticalAlignment="Center"/>
            <RadioButton x:Name="RbStderr"  Content="stderr"                   Margin="0,0,16,0" VerticalAlignment="Center"/>
            <RadioButton x:Name="RbBoth"    Content="Both"                     VerticalAlignment="Center"/>
        </StackPanel>

        <!-- Action buttons -->
        <StackPanel Grid.Row="8" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="Cancel" Style="{StaticResource SecondaryButton}"
                    Width="80" Click="Cancel_Click" Margin="0,0,8,0"/>
            <Button Content="▶ Launch" Style="{StaticResource PrimaryButton}"
                    Width="90" Click="Launch_Click" IsDefault="True"/>
        </StackPanel>
    </Grid>
</Window>
```

### `Views/LaunchProcessDialog.xaml.cs` — create new file

```csharp
using System.IO;
using System.Windows;
using LogTailPro.Models;
using Microsoft.Win32;

namespace LogTailPro.Views;

public partial class LaunchProcessDialog : Window
{
    public string ExecutablePath    { get; private set; } = string.Empty;
    public string Arguments         { get; private set; } = string.Empty;
    public string? WorkingDirectory { get; private set; }
    public ProcessAttachmentType CaptureType { get; private set; }

    public LaunchProcessDialog() => InitializeComponent();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dlg = new()
        {
            Filter = "Executables|*.exe;*.bat;*.cmd|All Files|*.*",
            Title  = "Select Executable"
        };
        if (dlg.ShowDialog() == true)
        {
            ExeBox.Text     = dlg.FileName;
            WorkDirBox.Text = Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
        }
    }

    private void BrowseDir_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dlg = new() { Title = "Select Working Directory" };
        if (dlg.ShowDialog() == true) WorkDirBox.Text = dlg.FolderName;
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ExeBox.Text))
        {
            MessageBox.Show("Please specify an executable.", "Launch Process",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ExecutablePath   = ExeBox.Text.Trim();
        Arguments        = ArgsBox.Text.Trim();
        WorkingDirectory = string.IsNullOrWhiteSpace(WorkDirBox.Text) ? null : WorkDirBox.Text.Trim();

        CaptureType = RbStderr.IsChecked == true
            ? ProcessAttachmentType.StandardError
            : ProcessAttachmentType.StandardOutput;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
```

---

## Step 5: Modify `MainWindow.xaml`

### 5a. Add keyboard shortcut in `<Window.InputBindings>` (after the last `<KeyBinding>`):

```xml
<KeyBinding Key="P" Modifiers="Ctrl+Shift" Command="{Binding ToggleProcessExplorerCommand}"/>
```

### 5b. Add View menu item (after the Stats Panel item):

```xml
<Separator/>
<MenuItem Header="Process Explorer"
          Command="{Binding ToggleProcessExplorerCommand}"
          InputGestureText="Ctrl+Shift+P"
          IsCheckable="True"
          IsChecked="{Binding IsProcessExplorerVisible, Mode=OneWay}"/>
```

### 5c. Add toolbar button (after the existing buttons, before the file path `TextBlock`):

```xml
<Rectangle Grid.Column="5"
           Width="1" Height="20"
           Fill="{StaticResource BorderNormal}"
           VerticalAlignment="Center"
           Margin="8,0"/>

<Button Grid.Column="6"
        Content="⚙ Processes"
        Command="{Binding ToggleProcessExplorerCommand}"
        Style="{StaticResource SecondaryButton}"
        ToolTip="Toggle Process Explorer (Ctrl+Shift+P)"/>
```

> **IMPORTANT:** Update the Grid column numbers in the toolbar so the new columns fit between the existing ComboBox (column 5) and the file path TextBlock. Adjust existing `Grid.Column` attribute on the file path `TextBlock` accordingly.

### 5d. Replace the **entire** section 6 (`<!-- ═══ 6. Log Content -->`) with:

```xml
<!-- ═══ 6. Process Explorer + Log Content ══════════════════════════════════ -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition x:Name="ProcessPanelColumn"
                          Width="{Binding Settings.ProcessExplorerPanelWidth, Mode=TwoWay}"
                          MinWidth="0" MaxWidth="600"/>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- Left: Process Explorer panel -->
    <views:ProcessExplorerPanel
        Grid.Column="0"
        DataContext="{Binding ProcessExplorer}"
        Visibility="{Binding IsProcessExplorerVisible,
                     Converter={StaticResource BoolToVisibility}}"/>

    <!-- Drag handle -->
    <GridSplitter Grid.Column="1"
                  Width="4"
                  Background="{StaticResource BorderNormal}"
                  HorizontalAlignment="Center"
                  VerticalAlignment="Stretch"
                  Visibility="{Binding IsProcessExplorerVisible,
                               Converter={StaticResource BoolToVisibility}}"/>

    <!-- Right: existing log content -->
    <ContentControl Grid.Column="2" Content="{Binding SelectedTab}">
        <ContentControl.ContentTemplate>
            <DataTemplate DataType="{x:Type vm:LogTabViewModel}">
                <views:LogTabView/>
            </DataTemplate>
        </ContentControl.ContentTemplate>
    </ContentControl>
</Grid>
```

---

## Step 6: Modify `Models/AppSettings.cs`

Add these properties at the end of the class:

```csharp
// ── Phase 7: Process Explorer ─────────────────────────────────────────────

/// <summary>Whether the Process Explorer panel is open.</summary>
public bool IsProcessExplorerVisible { get; set; } = false;

/// <summary>Width of the Process Explorer panel in device-independent pixels.</summary>
public double ProcessExplorerPanelWidth { get; set; } = 280.0;

/// <summary>Whether the Process Explorer auto-refreshes every 2 seconds.</summary>
public bool ProcessExplorerAutoRefresh { get; set; } = false;

/// <summary>Whether system processes (PID &lt;= 4, or no user name) are shown.</summary>
public bool ProcessExplorerShowSystem { get; set; } = false;

/// <summary>Whether only .NET processes are shown in the Process Explorer.</summary>
public bool ProcessExplorerDotNetOnly { get; set; } = false;
```

---

## Step 7: Build Verification

```powershell
# Clean, restore, then build
dotnet restore src/LogTailPro/LogTailPro.csproj
dotnet build   src/LogTailPro/LogTailPro.csproj -c Debug

# Must see: Build succeeded. 0 Error(s) 0 Warning(s)
# Fix ALL warnings before proceeding. Target: 0 errors, 0 warnings.
```

**Common issues to watch for:**
- `PROCESSENTRY32` struct `szExeFile` field must be `CharSet.Auto` on the struct, not the method
- `ProcessOutputCaptureService.NewLinesAvailable` is `event` — it cannot be `.Invoke()` directly from outside; adapt the debug monitor routing accordingly (use a helper private method or lambda)
- `GridSplitter` needs both columns defined correctly — check min/max widths
- If `HexToColorConverter` does not accept a plain hex string `"#4CAF50"` as input, use a `SolidColorBrush` property on `ProcessItemViewModel` instead

---

## Step 8: Manual Test Checklist

- [ ] App launches — no crash, toolbar shows "⚙ Processes" button
- [ ] Click "⚙ Processes" — Process Explorer panel slides in with GridSplitter
- [ ] Ctrl+Shift+P toggles panel open/close
- [ ] Click "↻ Refresh" — tree populates with running processes
- [ ] Panel width persists after restart (saved to settings.json)
- [ ] Panel visibility persists after restart
- [ ] Search box filters by name and PID in real time
- [ ] ".NET" checkbox shows only .NET processes
- [ ] "Auto" checkbox enables 2-second live refresh; CPU/MEM values update
- [ ] Right-click any process → context menu appears with all 7 items
- [ ] "Capture Debug Output" — opens a new log tab, streams OutputDebugString from that process
- [ ] "Monitor Event Log" — opens a new log tab; write to Application event log in another tool to confirm
- [ ] "Launch & Attach…" dialog opens; browse to any EXE (e.g., `ping.exe`); select stdout; click Launch — new tab opens and streams stdout
- [ ] Process tab tab-header shows process name + PID
- [ ] When launched process exits — tab status changes to "Process exited (code N)"
- [ ] Search, highlights, and bookmarks all work normally in a process tab
- [ ] Close a process tab — no resource leaks (no crash after several open/close cycles)
- [ ] "Kill Process" shows confirmation dialog; confirmed kill removes the process
- [ ] "Open in Explorer" opens the folder for a process with a known EXE path
- [ ] Panel closes cleanly — existing file tabs still work normally
- [ ] 0 errors, 0 warnings in Release build

---

## Git Commit

```bash
git add -A
git commit -m "Phase 7 complete: Process Explorer — dotPeek-style panel, OutputDebugString capture, Launch & Attach, Event Log monitor"
```
