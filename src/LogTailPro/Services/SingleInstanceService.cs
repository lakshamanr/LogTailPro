using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace LogTailPro.Services;

/// <summary>
/// Keeps LogTailPro to one running instance per Windows user. The first instance owns a named mutex and
/// listens on a per-user named pipe; later launches (e.g. Explorer "Open with") forward their file
/// arguments through the pipe and exit, so the running window just gains new tabs.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private const char ArgSeparator = '\0';
    private const int MaxMessageChars = 64 * 1024;
    private const int MaxArgs = 200;
    private const int ConnectTimeoutMs = 5000;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);

    private const int AsfwAny = -1;

    private readonly string _mutexName;
    private readonly string _pipeName;
    private Mutex? _mutex;
    private CancellationTokenSource? _cts;
    private Action<IReadOnlyList<string>>? _handler;
    private readonly ConcurrentQueue<IReadOnlyList<string>> _pending = new();
    private readonly object _handlerLock = new();

    /// <summary>Creates the service; names are per user so different users never talk to each other.</summary>
    public SingleInstanceService()
    {
        string sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        _mutexName = $"Local\\LogTailPro.Instance.{sid}";
        _pipeName = $"LogTailPro.Pipe.{sid}";
    }

    /// <summary>
    /// Tries to become the primary instance. Returns true when this process is the first one (and starts
    /// listening for forwarded arguments); false when another instance already owns the mutex.
    /// </summary>
    public bool TryBecomePrimary()
    {
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out bool createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        _cts = new CancellationTokenSource();
        StartListening(_cts.Token);
        return true;
    }

    /// <summary>
    /// Sets the callback invoked (on a pool thread) for every forwarded argument list. Messages that arrived
    /// before the handler was attached are delivered immediately.
    /// </summary>
    public void SetHandler(Action<IReadOnlyList<string>> handler)
    {
        lock (_handlerLock)
        {
            _handler = handler;
            while (_pending.TryDequeue(out IReadOnlyList<string>? args))
                handler(args);
        }
    }

    /// <summary>
    /// Sends <paramref name="args"/> to the primary instance. File paths are made absolute first, because the
    /// primary instance has a different working directory. Returns false when the primary instance cannot be
    /// reached (the caller should then start normally instead of losing the request).
    /// </summary>
    public bool TrySendToPrimary(IEnumerable<string> args)
    {
        try
        {
            string[] absolute = args
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.StartsWith('-') ? a : SafeFullPath(a))
                .Take(MaxArgs)
                .ToArray();

            // The user just interacted with this process (double-click / Open with), so it is allowed to hand
            // foreground rights to the primary window; otherwise Windows only flashes the taskbar button.
            AllowSetForegroundWindow(AsfwAny);

            using NamedPipeClientStream client =
                new(".", _pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(ConnectTimeoutMs);

            byte[] payload = Encoding.UTF8.GetBytes(string.Join(ArgSeparator, absolute));
            client.Write(payload, 0, payload.Length);
            client.Flush();
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    /// <summary>Accepts connections one after another; a fresh pipe instance is always waiting.</summary>
    private void StartListening(CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    NamedPipeServerStream server = new(
                        _pipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                    await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                    // Hand the connection to its own task so the next client never waits for this read.
                    _ = Task.Run(() => ReadMessageAsync(server, ct), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (IOException) { await Task.Delay(200, ct).ConfigureAwait(false); }
            }
        }, ct);
    }

    private async Task ReadMessageAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        try
        {
            using (server)
            using (StreamReader reader = new(server, Encoding.UTF8))
            {
                char[] buffer = new char[MaxMessageChars];
                int total = 0, read;
                while (total < buffer.Length &&
                       (read = await reader.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct)
                                           .ConfigureAwait(false)) > 0)
                    total += read;

                string[] args = new string(buffer, 0, total)
                    .Split(ArgSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .Take(MaxArgs)
                    .ToArray();

                Deliver(args);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // Client went away mid-message; nothing to open.
        }
    }

    private void Deliver(IReadOnlyList<string> args)
    {
        lock (_handlerLock)
        {
            if (_handler == null) _pending.Enqueue(args);
            else _handler(args);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_mutex != null)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { /* not owned by this thread */ }
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
