using System.Runtime.InteropServices;

namespace LogTailPro.Services;

/// <summary>
/// Captures OutputDebugString / OutputDebugStringW output from ALL running processes
/// using the same Win32 named shared-memory technique as Sysinternals DebugView.
/// Only one listener can run per machine at a time (conflicts with DebugView or another tab).
/// </summary>
public sealed class DebugOutputMonitorService : IDisposable
{
    // ── Win32 P/Invoke ────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", EntryPoint = "CreateFileMappingW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpAttr,
        uint flProtect, uint dwMaxSizeHigh, uint dwMaxSizeLow, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(IntPtr hMap, uint dwAccess,
        uint dwOffHigh, uint dwOffLow, UIntPtr dwBytes);

    [DllImport("kernel32.dll")]
    private static extern bool UnmapViewOfFile(IntPtr lpBase);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);

    [DllImport("kernel32.dll", EntryPoint = "CreateEventW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateEvent(IntPtr lpAttr, bool bManualReset,
        bool bInitialState, string lpName);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr h, uint dwMs);

    [DllImport("kernel32.dll")]
    private static extern bool SetEvent(IntPtr h);

    private const int BufferSize   = 4096;
    private const uint PageReadWrite = 0x04;
    private const uint FileMapRead = 0x0004;
    private const uint WaitObject0 = 0;
    private const int ErrorAlreadyExists = 183;

    private IntPtr _hMap   = IntPtr.Zero;
    private IntPtr _pView  = IntPtr.Zero;
    private IntPtr _hReady = IntPtr.Zero;
    private IntPtr _hData  = IntPtr.Zero;

    private Thread? _thread;
    private volatile bool _running;
    private bool _disposed;

    /// <summary>Raised when a debug string is received. (pid, message) — runs on a background thread.</summary>
    public event EventHandler<(int Pid, string Message)>? LineReceived;

    /// <summary>True while the capture thread is running.</summary>
    public bool IsCapturing => _running;

    /// <summary>
    /// Starts the capture thread.
    /// Throws <see cref="InvalidOperationException"/> if another listener already holds the buffer.
    /// </summary>
    public void Start()
    {
        if (_running || _disposed) return;

        _hMap = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, PageReadWrite, 0, BufferSize, "DBWIN_BUFFER");
        int mapError = Marshal.GetLastWin32Error();
        if (_hMap == IntPtr.Zero || mapError == ErrorAlreadyExists)
        {
            // ERROR_ALREADY_EXISTS means someone else (DebugView, VS, another tab) owns the buffer.
            ReleaseHandles();
            throw new InvalidOperationException(
                "Another debug-output listener (e.g. Sysinternals DebugView or Visual Studio) " +
                "already owns the DBWIN_BUFFER. Close it and try again.");
        }

        _pView = MapViewOfFile(_hMap, FileMapRead, 0, 0, new UIntPtr(BufferSize));
        if (_pView == IntPtr.Zero)
        {
            ReleaseHandles();
            throw new InvalidOperationException("Cannot map the DBWIN_BUFFER view.");
        }

        _hReady = CreateEvent(IntPtr.Zero, false, false, "DBWIN_BUFFER_READY");
        _hData  = CreateEvent(IntPtr.Zero, false, false, "DBWIN_DATA_READY");
        if (_hReady == IntPtr.Zero || _hData == IntPtr.Zero)
        {
            ReleaseHandles();
            throw new InvalidOperationException("Cannot create the DBWIN synchronisation events.");
        }

        _running = true;
        _thread = new Thread(CaptureLoop) { IsBackground = true, Name = "DebugOutputMonitor" };
        _thread.Start();
    }

    /// <summary>Signals the capture thread to stop.</summary>
    public void Stop() => _running = false;

    private void CaptureLoop()
    {
        while (_running)
        {
            // Tell writers the buffer is free, then wait for data (timeout lets us observe Stop()).
            SetEvent(_hReady);
            if (WaitForSingleObject(_hData, 500) != WaitObject0) continue;
            if (!_running) break;

            int pid = Marshal.ReadInt32(_pView);
            string msg = Marshal.PtrToStringAnsi(new IntPtr(_pView.ToInt64() + sizeof(int))) ?? string.Empty;
            msg = msg.TrimEnd('\r', '\n', '\0');
            if (msg.Length == 0) continue;

            try { LineReceived?.Invoke(this, (pid, msg)); }
            catch { /* never let a subscriber kill the capture thread */ }
        }
    }

    private void ReleaseHandles()
    {
        if (_pView  != IntPtr.Zero) { UnmapViewOfFile(_pView); _pView  = IntPtr.Zero; }
        if (_hMap   != IntPtr.Zero) { CloseHandle(_hMap);      _hMap   = IntPtr.Zero; }
        if (_hReady != IntPtr.Zero) { CloseHandle(_hReady);    _hReady = IntPtr.Zero; }
        if (_hData  != IntPtr.Zero) { CloseHandle(_hData);     _hData  = IntPtr.Zero; }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        // The loop wakes at least every 500 ms; wait for it before freeing the memory it reads.
        _thread?.Join(2000);
        ReleaseHandles();
    }
}
