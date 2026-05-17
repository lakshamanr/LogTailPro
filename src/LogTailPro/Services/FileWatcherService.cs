using System.IO;
using System.Text;

namespace LogTailPro.Services;

/// <summary>
/// Watches a log file for new content using <see cref="FileSystemWatcher"/>.
/// Reads only new bytes via byte-offset seeking — never re-reads the full file.
/// </summary>
public sealed class FileWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private long _lastReadPosition;
    private string _filePath = string.Empty;
    private Encoding _encoding = Encoding.UTF8;
    private bool _disposed;

    /// <summary>Raised on the thread pool when new lines are available.</summary>
    public event EventHandler<IReadOnlyList<string>>? NewLinesAvailable;

    /// <summary>Raised when the file is deleted or renamed away.</summary>
    public event EventHandler? FileDeleted;

    /// <summary>Raised when log rotation is detected (file shrunk).</summary>
    public event EventHandler? FileTruncated;

    /// <summary>
    /// Begins watching <paramref name="filePath"/> for appended content.
    /// Call after the initial load so <paramref name="initialPosition"/> skips already-read bytes.
    /// </summary>
    public void StartWatching(string filePath, long initialPosition, Encoding? encoding = null)
    {
        _filePath = filePath;
        _lastReadPosition = initialPosition;
        _encoding = encoding ?? Encoding.UTF8;

        _watcher = new FileSystemWatcher(
            Path.GetDirectoryName(filePath)!,
            Path.GetFileName(filePath))
        {
            // NotifyFilters.LastAccess fires too often; use LastWrite | Size only
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Deleted += OnFileDeleted;
        _watcher.Renamed += OnFileDeleted;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // FileShare.ReadWrite | Delete so we never lock the writer
            using FileStream stream = new(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            // Detect log rotation (file truncated / rolled)
            if (stream.Length < _lastReadPosition)
            {
                _lastReadPosition = 0;
                FileTruncated?.Invoke(this, EventArgs.Empty);
            }

            if (stream.Length == _lastReadPosition)
                return;

            stream.Seek(_lastReadPosition, SeekOrigin.Begin);

            List<string> newLines = new();
            using StreamReader reader = new(stream, _encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);

            string? line;
            while ((line = reader.ReadLine()) != null)
                newLines.Add(line);

            _lastReadPosition = stream.Position;

            if (newLines.Count > 0)
                NewLinesAvailable?.Invoke(this, newLines);
        }
        catch (IOException)
        {
            // File temporarily locked by writer — skip this change event
        }
        catch (Exception)
        {
            // Swallow unexpected errors to keep the watcher alive
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        FileDeleted?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher?.Dispose();
    }
}
