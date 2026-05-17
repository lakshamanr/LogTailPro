# PHASE 3 — Large File Support & Virtual Scrolling

## Goal
By the end of this phase:
- Files > 2GB open without crash or freeze (< 3 seconds to display tail)
- The UI never freezes during initial load or tailing
- Users can jump to any line number instantly
- Memory usage stays under 150MB regardless of file size
- Virtual scrolling handles 1M+ visible lines smoothly

---

## Pre-flight Checklist
- [ ] Phase 2 is fully working and built without errors
- [ ] Re-read SKILL.md section 3 (Large File Initial Load — CRITICAL)

---

## Step 1: LogParserService — Chunked Tail Load

Create `Services/LogParserService.cs`:

```csharp
/// <summary>
/// Efficiently reads the tail of a large log file by seeking backwards
/// in 64KB chunks without loading the entire file.
/// </summary>
public sealed class LogParserService
{
    private const int ChunkSize = 65536; // 64KB

    public async Task<TailLoadResult> LoadTailAsync(
        string filePath,
        int maxLines,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var lines = new List<string>(maxLines);
        var encoding = await DetectEncodingAsync(filePath, ct);
        long fileSize = new FileInfo(filePath).Length;

        using var stream = new FileStream(filePath, FileMode.Open,
            FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            bufferSize: ChunkSize, useAsync: true);

        long position = fileSize;
        var buffer = new byte[ChunkSize];
        string leftover = string.Empty;

        while (position > 0 && lines.Count < maxLines)
        {
            ct.ThrowIfCancellationRequested();

            long readFrom = Math.Max(0, position - ChunkSize);
            int bytesToRead = (int)(position - readFrom);

            stream.Seek(readFrom, SeekOrigin.Begin);
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bytesToRead), ct);

            string chunk = encoding.GetString(buffer, 0, bytesRead) + leftover;
            string[] chunkLines = chunk.Split('\n');

            // Last (incomplete) line becomes leftover for next iteration
            leftover = chunkLines[0];

            // Add complete lines in reverse order
            for (int i = chunkLines.Length - 1; i >= 1; i--)
            {
                lines.Insert(0, chunkLines[i].TrimEnd('\r'));
                if (lines.Count >= maxLines) break;
            }

            position = readFrom;
            progress?.Report((int)((fileSize - position) * 100 / fileSize));
        }

        // If we reached the start, include leftover
        if (position == 0 && !string.IsNullOrEmpty(leftover))
            lines.Insert(0, leftover);

        return new TailLoadResult
        {
            Lines = lines,
            Encoding = encoding,
            FileSize = fileSize,
            TailStartByteOffset = position
        };
    }

    /// <summary>
    /// Detect encoding from BOM or default to UTF-8.
    /// Handles: UTF-8 BOM, UTF-16 LE, UTF-16 BE, ASCII.
    /// </summary>
    public async Task<Encoding> DetectEncodingAsync(string filePath, CancellationToken ct)
    {
        using var stream = new FileStream(filePath, FileMode.Open,
            FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bom = new byte[4];
        int read = await stream.ReadAsync(bom.AsMemory(0, 4), ct);

        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            return Encoding.UTF8;
        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
            return Encoding.Unicode; // UTF-16 LE
        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}

public sealed class TailLoadResult
{
    public List<string> Lines { get; init; } = new();
    public Encoding Encoding { get; init; } = Encoding.UTF8;
    public long FileSize { get; init; }
    public long TailStartByteOffset { get; init; }
}
```

---

## Step 2: Loading Progress UI

Add a loading overlay to `LogTabView.xaml`:

```xml
<!-- Loading overlay — shown only during initial file load -->
<Grid Visibility="{Binding IsLoading, Converter={StaticResource BoolToVisibilityConverter}}"
      Background="#80000000">
  <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
    <TextBlock Text="Loading file..." Foreground="White" FontSize="16"
               HorizontalAlignment="Center"/>
    <ProgressBar Value="{Binding LoadProgress}" Maximum="100"
                 Width="300" Height="20" Margin="0,12,0,0"/>
    <TextBlock Text="{Binding LoadProgress, StringFormat='{}{0}%'}"
               Foreground="White" HorizontalAlignment="Center" Margin="0,8,0,0"/>
  </StackPanel>
</Grid>
```

Add to `LogTabViewModel`:
```csharp
[ObservableProperty] private bool _isLoading = false;
[ObservableProperty] private int _loadProgress = 0;
```

Update `InitializeAsync()`:
```csharp
public async Task InitializeAsync()
{
    IsLoading = true;
    try
    {
        var progress = new Progress<int>(p => LoadProgress = p);
        var result = await _parserService.LoadTailAsync(
            _logFile.FilePath, Settings.MaxLinesInMemory, progress);

        long lineNumber = 1;
        foreach (string line in result.Lines)
        {
            _allLines.Add(new LogLine { LineNumber = lineNumber++, Text = line });
        }

        // Refresh visible lines
        await RefreshVisibleLinesAsync(CancellationToken.None);

        // Start watcher at the byte offset where tail ended
        _watcherService.StartWatching(_logFile.FilePath, result.TailStartByteOffset);
        _lineNumberCounter = lineNumber;

        StatusText = $"Watching | Lines: {_allLines.Count:N0} | {result.FileSize:N0} bytes";
    }
    finally
    {
        IsLoading = false;
    }
}
```

---

## Step 3: Go To Line Feature

Add `GoToLine` dialog:

```csharp
[RelayCommand]
private void GoToLine()
{
    var dialog = new GoToLineDialog { Owner = Application.Current.MainWindow };
    if (dialog.ShowDialog() != true) return;

    long targetLine = dialog.LineNumber;
    var match = VisibleLines.FirstOrDefault(l => l.LineNumber >= targetLine);
    if (match != null)
    {
        SelectedLogLine = match;
        // Signal view to scroll to this item
        ScrollToLineRequested?.Invoke(this, match);
    }
}

public event EventHandler<LogLine>? ScrollToLineRequested;
```

In `LogTabView.xaml.cs`:
```csharp
ViewModel.ScrollToLineRequested += (_, line) =>
    LogListView.ScrollIntoView(line);
```

---

## Step 4: Memory Management

When `_allLines.Count > Settings.MaxLinesInMemory * 1.1`:

```csharp
private void TrimOldLines()
{
    int toRemove = _allLines.Count - Settings.MaxLinesInMemory;
    if (toRemove <= 0) return;

    _allLines.RemoveRange(0, toRemove);

    // Also trim visible lines if not filtered
    App.Current.Dispatcher.Invoke(() =>
    {
        for (int i = 0; i < Math.Min(toRemove, VisibleLines.Count); i++)
            VisibleLines.RemoveAt(0);
    });
}
```

Call `TrimOldLines()` every time `NewLinesAvailable` fires.

---

## Step 5: Elapsed Time Column

Parse the timestamp from each log line and compute time elapsed since the previous line.
This is a powerful debugging feature — immediately shows you where time gaps occur.

```csharp
public sealed class TimestampParser
{
    // Common log timestamp patterns — try each in order
    private static readonly (Regex Pattern, string Format)[] Patterns =
    [
        (new Regex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}"), "yyyy-MM-dd HH:mm:ss.fff"),
        (new Regex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}"), "yyyy-MM-ddTHH:mm:ss"),
        (new Regex(@"\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2}"), "dd/MM/yyyy HH:mm:ss"),
        (new Regex(@"\d{2}:\d{2}:\d{2}\.\d{3}"), "HH:mm:ss.fff"),
    ];

    public DateTime? TryParse(string lineText)
    {
        foreach (var (pattern, format) in Patterns)
        {
            var match = pattern.Match(lineText);
            if (!match.Success) continue;
            if (DateTime.TryParseExact(match.Value, format,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
        }
        return null;
    }
}
```

Add to `LogLine`:
```csharp
public DateTime? Timestamp { get; set; }
public TimeSpan? ElapsedSincePrevious { get; set; }
public string ElapsedDisplay => ElapsedSincePrevious.HasValue
    ? $"+{ElapsedSincePrevious.Value.TotalMilliseconds:N0}ms"
    : string.Empty;
```

Add an optional "Elapsed" column to the GridView (user can toggle it).
Highlight elapsed time > 1 second in orange, > 5 seconds in red.

---

## Step 6: Build and Test

```bash
dotnet build src/LogTailPro/LogTailPro.csproj
```

### Large File Test Setup
```powershell
# Generate a 200MB test log file
$sw = [System.IO.StreamWriter]::new("bigtest.log")
for ($i = 1; $i -le 2000000; $i++) {
    $sw.WriteLine("[$i] $(Get-Date -Format 'HH:mm:ss.fff') INFO This is a test log line number $i with some content")
}
$sw.Close()
```

### Manual Test Checklist
- [ ] 200MB file opens in < 3 seconds (shows last 5000 lines, not all)
- [ ] Loading progress bar appears and completes
- [ ] Memory usage in Task Manager stays < 150MB after loading
- [ ] New lines still appear in real-time after large file load
- [ ] Ctrl+G opens Go To Line dialog, jumps to correct line
- [ ] 1M lines in VisibleLines — scroll is smooth (no freeze)
- [ ] After tailing for 5 minutes, memory doesn't grow unboundedly
- [ ] File truncation (log rotation) is handled — viewer resets and restarts
- [ ] Elapsed time column appears for log files with timestamps
- [ ] Large time gaps (>1s) are highlighted in the elapsed column
- [ ] Elapsed column can be toggled on/off from View menu

---

## Phase 3 Complete When
All checkboxes above are ticked. Then read `PHASE_4.md`.

---

## Git Commit (Do This Before Moving to Next Phase)

```bash
git add -A
git commit -m "Phase 3 complete: large file support, chunked tail load, Go To Line"
```

Do not proceed to Phase 4 until this commit succeeds.
