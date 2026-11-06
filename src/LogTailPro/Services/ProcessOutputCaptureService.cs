using System.Diagnostics;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>
/// Streams lines into a process tab using the same event contract as <see cref="FileWatcherService"/>.
/// Lines come either from a launched child process (stdout/stderr, see <see cref="Start"/>) or from
/// any other producer (debug output, event log) that calls <see cref="Emit(string)"/>.
/// </summary>
public sealed class ProcessOutputCaptureService : IDisposable
{
    private Process? _process;
    private bool _disposed;

    /// <summary>Raised on a background thread when new lines arrive. Same contract as FileWatcherService.</summary>
    public event EventHandler<IReadOnlyList<string>>? NewLinesAvailable;

    /// <summary>Raised when the child process exits. int = exit code.</summary>
    public event EventHandler<int>? ProcessExited;

    /// <summary>True while the launched child process is alive.</summary>
    public bool IsRunning
    {
        get
        {
            try { return _process is { HasExited: false }; }
            catch (InvalidOperationException) { return false; }
        }
    }

    /// <summary>PID of the launched child process (null before <see cref="Start"/>).</summary>
    public int? ProcessId { get; private set; }

    /// <summary>Pushes a single line to subscribers (used by non-process producers).</summary>
    public void Emit(string line)
    {
        if (!_disposed) NewLinesAvailable?.Invoke(this, new[] { line });
    }

    /// <summary>
    /// Launches <paramref name="exePath"/> and begins streaming output.
    /// Throws if the process cannot be started.
    /// </summary>
    public void Start(string exePath, string arguments, ProcessAttachmentType type,
                      string? workingDirectory = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        bool both       = type is ProcessAttachmentType.StandardOutputAndError;
        bool captureOut = both || type is ProcessAttachmentType.StandardOutput;
        bool captureErr = both || type is ProcessAttachmentType.StandardError;

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

        Process process = new() { StartInfo = si, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) => { if (e.Data != null) Emit(e.Data); };
        process.ErrorDataReceived  += (_, e) =>
        {
            if (e.Data != null) Emit(both ? $"[stderr] {e.Data}" : e.Data);
        };
        process.Exited += (_, _) =>
        {
            // Parameterless WaitForExit blocks until redirected streams reach EOF,
            // so the final lines are flushed before the exit notification.
            int code = 0;
            try { process.WaitForExit(); code = process.ExitCode; } catch { /* disposed */ }
            ProcessExited?.Invoke(this, code);
        };

        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            throw;
        }

        _process = process;
        ProcessId = process.Id;
        if (captureOut) process.BeginOutputReadLine();
        if (captureErr) process.BeginErrorReadLine();
    }

    /// <summary>Stops reading output but does NOT kill the child process.</summary>
    public void Detach()
    {
        if (_process == null) return;
        try { if (_process.HasExited) return; } catch { return; }
        try { _process.CancelOutputRead(); } catch { /* not redirected */ }
        try { _process.CancelErrorRead();  } catch { /* not redirected */ }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
        _process?.Dispose();
        _process = null;
    }
}
