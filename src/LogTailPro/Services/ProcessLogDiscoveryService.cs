using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>
/// Finds the log files a running process writes to, using three independent signals:
/// files the process has open (strongest), paths referenced by configuration files next to the
/// executable (NLog / log4net / Serilog / appsettings / ini / yaml …), and a scan of the usual log
/// folders. Results are merged, de-duplicated and scored.
/// </summary>
public sealed class ProcessLogDiscoveryService
{
    // ── Tunables ──────────────────────────────────────────────────────────────

    private static readonly string[] LogExtensions =
        { ".log", ".txt", ".out", ".err", ".trace", ".jsonl", ".ndjson" };

    private static readonly string[] ConfigPatterns =
    {
        "appsettings*.json", "nlog*.config", "nlog*.xml", "log4net*.config", "log4net*.xml",
        "serilog*.json", "logging*.json", "logging*.xml", "*.config", "*.ini", "*.yaml", "*.yml",
        "*.properties", "*.conf"
    };

    private static readonly string[] NoiseNames =
        { "readme", "license", "licence", "eula", "thirdparty", "third-party", "notice", "changelog", "release", "version", "copying" };

    private const long MaxConfigBytes = 2L * 1024 * 1024;
    private const int MaxConfigFiles = 60;
    private const int MaxFilesPerGlob = 200;
    private const int MaxScanFiles = 200;
    private const int MaxFolderEntries = 5;
    private static readonly TimeSpan RecentWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan HandleTimeout = TimeSpan.FromSeconds(8);

    private static readonly Regex DateLiteral = new(
        @"(?<![A-Za-z])(?:y{2,4}|M{2}|d{2}|H{2}|h{2}|m{2}|s{2})(?:[-_.]?(?:y{2,4}|M{2}|d{2}|H{2}|h{2}|m{2}|s{2}))*(?![A-Za-z])",
        RegexOptions.Compiled);

    private static readonly Regex Log4NetToken = new(@"%[A-Za-z]+\{[^}]*\}", RegexOptions.Compiled);
    private static readonly Regex NLogToken = new(@"\$\{[^}]*\}", RegexOptions.Compiled);
    private static readonly Regex BraceToken = new(@"\{[^{}]*\}", RegexOptions.Compiled);
    private static readonly Regex LogFileName = new(
        @"\.log([._-]?\d+)?$|\.log\.[\w\-]{1,12}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LogFolderSegment = new(
        @"(^|[\\/])logs?([\\/]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Discovers log files for a single process.</summary>
    public Task<IReadOnlyList<LogCandidate>> DiscoverAsync(ProcessInfo process, CancellationToken ct = default) =>
        DiscoverManyAsync(new[] { process }, ct);

    /// <summary>
    /// Discovers log files for several processes. The system handle table is read only once,
    /// so this is much cheaper than calling <see cref="DiscoverAsync"/> in a loop.
    /// </summary>
    public async Task<IReadOnlyList<LogCandidate>> DiscoverManyAsync(
        IReadOnlyList<ProcessInfo> processes, CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            HashSet<int> pids = processes.Where(p => p.Pid > 4).Select(p => p.Pid).ToHashSet();

            // Handle snapshot runs with its own timeout; other collectors are pure file-system work.
            Task<Dictionary<int, List<string>>> handleTask = Task.Run(() => GetOpenLogFiles(pids, ct), ct);
            Task winner = await Task.WhenAny(handleTask, Task.Delay(HandleTimeout, ct));
            Dictionary<int, List<string>> handles = winner == handleTask && handleTask.IsCompletedSuccessfully
                ? handleTask.Result
                : new Dictionary<int, List<string>>();

            List<LogCandidate> all = new();
            foreach (ProcessInfo p in processes)
            {
                ct.ThrowIfCancellationRequested();
                Dictionary<string, LogCandidate> byPath = new(StringComparer.OrdinalIgnoreCase);

                if (handles.TryGetValue(p.Pid, out List<string>? open))
                    foreach (string path in open)
                        Add(byPath, p, path, LogSource.OpenHandle, "open file handle");

                CollectFromConfig(p, byPath, ct);
                CollectFromFolders(p, byPath, ct);

                List<LogCandidate> ranked = byPath.Values.ToList();
                foreach (LogCandidate c in ranked) c.Score = ComputeScore(c);
                ranked = ranked.OrderByDescending(c => c.Score).ThenByDescending(c => c.LastWriteUtc).ToList();
                if (ranked.Count > 0) ranked[0].IsRecommended = true;
                all.AddRange(ranked);
            }
            return (IReadOnlyList<LogCandidate>)all;
        }, ct);
    }

    // ── Merge / score ─────────────────────────────────────────────────────────

    private static void Add(Dictionary<string, LogCandidate> byPath, ProcessInfo p, string path,
                            LogSource source, string detail)
    {
        FileInfo fi;
        try
        {
            fi = new FileInfo(path);
            if (!fi.Exists) return;
        }
        catch { return; }

        string key = fi.FullName;
        if (!byPath.TryGetValue(key, out LogCandidate? c))
        {
            c = new LogCandidate
            {
                Path = key,
                SizeBytes = fi.Length,
                LastWriteUtc = fi.LastWriteTimeUtc,
                ProcessName = p.Name,
                Pid = p.Pid,
                Detail = detail
            };
            byPath[key] = c;
        }
        else if (!c.Detail.Contains(detail, StringComparison.Ordinal))
        {
            c.Detail += "; " + detail;
        }
        c.Sources |= source;
    }

    private static int ComputeScore(LogCandidate c)
    {
        int score = 0;
        if (c.Sources.HasFlag(LogSource.OpenHandle)) score += 100;
        if (c.Sources.HasFlag(LogSource.Config)) score += 60;
        if (c.Sources.HasFlag(LogSource.FolderScan)) score += 20;

        TimeSpan age = DateTime.UtcNow - c.LastWriteUtc;
        if (age < TimeSpan.FromHours(1)) score += 30;
        else if (age < TimeSpan.FromDays(1)) score += 15;
        else if (age < TimeSpan.FromDays(7)) score += 5;

        if (c.Path.EndsWith(".log", StringComparison.OrdinalIgnoreCase)) score += 10;
        return score;
    }

    // ── Collector: config files ───────────────────────────────────────────────

    private static void CollectFromConfig(ProcessInfo p, Dictionary<string, LogCandidate> byPath, CancellationToken ct)
    {
        string exeDir = ExeDirectory(p);
        if (exeDir.Length == 0 || IsUnderWindows(exeDir)) return;
        string exeName = Path.GetFileNameWithoutExtension(p.ExecutablePath);

        foreach (string configFile in FindConfigFiles(p.ExecutablePath))
        {
            ct.ThrowIfCancellationRequested();
            string content;
            try { content = File.ReadAllText(configFile); }
            catch { continue; }

            string configName = Path.GetFileName(configFile);
            foreach ((string raw, string key) in ExtractPaths(content, configFile))
            {
                string? pattern = ExpandPattern(raw, Path.GetDirectoryName(configFile) ?? exeDir, exeName, exeDir);
                if (pattern == null) continue;

                string detail = $"'{key}' in {configName}";
                foreach (string file in ResolveFiles(pattern, MaxFilesPerGlob))
                    Add(byPath, p, file, LogSource.Config, detail);
            }
        }
    }

    /// <summary>Configuration files sitting next to the executable (and one level below).</summary>
    public static IReadOnlyList<string> FindConfigFiles(string exePath)
    {
        string dir = Path.GetDirectoryName(exePath) ?? string.Empty;
        if (dir.Length == 0 || !Directory.Exists(dir)) return Array.Empty<string>();

        HashSet<string> found = new(StringComparer.OrdinalIgnoreCase);

        void ScanDir(string d)
        {
            foreach (string pattern in ConfigPatterns)
            {
                if (found.Count >= MaxConfigFiles) return;
                try
                {
                    foreach (string f in Directory.EnumerateFiles(d, pattern))
                    {
                        try { if (new FileInfo(f).Length <= MaxConfigBytes) found.Add(f); }
                        catch { /* ignore */ }
                        if (found.Count >= MaxConfigFiles) return;
                    }
                }
                catch { /* access denied */ }
            }
        }

        // The exe's own config first so it is never crowded out by the cap.
        string own = exePath + ".config";
        if (File.Exists(own)) found.Add(own);

        ScanDir(dir);
        try
        {
            foreach (string sub in Directory.EnumerateDirectories(dir).Take(30))
            {
                string name = Path.GetFileName(sub);
                if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;
                ScanDir(sub);
            }
        }
        catch { /* access denied */ }

        return found.ToList();
    }

    /// <summary>
    /// Extracts values from a config file that look like log paths. Returns (rawValue, keyLabel) pairs;
    /// values still contain variables such as <c>${basedir}</c>.
    /// </summary>
    public static IReadOnlyList<(string Raw, string Key)> ExtractPaths(string content, string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        List<(string, string)> results = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        void Consider(string key, string value, bool unescapeJson = false)
        {
            string v = value.Trim().Trim('"', '\'');
            if (unescapeJson) v = v.Replace("\\\\", "\\").Replace("\\/", "/");
            if (!LooksLikeLogPath(v)) return;
            if (seen.Add(v)) results.Add((v, key));
        }

        if (ext == ".json")
        {
            foreach (Match m in Regex.Matches(content, "\"(?<key>[^\"]+)\"\\s*:\\s*\"(?<val>(?:[^\"\\\\]|\\\\.)*)\""))
                Consider(m.Groups["key"].Value, m.Groups["val"].Value, unescapeJson: true);
        }
        else if (ext is ".ini" or ".properties" or ".yaml" or ".yml" or ".conf")
        {
            foreach (Match m in Regex.Matches(content, @"^\s*-?\s*(?<key>[\w.\-\[\]]+)\s*[=:]\s*(?<val>[^\r\n#;]+)", RegexOptions.Multiline))
                Consider(m.Groups["key"].Value, m.Groups["val"].Value);
        }
        else // xml / .config / anything else: attributes and element text
        {
            foreach (Match m in Regex.Matches(content, "(?<key>[A-Za-z_:][\\w:.\\-]*)\\s*=\\s*\"(?<val>[^\"<>]+)\""))
                Consider(m.Groups["key"].Value, System.Net.WebUtility.HtmlDecode(m.Groups["val"].Value));
            foreach (Match m in Regex.Matches(content, @"<(?<key>[\w:.\-]+)[^<>]*>(?<val>[^<>]+)</\k<key>>"))
                Consider(m.Groups["key"].Value, System.Net.WebUtility.HtmlDecode(m.Groups["val"].Value));
        }

        return results;
    }

    /// <summary>True when a config value plausibly names a log file or log folder.</summary>
    private static bool LooksLikeLogPath(string v)
    {
        if (v.Length < 3 || v.Length > 400) return false;
        if (v.Contains('\n') || v.Contains('\r')) return false;
        if (v.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return false;
        if (v.Contains("://", StringComparison.Ordinal)) return false;

        string withoutTokens = NLogToken.Replace(v, "");
        string ext = Path.GetExtension(withoutTokens);
        bool extOk = LogExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) || LogFileName.IsMatch(withoutTokens);
        bool folderOk = LogFolderSegment.IsMatch(v) && (v.Contains('\\') || v.Contains('/') || v.Contains('$') || v.Contains('%'));
        return extOk || folderOk;
    }

    /// <summary>
    /// Expands variables in a raw config path and turns dated/rolling parts into <c>*</c> wildcards.
    /// Returns a full path pattern, or null when it cannot be made absolute.
    /// </summary>
    public static string? ExpandPattern(string raw, string baseDir, string processName, string? exeDir = null)
    {
        exeDir ??= baseDir;
        string s = raw.Trim();

        // NLog specialfolder / env / known layout variables.
        s = Regex.Replace(s, @"\$\{specialfolder:(?:folder=)?(?<f>\w+)(?::file=(?<file>[^}]*))?\}", m =>
        {
            string folder = m.Groups["f"].Value;
            string file = m.Groups["file"].Success ? m.Groups["file"].Value : string.Empty;
            string basePath = SpecialFolderPath(folder);
            return file.Length > 0 ? Path.Combine(basePath, file) : basePath;
        }, RegexOptions.IgnoreCase);

        s = Regex.Replace(s, @"\$\{(?:env|environment):(?<n>[^}:]+)[^}]*\}",
            m => Environment.GetEnvironmentVariable(m.Groups["n"].Value) ?? "*", RegexOptions.IgnoreCase);

        s = Regex.Replace(s, @"\$\{(?<n>basedir|processdir|currentdir|tempdir|processname|appdomain:[^}]*)\}", m =>
        {
            string n = m.Groups["n"].Value.ToLowerInvariant();
            return n switch
            {
                "basedir" or "processdir" => exeDir,
                "currentdir" => baseDir,
                "tempdir" => Path.GetTempPath().TrimEnd('\\'),
                "processname" => processName,
                _ => exeDir
            };
        }, RegexOptions.IgnoreCase);

        s = s.Replace("|DataDirectory|", Path.Combine(exeDir, "App_Data"), StringComparison.OrdinalIgnoreCase);

        // log4net-style %date{…} tokens first (they look like env-var syntax).
        s = Log4NetToken.Replace(s, "*");
        // Remaining ${…} (shortdate, date, level, guid …) are dynamic → wildcard.
        s = NLogToken.Replace(s, "*");
        try { s = Environment.ExpandEnvironmentVariables(s); } catch { /* keep as is */ }
        // Serilog {Date}, {HalfHour} … and any other {…}.
        s = BraceToken.Replace(s, "*");

        if (s.StartsWith("~/", StringComparison.Ordinal) || s.StartsWith("~\\", StringComparison.Ordinal))
            s = Path.Combine(exeDir, s[2..]);

        s = s.Replace('/', '\\');

        // Literal date formats in the file name ("app-yyyyMMdd.log").
        string dir = Path.GetDirectoryName(s) ?? string.Empty;
        string name = Path.GetFileName(s);
        if (name.Length > 0)
        {
            name = DateLiteral.Replace(name, "*");
            s = dir.Length > 0 ? dir + "\\" + name : name;
        }

        while (s.Contains("**", StringComparison.Ordinal)) s = s.Replace("**", "*");

        try
        {
            if (!Path.IsPathRooted(s)) s = Path.Combine(exeDir, s);
            // GetFullPath rejects '*' on some runtimes; normalise the wildcard-free prefix only.
            return s.Contains('*') || s.Contains('?') ? s : Path.GetFullPath(s);
        }
        catch { return null; }
    }

    private static string SpecialFolderPath(string name)
    {
        if (name.Equals("Temp", StringComparison.OrdinalIgnoreCase)) return Path.GetTempPath().TrimEnd('\\');
        if (Enum.TryParse(name, ignoreCase: true, out Environment.SpecialFolder folder))
        {
            string path = Environment.GetFolderPath(folder);
            if (path.Length > 0) return path;
        }
        return "*";
    }

    /// <summary>
    /// Resolves a (possibly wildcarded) full path pattern to existing files. Directory-valued patterns
    /// yield the most recently written log-like files inside them. Wildcards are allowed in any segment.
    /// </summary>
    public static IReadOnlyList<string> ResolveFiles(string pattern, int max)
    {
        List<string> files = new();
        try
        {
            if (pattern.IndexOfAny(new[] { '*', '?' }) < 0)
            {
                if (File.Exists(pattern)) return new[] { pattern };
                if (Directory.Exists(pattern)) return NewestLogFiles(pattern, MaxFolderEntries);

                // Rolled variants of a missing exact file: app.log -> app*.log / app.log*
                string d = Path.GetDirectoryName(pattern) ?? string.Empty;
                string stem = Path.GetFileNameWithoutExtension(pattern);
                string e = Path.GetExtension(pattern);
                if (Directory.Exists(d) && stem.Length > 0)
                {
                    pattern = Path.Combine(d, stem + "*" + e);
                    if (!pattern.Contains('*')) return files;
                }
                else return files;
            }

            string[] segments = pattern.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return files;

            // Root: "C:" -> "C:\" ; UNC \\server\share kept together.
            string root;
            int start;
            if (pattern.StartsWith("\\\\", StringComparison.Ordinal) && segments.Length >= 2)
            { root = "\\\\" + segments[0] + "\\" + segments[1] + "\\"; start = 2; }
            else
            { root = segments[0] + "\\"; start = 1; }

            ResolveSegments(root, segments, start, files, max);
        }
        catch { /* bad path */ }

        // Newest first; callers keep what they need.
        return files
            .OrderByDescending(f => { try { return File.GetLastWriteTimeUtc(f); } catch { return DateTime.MinValue; } })
            .Take(MaxFolderEntries * 2)
            .ToList();
    }

    private static void ResolveSegments(string current, string[] segments, int index, List<string> files, int max)
    {
        if (files.Count >= max || !Directory.Exists(current)) return;
        string seg = segments[index];
        bool last = index == segments.Length - 1;

        try
        {
            if (last)
            {
                foreach (string f in Directory.EnumerateFiles(current, seg))
                {
                    files.Add(f);
                    if (files.Count >= max) return;
                }
            }
            else if (seg.Contains('*') || seg.Contains('?'))
            {
                foreach (string d in Directory.EnumerateDirectories(current, seg).Take(50))
                    ResolveSegments(d + "\\", segments, index + 1, files, max);
            }
            else
            {
                ResolveSegments(Path.Combine(current, seg) + "\\", segments, index + 1, files, max);
            }
        }
        catch { /* access denied / path too long */ }
    }

    private static IReadOnlyList<string> NewestLogFiles(string dir, int count)
    {
        try
        {
            return Directory.EnumerateFiles(dir)
                .Where(IsLogLike)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(count)
                .Select(f => f.FullName)
                .ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    // ── Collector: folder scan ────────────────────────────────────────────────

    private static void CollectFromFolders(ProcessInfo p, Dictionary<string, LogCandidate> byPath, CancellationToken ct)
    {
        string exeDir = ExeDirectory(p);
        if (exeDir.Length == 0 || IsUnderWindows(exeDir)) return;
        string exeName = Path.GetFileNameWithoutExtension(p.ExecutablePath);
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string? parent = Path.GetDirectoryName(exeDir);

        List<string> dirs = new() { exeDir };
        foreach (string sub in new[] { "logs", "log", "Logs", "Log" })
        {
            dirs.Add(Path.Combine(exeDir, sub));
            if (parent != null) dirs.Add(Path.Combine(parent, sub));
        }
        foreach (string baseDir in new[] { programData, localApp, roaming, Path.GetTempPath() })
        {
            if (baseDir.Length == 0) continue;
            dirs.Add(Path.Combine(baseDir, exeName));
            dirs.Add(Path.Combine(baseDir, exeName, "logs"));
        }

        DateTime cutoff = DateTime.UtcNow - RecentWindow;
        int scanned = 0;
        foreach (string dir in dirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(dir)) continue;

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir); }
            catch { continue; }

            foreach (string f in files)
            {
                if (++scanned > MaxScanFiles) return;
                if (!IsLogLike(f)) continue;

                string name = Path.GetFileNameWithoutExtension(f);
                if (NoiseNames.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase))) continue;

                DateTime written;
                try { written = File.GetLastWriteTimeUtc(f); }
                catch { continue; }
                // Old files only count when they are clearly named as logs.
                if (written < cutoff && !name.Contains("log", StringComparison.OrdinalIgnoreCase)) continue;

                Add(byPath, p, f, LogSource.FolderScan, $"recent log in {Path.GetFileName(dir.TrimEnd('\\'))}");
            }
        }
    }

    // ── Collector: open handles ───────────────────────────────────────────────

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int infoClass, IntPtr buffer, int length, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DuplicateHandle(IntPtr srcProcess, IntPtr srcHandle, IntPtr dstProcess,
        out IntPtr dstHandle, uint access, bool inherit, uint options);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern uint GetFileType(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(IntPtr handle, StringBuilder path, uint length, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private const int SystemExtendedHandleInformation = 64;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    private const uint ProcessDupHandle = 0x0040;
    private const uint DuplicateSameAccess = 0x2;
    private const uint FileTypeDisk = 0x1;

    /// <summary>
    /// Returns, for each requested PID, the log-like files it currently has open.
    /// Best effort: processes we may not duplicate handles from (other users, protected) are skipped.
    /// </summary>
    private static Dictionary<int, List<string>> GetOpenLogFiles(HashSet<int> pids, CancellationToken ct)
    {
        Dictionary<int, List<string>> result = new();
        if (pids.Count == 0) return result;

        // A file handle we own tells us which ObjectTypeIndex means "File" on this Windows build.
        string probe = Path.GetTempFileName();
        try
        {
            using FileStream probeStream = new(probe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            IntPtr probeHandle = probeStream.SafeFileHandle.DangerousGetHandle();

            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (!TryReadHandleTable(out buffer, out long count)) return result;

                int ptr = IntPtr.Size;
                int entrySize = 3 * ptr + 16;
                int headerSize = 2 * ptr;
                int typeOffset = 3 * ptr + 6;
                int ownPid = Environment.ProcessId;

                // Pass 1: find the File type index.
                int fileType = -1;
                for (long i = 0; i < count; i++)
                {
                    IntPtr entry = buffer + headerSize + (int)(i * entrySize);
                    if ((int)Marshal.ReadIntPtr(entry, ptr) == ownPid &&
                        Marshal.ReadIntPtr(entry, 2 * ptr) == probeHandle)
                    {
                        fileType = Marshal.ReadInt16(entry, typeOffset);
                        break;
                    }
                }
                if (fileType < 0) return result;

                // Pass 2: duplicate candidate handles per target process and resolve their paths.
                Dictionary<int, List<IntPtr>> handlesByPid = new();
                for (long i = 0; i < count; i++)
                {
                    IntPtr entry = buffer + headerSize + (int)(i * entrySize);
                    int pid = (int)Marshal.ReadIntPtr(entry, ptr);
                    if (!pids.Contains(pid)) continue;
                    if (Marshal.ReadInt16(entry, typeOffset) != fileType) continue;

                    if (!handlesByPid.TryGetValue(pid, out List<IntPtr>? list))
                        handlesByPid[pid] = list = new List<IntPtr>();
                    list.Add(Marshal.ReadIntPtr(entry, 2 * ptr));
                }

                foreach ((int pid, List<IntPtr> handleValues) in handlesByPid)
                {
                    ct.ThrowIfCancellationRequested();
                    IntPtr hProc = OpenProcess(ProcessDupHandle, false, pid);
                    if (hProc == IntPtr.Zero) continue;
                    try
                    {
                        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
                        foreach (IntPtr hv in handleValues)
                        {
                            string? path = ResolveHandlePath(hProc, hv);
                            if (path != null && IsLogLike(path)) paths.Add(path);
                        }
                        if (paths.Count > 0) result[pid] = paths.ToList();
                    }
                    finally { CloseHandle(hProc); }
                }
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* best effort */ }
        finally { try { File.Delete(probe); } catch { /* ignore */ } }

        return result;
    }

    private static bool TryReadHandleTable(out IntPtr buffer, out long count)
    {
        int size = 8 * 1024 * 1024;
        buffer = IntPtr.Zero;
        count = 0;
        for (int attempt = 0; attempt < 7; attempt++)
        {
            buffer = Marshal.AllocHGlobal(size);
            int status = NtQuerySystemInformation(SystemExtendedHandleInformation, buffer, size, out int needed);
            if (status == 0)
            {
                count = Marshal.ReadIntPtr(buffer).ToInt64();
                return true;
            }
            Marshal.FreeHGlobal(buffer);
            buffer = IntPtr.Zero;
            if (status != StatusInfoLengthMismatch) return false;
            size = Math.Max(size * 2, needed + (1 << 20));
        }
        return false;
    }

    private static string? ResolveHandlePath(IntPtr hProcess, IntPtr handleValue)
    {
        if (!DuplicateHandle(hProcess, handleValue, GetCurrentProcess(), out IntPtr dup, 0, false, DuplicateSameAccess))
            return null;
        try
        {
            // Only real disk files: querying pipes/sockets by handle can block forever.
            if (GetFileType(dup) != FileTypeDisk) return null;

            StringBuilder sb = new(512);
            uint len = GetFinalPathNameByHandle(dup, sb, (uint)sb.Capacity, 0);
            if (len == 0 || len >= sb.Capacity) return null;

            string path = sb.ToString();
            if (path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal)) path = @"\\" + path[8..];
            else if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) path = path[4..];
            return File.Exists(path) ? path : null;
        }
        finally { CloseHandle(dup); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ExeDirectory(ProcessInfo p) =>
        string.IsNullOrEmpty(p.ExecutablePath) ? string.Empty : Path.GetDirectoryName(p.ExecutablePath) ?? string.Empty;

    private static bool IsUnderWindows(string dir)
    {
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return win.Length > 0 && dir.StartsWith(win, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True for files whose name suggests a text log.</summary>
    public static bool IsLogLike(string path)
    {
        string ext = Path.GetExtension(path);
        return LogExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) || LogFileName.IsMatch(path);
    }
}
