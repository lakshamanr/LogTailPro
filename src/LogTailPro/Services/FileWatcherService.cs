using System.Buffers;
using System.IO;
using System.Text;

namespace LogTailPro.Services;

/// <summary>
/// Follows a log file for appended content. Combines a <see cref="FileSystemWatcher"/> with a short
/// polling timer (watchers miss events on network shares and under heavy write load), reads only the
/// new bytes by seeking, and delivers only complete lines — a line the writer is still in the middle of
/// is held back until its newline arrives.
/// </summary>
public sealed class FileWatcherService : IDisposable
{
    private const int PollIntervalMs = 1000;
    private const int MaxReadPerPass = 16 * 1024 * 1024;
    private static readonly char[] LineTerminators = { '\n', '\0' };

    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;
    private Timer? _pollTimer;
    private long _lastReadPosition;
    private string _filePath = string.Empty;
    private Encoding _encoding = Encoding.UTF8;
    private bool _paused;
    private bool _suspended;
    private bool _disposed;

    /// <summary>Raised on a thread-pool thread when new complete lines are available.</summary>
    public event EventHandler<IReadOnlyList<string>>? NewLinesAvailable;

    /// <summary>Raised when the file is deleted or renamed away.</summary>
    public event EventHandler? FileDeleted;

    /// <summary>Raised once when log rotation is detected (file shrank). Reading stops until <see cref="StartWatching"/> is called again.</summary>
    public event EventHandler? FileTruncated;

    /// <summary>
    /// Begins (or restarts) watching <paramref name="filePath"/>.
    /// <paramref name="initialPosition"/> must be the byte offset just after the last byte already loaded.
    /// </summary>
    public void StartWatching(string filePath, long initialPosition, Encoding? encoding = null)
    {
        lock (_gate)
        {
            StopInternal();
            if (_disposed) return;

            _filePath = filePath;
            _lastReadPosition = initialPosition;
            _encoding = encoding ?? Encoding.UTF8;
            _paused = false;
            _suspended = false;

            _watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath)!, Path.GetFileName(filePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _watcher.Changed += (_, _) => ReadNewContent();
            _watcher.Deleted += (_, _) => FileDeleted?.Invoke(this, EventArgs.Empty);
            _watcher.Renamed += (_, _) => FileDeleted?.Invoke(this, EventArgs.Empty);

            _pollTimer = new Timer(_ => ReadNewContent(), null, PollIntervalMs, PollIntervalMs);
        }
    }

    private void StopInternal()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
        _watcher?.Dispose();
        _watcher = null;
    }

    private void ReadNewContent()
    {
        lock (_gate)
        {
            List<string>? batch = null;

            if (_disposed || _paused || _suspended || _filePath.Length == 0) return;

            try
            {
                using FileStream stream = new(
                    _filePath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                long length = stream.Length;

                if (length < _lastReadPosition)
                {
                    // Rotated / truncated: stop reading until the owner reloads and restarts watching.
                    _suspended = true;
                    FileTruncated?.Invoke(this, EventArgs.Empty);
                    return;
                }

                while (_lastReadPosition < length)
                {
                    int count = (int)Math.Min(length - _lastReadPosition, MaxReadPerPass);
                    byte[] bytes = ArrayPool<byte>.Shared.Rent(count);
                    try
                    {
                        stream.Seek(_lastReadPosition, SeekOrigin.Begin);
                        int read = stream.Read(bytes, 0, count);
                        if (read <= 0) break;

                        string text = _encoding.GetString(bytes, 0, read);
                        // '\0' is the IIS-style line terminator, '\n' the normal one.
                        int lastNewline = text.LastIndexOfAny(LineTerminators);

                        string complete;
                        int consumedLength;
                        if (lastNewline >= 0)
                        {
                            consumedLength = lastNewline + 1;
                            complete = text[..consumedLength];
                        }
                        else if (read >= MaxReadPerPass)
                        {
                            consumedLength = text.Length;
                            complete = text + "\n"; // an absurdly long line: don't stall forever
                        }
                        else
                        {
                            break; // writer is mid-line: wait for the newline
                        }

                        _lastReadPosition += _encoding.GetByteCount(text[..consumedLength]);

                        batch ??= new List<string>();
                        ReadOnlySpan<char> span = complete.AsSpan();
                        while (!span.IsEmpty)
                        {
                            int idx = span.IndexOfAny('\n', '\0');
                            if (idx < 0)
                            {
                                string last = span.TrimEnd('\r').ToString();
                                if (last.Length > 0) batch.Add(last);
                                break;
                            }
                            batch.Add(span[..idx].TrimEnd('\r').ToString());
                            span = span[(idx + 1)..];
                        }

                        if (read < count) break;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(bytes);
                    }
                }
            }
            catch (IOException)
            {
                // File temporarily locked by the writer — the next poll will retry.
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            // Raised inside the lock so batches always reach the consumer in file order
            // (the consumer only queues work to the UI thread, it never blocks here).
            if (batch is { Count: > 0 })
                NewLinesAvailable?.Invoke(this, batch);
        }
    }

    /// <summary>Stops delivering new lines (position is kept, so resuming catches up).</summary>
    public void Pause()
    {
        lock (_gate) _paused = true;
    }

    /// <summary>Resumes delivery and immediately catches up on anything written while paused.</summary>
    public void Resume()
    {
        lock (_gate) _paused = false;
        ReadNewContent();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            StopInternal();
        }
    }
}
