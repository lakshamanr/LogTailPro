using System.IO;

namespace LogTailPro.Services;

/// <summary>
/// Watches a directory and raises <see cref="NewFileDetected"/> when a matching file is created.
/// </summary>
public sealed class DirectoryMonitorService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    /// <summary>Raised on the thread pool when a new matching file appears in the watched directory.</summary>
    public event EventHandler<string>? NewFileDetected;

    /// <summary>
    /// Starts monitoring <paramref name="directoryPath"/> for files matching <paramref name="filter"/>.
    /// </summary>
    public void StartMonitoring(string directoryPath, string filter = "*.log")
    {
        _watcher?.Dispose();

        _watcher = new FileSystemWatcher(directoryPath, filter)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        _watcher.Created += (_, e) => NewFileDetected?.Invoke(this, e.FullPath);
    }

    /// <summary>Directory currently being monitored, or empty string if not started.</summary>
    public string MonitoredPath => _watcher?.Path ?? string.Empty;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher?.Dispose();
    }
}
