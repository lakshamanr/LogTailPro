using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.PluginContract;
using Newtonsoft.Json;

namespace LogTailPro.Services;

public sealed partial class PluginLoaderService : ObservableObject
{
    private IPluginHost? _host;

    // ── Plugin load log (visible in Plugins Manager dialog) ──────────────────
    public ObservableCollection<string> LoadLog { get; } = new();

    public List<string> CustomPluginDirectories { get; set; } = new();
    public List<string> ExcludedPluginDirectories { get; set; } = new();

    public ObservableCollection<PluginMenuGroup> MenuGroups { get; } = new();
    public ObservableCollection<LoadedPluginInfo> LoadedPlugins { get; } = new();

    // Track all ALC instances so we can Unload() them on reload (prevents assembly leak)
    private readonly List<PluginLoadContext> _loadedContexts = new();

    // ── Plugin init timeout (public settable for testing) ────────────────────
    public static TimeSpan PluginInitTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public static async Task RunOnUIAsync(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher == null)
        {
            action();
            return;
        }

        if (app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (!app.Dispatcher.CheckAccess())
        {
            try
            {
                await app.Dispatcher.InvokeAsync(action);
            }
            catch (Exception ex) when (ex is TaskCanceledException or InvalidOperationException)
            {
                // Dispatcher shutting down or unavailable
            }
        }
        else
        {
            action();
        }
    }

    public static async Task RunOnUIAsync(Func<Task> action)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher == null)
        {
            await action();
            return;
        }

        if (app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (!app.Dispatcher.CheckAccess())
        {
            try
            {
                var op = app.Dispatcher.InvokeAsync(action);
                await op.Task.Unwrap();
            }
            catch (Exception ex) when (ex is TaskCanceledException or InvalidOperationException)
            {
                // Dispatcher shutting down or unavailable
            }
        }
        else
        {
            await action();
        }
    }

    private static void RunOnUI(Action action) => _ = RunOnUIAsync(action);

    public static string LocalPluginsDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

    public static string UserPluginsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogTailPro", "plugins");

    public string LocalPluginsPath { get; set; } = LocalPluginsDirectory;
    public string UserPluginsPath { get; set; } = UserPluginsDirectory;

    public async Task InitializeAndLoadAsync(IPluginHost host)
    {
        _host = host;
        await ReloadPluginsAsync();
    }

    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    public async Task<T> ExecuteWithReloadLockAsync<T>(Func<Task<T>> action)
    {
        await _reloadLock.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public async Task ReloadPluginsAsync()
    {
        await _reloadLock.WaitAsync();
        try
        {
            await ReloadInternalAsync();
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    internal async Task ReloadInternalAsync()
    {
        LoadedPluginInfo[] previousPlugins = [];
        await RunOnUIAsync(() =>
        {
            previousPlugins = LoadedPlugins.ToArray();
            LoadLog.Clear();
            LogInfo("── Plugin reload started ──────────────────────────────────────");
        });

        // 1. Shutdown existing plugins gracefully on the UI thread
        foreach (var p in previousPlugins)
        {
            await RunOnUIAsync(() =>
            {
                try
                {
                    p.HostSession?.Deactivate();
                    var sw = Stopwatch.StartNew();
                    p.Plugin.Shutdown();
                    sw.Stop();
                    if (sw.ElapsedMilliseconds > 50)
                    {
                        LogWarn($"[{p.Name}] Shutdown took {sw.ElapsedMilliseconds}ms on the UI dispatcher.");
                    }
                }
                catch (Exception ex)
                {
                    LogWarn($"Shutdown error for '{p.Name}': {ex.Message}");
                }
            });
        }

        // 2. Unload isolated ALCs to free memory (assembly leak prevention)
        foreach (var alc in _loadedContexts)
        {
            try { alc.Unload(); } catch { /* best-effort */ }
        }
        _loadedContexts.Clear();

        await RunOnUIAsync(() =>
        {
            LoadedPlugins.Clear();
            MenuGroups.Clear();
        });

        // 3. Snapshot directory inputs
        var customDirs = CustomPluginDirectories.ToList();
        var excludedDirs = ExcludedPluginDirectories.ToList();
        var userDir = UserPluginsPath;
        var localDir = LocalPluginsPath;
        var hostVersion = _host?.LogTailProVersion;

        // 4. Off UI thread: Discover candidate directories in priority order, honoring exclusions
        var candidateDirs = await Task.Run(() => DiscoverCandidateDirectories(customDirs, userDir, localDir, excludedDirs));
        LogInfo($"Discovered {candidateDirs.Count} candidate director{(candidateDirs.Count == 1 ? "y" : "ies")}.");

        // 5. Process candidate directories sequentially to respect priority order
        var loadedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in candidateDirs)
        {
            try
            {
                await ProcessCandidateDirectoryAsync(dir, loadedIds, hostVersion);
            }
            catch (Exception ex)
            {
                // Catch-all so one bad plugin never crashes the entire reload
                LogError($"Unexpected error loading plugin from '{Path.GetFileName(dir)}': {ex.Message}");
            }
        }

        await RunOnUIAsync(() =>
            LogInfo($"── Reload complete: {LoadedPlugins.Count} plugin(s) loaded ──────────────"));
    }

    internal async Task RetirePluginAsync(LoadedPluginInfo info)
    {
        var plugin = info.Plugin;
        var session = info.HostSession;
        var asm = info.Assembly;
        var name = info.Name;
        var pluginId = plugin?.PluginId ?? info.Manifest?.Id ?? string.Empty;

        await RunOnUIAsync(() =>
        {
            try
            {
                session?.Deactivate();
                var sw = Stopwatch.StartNew();
                plugin?.Shutdown();
                sw.Stop();
                if (sw.ElapsedMilliseconds > 50)
                {
                    LogWarn($"[{name}] Shutdown took {sw.ElapsedMilliseconds}ms on the UI dispatcher.");
                }
            }
            catch (Exception ex)
            {
                LogWarn($"Shutdown error for '{name}': {ex.Message}");
            }

            LoadedPlugins.Remove(info);
            var group = MenuGroups.FirstOrDefault(g => g.PluginId.Equals(pluginId, StringComparison.OrdinalIgnoreCase));
            if (group != null)
            {
                MenuGroups.Remove(group);
            }
        });

        // Unload and remove ALC if found
        if (asm != null)
        {
            var matchingAlc = _loadedContexts.FirstOrDefault(alc =>
                alc.Assemblies.Any(a => string.Equals(a.Location, asm.Location, StringComparison.OrdinalIgnoreCase)));
            if (matchingAlc != null)
            {
                _loadedContexts.Remove(matchingAlc);
                try { matchingAlc.Unload(); } catch { }
            }
        }

        info.ReleaseReferences();
        plugin = null;
        session = null;
        asm = null;
    }

    private static List<string> DiscoverCandidateDirectories(List<string> customDirs, string userDir, string localDir, List<string> excludedDirs)
    {
        EnsureDirectory(localDir);
        EnsureDirectory(userDir);

        var candidateDirs = new List<string>();
        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var excludedFullPaths = excludedDirs
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => Path.GetFullPath(e).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        void AddDir(string d)
        {
            if (Directory.Exists(d))
            {
                string full = Path.GetFullPath(d).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (excludedFullPaths.Contains(full))
                {
                    return;
                }

                if (seenDirs.Add(full))
                {
                    candidateDirs.Add(full);
                }
            }
        }

        // Priority 1: Custom directories
        foreach (var custom in customDirs)
        {
            if (Directory.Exists(custom))
            {
                if (File.Exists(Path.Combine(custom, "plugin.json")) || Directory.GetFiles(custom, "*.dll").Length > 0)
                {
                    AddDir(custom);
                }
                try
                {
                    foreach (var sub in Directory.GetDirectories(custom))
                    {
                        AddDir(sub);
                    }
                }
                catch { }
            }
        }

        // Priority 2: User AppData plugins
        if (Directory.Exists(userDir))
        {
            try
            {
                foreach (var sub in Directory.GetDirectories(userDir))
                {
                    AddDir(sub);
                }
            }
            catch { }
        }

        // Priority 3: Local plugins
        if (Directory.Exists(localDir))
        {
            try
            {
                foreach (var sub in Directory.GetDirectories(localDir))
                {
                    AddDir(sub);
                }
            }
            catch { }
        }

        return candidateDirs;
    }

    private async Task ProcessCandidateDirectoryAsync(string dir, HashSet<string> loadedIds, string? hostVersion)
    {
        // Off UI thread: Probe directory, parse manifest, load assembly in ALC, detect plugin type
        var probeResult = await Task.Run(() => ProbeDirectory(dir, loadedIds, hostVersion));

        if (probeResult.Skipped || !probeResult.Success)
        {
            return;
        }

        // On UI thread: Instantiate plugin, execute InitializeAsync with timeout, register menu
        await RunOnUIAsync(async () =>
        {
            ILogTailPlugin plugin;
            try
            {
                var swCtor = Stopwatch.StartNew();
                var instance = Activator.CreateInstance(probeResult.PluginType!);
                swCtor.Stop();
                if (swCtor.ElapsedMilliseconds > 50)
                {
                    LogWarn($"[{Path.GetFileName(dir)}] Plugin constructor took {swCtor.ElapsedMilliseconds}ms on the UI dispatcher.");
                }

                if (instance is not ILogTailPlugin p)
                {
                    try { probeResult.LoadContext?.Unload(); } catch { }
                    LogError($"[{Path.GetFileName(dir)}] Created instance does not implement ILogTailPlugin.");
                    return;
                }
                plugin = p;
            }
            catch (Exception ex)
            {
                try { probeResult.LoadContext?.Unload(); } catch { }
                LogError($"[{Path.GetFileName(dir)}] Activator.CreateInstance failed: {ex.InnerException?.Message ?? ex.Message}");
                return;
            }

            // Dedup by plugin runtime ID
            if (loadedIds.Contains(plugin.PluginId))
            {
                try { probeResult.LoadContext?.Unload(); } catch { }
                LogInfo($"[{plugin.PluginId}] Skipped (duplicate plugin ID).");
                return;
            }

            // Default manifest if not provided
            var manifest = probeResult.Manifest ?? new PluginManifest
            {
                Id = plugin.PluginId,
                Name = plugin.PluginName,
                Version = plugin.Version,
                EntryAssembly = Path.GetFileName(probeResult.Assembly?.Location ?? string.Empty),
                Menu = new PluginMenuDefinition
                {
                    Header = plugin.PluginName,
                    Items = new List<PluginMenuItem>
                    {
                        new() { Id = "default", Header = $"Open {plugin.PluginName}" }
                    }
                }
            };

            // ── InitializeAsync with timeout on UI Dispatcher ─────────────────────
            PluginHostSession? session = null;
            if (_host != null)
            {
                session = new PluginHostSession(_host, plugin.PluginId, msg => LogWarn(msg));
                using var cts = new CancellationTokenSource(PluginInitTimeout);

                var swInitSync = Stopwatch.StartNew();
                Task initTask;
                try
                {
                    initTask = plugin.InitializeAsync(session);
                }
                catch (Exception ex)
                {
                    swInitSync.Stop();
                    session.Deactivate();
                    try { plugin.Shutdown(); } catch { }
                    try { probeResult.LoadContext?.Unload(); } catch { }
                    LogError($"[{plugin.PluginId}] InitializeAsync threw synchronously: {ex.Message}");
                    return;
                }
                swInitSync.Stop();
                if (swInitSync.ElapsedMilliseconds > 50)
                {
                    LogWarn($"[{plugin.PluginId}] InitializeAsync synchronous portion took {swInitSync.ElapsedMilliseconds}ms on the UI dispatcher.");
                }

                try
                {
                    await initTask.WaitAsync(cts.Token);
                    LogInfo($"[{plugin.PluginId}] Initialized successfully (v{plugin.Version}).");
                }
                catch (OperationCanceledException)
                {
                    session.Deactivate();
                    LogError($"[{plugin.PluginId}] InitializeAsync timed out after {PluginInitTimeout.TotalSeconds}s. Plugin skipped.");

                    // A timeout does not stop the underlying asynchronous task.
                    // Schedule cleanup for late completion so initialization and shutdown do not race:
                    _ = initTask.ContinueWith(async t =>
                    {
                        await RunOnUIAsync(() =>
                        {
                            try
                            {
                                var swLateShutdown = Stopwatch.StartNew();
                                plugin.Shutdown();
                                swLateShutdown.Stop();
                                if (swLateShutdown.ElapsedMilliseconds > 50)
                                {
                                    LogWarn($"[{plugin.PluginId}] Late shutdown took {swLateShutdown.ElapsedMilliseconds}ms on the UI dispatcher.");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogWarn($"[{plugin.PluginId}] Late shutdown error: {ex.Message}");
                            }
                            try { probeResult.LoadContext?.Unload(); } catch { }
                        });
                    }, TaskScheduler.Default);

                    return;
                }
                catch (Exception ex)
                {
                    session.Deactivate();
                    try { plugin.Shutdown(); } catch { }
                    try { probeResult.LoadContext?.Unload(); } catch { }
                    LogError($"[{plugin.PluginId}] InitializeAsync threw: {ex.Message}");
                    return;
                }
            }

            // Reserve IDs only AFTER successful initialization so fallbacks are never blocked
            loadedIds.Add(plugin.PluginId);
            if (!string.IsNullOrWhiteSpace(manifest.Id))
            {
                loadedIds.Add(manifest.Id);
            }

            if (probeResult.LoadContext != null)
            {
                _loadedContexts.Add(probeResult.LoadContext);
            }

            var pluginInfo = new LoadedPluginInfo(plugin, manifest, dir, probeResult.Assembly!, session);

            // Build Menu Group
            var group = new PluginMenuGroup
            {
                PluginId = plugin.PluginId,
                Header = manifest.Menu?.Header ?? plugin.PluginName
            };

            if (manifest.Menu?.Items != null)
            {
                foreach (var item in manifest.Menu.Items)
                {
                    group.Items.Add(new PluginMenuCommandViewModel
                    {
                        PluginId = plugin.PluginId,
                        CommandId = item.Id,
                        Header = item.Header,
                        Shortcut = item.Shortcut,
                        SeparatorBefore = item.SeparatorBefore,
                        Command = new RelayCommand(() => ExecutePluginCommand(plugin.PluginId, item.Id))
                    });
                }
            }

            // Include any dynamic menu items registered during InitializeAsync
            if (session != null)
            {
                foreach (var item in session.GetPendingMenuItems())
                {
                    if (!group.Items.Any(i => string.Equals(i.CommandId, item.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        group.Items.Add(new PluginMenuCommandViewModel
                        {
                            PluginId = plugin.PluginId,
                            CommandId = item.Id,
                            Header = item.Header,
                            Shortcut = item.Shortcut,
                            SeparatorBefore = item.SeparatorBefore,
                            Command = new RelayCommand(() => ExecutePluginCommand(plugin.PluginId, item.Id))
                        });
                    }
                }
            }

            LoadedPlugins.Add(pluginInfo);
            MenuGroups.Add(group);
        });
    }

    private sealed record DirectoryProbeResult(
        bool Success,
        bool Skipped,
        PluginManifest? Manifest,
        PluginLoadContext? LoadContext,
        Assembly? Assembly,
        Type? PluginType)
    {
        public static DirectoryProbeResult SkippedResult => new(false, true, null, null, null, null);
        public static DirectoryProbeResult FailedResult => new(false, false, null, null, null, null);
    }

    private DirectoryProbeResult ProbeDirectory(string dir, HashSet<string> loadedIds, string? hostVersion)
    {
        string manifestPath = Path.Combine(dir, "plugin.json");
        PluginManifest? manifest = null;

        if (File.Exists(manifestPath))
        {
            try
            {
                string json = File.ReadAllText(manifestPath);
                manifest = JsonConvert.DeserializeObject<PluginManifest>(json);
            }
            catch (Exception ex)
            {
                LogWarn($"[{Path.GetFileName(dir)}] Failed to parse plugin.json: {ex.Message}");
            }
        }

        // Skip if already loaded by manifest ID
        if (manifest != null && !string.IsNullOrWhiteSpace(manifest.Id) && loadedIds.Contains(manifest.Id))
        {
            LogInfo($"[{manifest.Id}] Skipped (already loaded).");
            return DirectoryProbeResult.SkippedResult;
        }

        // ── Version gate ─────────────────────────────────────────────────────
        if (manifest != null && !string.IsNullOrWhiteSpace(manifest.MinLogTailProVersion))
        {
            if (hostVersion != null &&
                Version.TryParse(manifest.MinLogTailProVersion, out var required) &&
                Version.TryParse(hostVersion.Split('+')[0], out var current) &&
                current < required)
            {
                LogWarn($"[{manifest.Id ?? Path.GetFileName(dir)}] Requires LogTailPro >= {manifest.MinLogTailProVersion} (current: {hostVersion}). Skipped.");
                return DirectoryProbeResult.SkippedResult;
            }
        }

        string? assemblyPath = null;
        Assembly? asm = null;
        Type? pluginType = null;
        PluginLoadContext? alc = null;

        if (manifest != null && !string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            assemblyPath = Path.Combine(dir, manifest.EntryAssembly);
            if (File.Exists(assemblyPath))
            {
                try
                {
                    alc = new PluginLoadContext(assemblyPath);
                    asm = alc.LoadFromAssemblyPath(assemblyPath);
                    if (!string.IsNullOrWhiteSpace(manifest.EntryClass))
                    {
                        pluginType = asm.GetType(manifest.EntryClass);
                    }
                    pluginType ??= FindPluginType(asm, dir);
                }
                catch (BadImageFormatException ex)
                {
                    try { alc?.Unload(); } catch { }
                    LogError($"[{Path.GetFileName(dir)}] Not a valid .NET assembly '{manifest.EntryAssembly}': {ex.Message}");
                    return DirectoryProbeResult.FailedResult;
                }
                catch (FileNotFoundException ex)
                {
                    try { alc?.Unload(); } catch { }
                    LogError($"[{Path.GetFileName(dir)}] Missing dependency for '{manifest.EntryAssembly}': {ex.Message}");
                    return DirectoryProbeResult.FailedResult;
                }
                catch (Exception ex)
                {
                    try { alc?.Unload(); } catch { }
                    LogError($"[{Path.GetFileName(dir)}] Failed loading entry assembly '{manifest.EntryAssembly}': {ex.Message}");
                    return DirectoryProbeResult.FailedResult;
                }
            }
            else
            {
                LogError($"[{Path.GetFileName(dir)}] Entry assembly '{manifest.EntryAssembly}' not found in directory.");
                return DirectoryProbeResult.FailedResult;
            }
        }
        else
        {
            // Auto-detect: probe each DLL, skip contract, prefer *Extension.dll / *Plugin.dll
            var dlls = Directory.GetFiles(dir, "*.dll")
                .Where(f => !Path.GetFileName(f).Equals("LogTailPro.PluginContract.dll", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f =>
                    f.EndsWith("Extension.dll", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith("Plugin.dll", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var dll in dlls)
            {
                PluginLoadContext? probeAlc = null;
                try
                {
                    probeAlc = new PluginLoadContext(dll);
                    var probeAsm = probeAlc.LoadFromAssemblyPath(dll);
                    var detectedType = FindPluginType(probeAsm, dir);
                    if (detectedType != null)
                    {
                        assemblyPath = dll;
                        alc = probeAlc;
                        asm = probeAsm;
                        pluginType = detectedType;
                        break;
                    }
                    else
                    {
                        try { probeAlc.Unload(); } catch { }
                    }
                }
                catch (BadImageFormatException)
                {
                    LogWarn($"[{Path.GetFileName(dir)}] Skipped '{Path.GetFileName(dll)}' (not a .NET assembly).");
                    try { probeAlc?.Unload(); } catch { }
                }
                catch (FileNotFoundException ex)
                {
                    LogWarn($"[{Path.GetFileName(dir)}] Skipped '{Path.GetFileName(dll)}' (missing dependency: {ex.FileName}).");
                    try { probeAlc?.Unload(); } catch { }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    var loaderMsgs = ex.LoaderExceptions?
                        .Where(e => e != null)
                        .Select(e => e!.Message)
                        .Distinct()
                        .Take(3);
                    LogWarn($"[{Path.GetFileName(dir)}] Skipped '{Path.GetFileName(dll)}': {string.Join("; ", loaderMsgs ?? Enumerable.Empty<string>())}");
                    try { probeAlc?.Unload(); } catch { }
                }
                catch (Exception ex)
                {
                    LogWarn($"[{Path.GetFileName(dir)}] Probing '{Path.GetFileName(dll)}' skipped: {ex.Message}");
                    try { probeAlc?.Unload(); } catch { }
                }
            }
        }

        if (string.IsNullOrEmpty(assemblyPath) || asm == null || pluginType == null)
        {
            if (Directory.GetFiles(dir, "*.dll").Length > 0)
            {
                LogWarn($"[{Path.GetFileName(dir)}] No ILogTailPlugin implementation found.");
            }
            try { alc?.Unload(); } catch { }
            return DirectoryProbeResult.FailedResult;
        }

        return new DirectoryProbeResult(true, false, manifest, alc, asm, pluginType);
    }

    public void RegisterDynamicMenuItem(string pluginId, PluginMenuItem item)
    {
        var plugin = LoadedPlugins.FirstOrDefault(p =>
            string.Equals(p.Plugin.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
        if (plugin?.HostSession != null)
        {
            RegisterDynamicMenuItem(plugin.HostSession, item);
        }
    }

    public void RegisterDynamicMenuItem(PluginHostSession session, PluginMenuItem item)
    {
        RunOnUI(() =>
        {
            // Validate that the originating host session is still the active session for this plugin!
            if (!session.IsActive || item == null)
            {
                return;
            }

            var currentPlugin = LoadedPlugins.FirstOrDefault(p =>
                string.Equals(p.Plugin.PluginId, session.PluginId, StringComparison.OrdinalIgnoreCase));

            if (currentPlugin == null || !ReferenceEquals(currentPlugin.HostSession, session))
            {
                // The originating session is retired, failed, or replaced by a new instance
                return;
            }

            var group = MenuGroups.FirstOrDefault(g => g.PluginId.Equals(session.PluginId, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                group = new PluginMenuGroup
                {
                    PluginId = session.PluginId,
                    Header = session.PluginId
                };
                MenuGroups.Add(group);
            }

            if (!group.Items.Any(i => string.Equals(i.CommandId, item.Id, StringComparison.OrdinalIgnoreCase)))
            {
                group.Items.Add(new PluginMenuCommandViewModel
                {
                    PluginId = session.PluginId,
                    CommandId = item.Id,
                    Header = item.Header,
                    Shortcut = item.Shortcut,
                    SeparatorBefore = item.SeparatorBefore,
                    Command = new RelayCommand(() => ExecutePluginCommand(session.PluginId, item.Id))
                });
            }
        });
    }

    public void ExecutePluginCommand(string pluginId, string commandId)
    {
        var pluginInfo = LoadedPlugins.FirstOrDefault(p =>
            p.Plugin.PluginId.Equals(pluginId, StringComparison.OrdinalIgnoreCase));

        if (pluginInfo == null)
        {
            LogWarn($"ExecutePluginCommand: plugin '{pluginId}' not found.");
            return;
        }

        var context = _host?.GetActiveContext() ?? new PluginLogContext(null);
        try
        {
            var priorWindows = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().ToArray() ?? Array.Empty<System.Windows.Window>();

            pluginInfo.Plugin.ExecuteCommand(commandId, context);

            // Automatically align newly launched plugin window with LogTailPro main window
            if (System.Windows.Application.Current?.MainWindow is { } mainWin)
            {
                System.Windows.Application.Current.Dispatcher?.InvokeAsync(() =>
                {
                    try
                    {
                        var currentWindows = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().ToArray() ?? Array.Empty<System.Windows.Window>();
                        foreach (System.Windows.Window win in currentWindows)
                        {
                            if (ReferenceEquals(win, mainWin) || priorWindows.Contains(win)) continue;
                            AlignPluginWindow(win, mainWin);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PluginLoader] Error aligning windows: {ex.Message}");
                    }
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
        catch (Exception ex)
        {
            var msg = $"Plugin Error ({pluginInfo.Plugin.PluginName} / {commandId}): {ex.Message}";
            LogError(msg);
            _host?.SetStatusMessage(msg);
        }
    }

    private static void AlignPluginWindow(System.Windows.Window win, System.Windows.Window mainWin)
    {
        try
        {
            if (win.Owner == null && !ReferenceEquals(win, mainWin) && mainWin.IsVisible)
            {
                try { win.Owner = mainWin; } catch { /* Ignore if owner cannot be set */ }
            }

            if (mainWin.WindowState == System.Windows.WindowState.Maximized)
            {
                win.WindowState = System.Windows.WindowState.Maximized;
            }
            else if (mainWin.WindowState == System.Windows.WindowState.Normal)
            {
                double targetWidth = mainWin.ActualWidth > 0 ? mainWin.ActualWidth : mainWin.Width;
                double targetHeight = mainWin.ActualHeight > 0 ? mainWin.ActualHeight : mainWin.Height;

                if (double.IsNaN(targetWidth) || targetWidth <= 0) targetWidth = 1200;
                if (double.IsNaN(targetHeight) || targetHeight <= 0) targetHeight = 800;

                targetWidth = Math.Max(win.MinWidth > 0 ? win.MinWidth : 800, targetWidth);
                targetHeight = Math.Max(win.MinHeight > 0 ? win.MinHeight : 600, targetHeight);

                win.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                if (!double.IsNaN(mainWin.Left) && !double.IsInfinity(mainWin.Left))
                {
                    win.Left = mainWin.Left;
                }
                if (!double.IsNaN(mainWin.Top) && !double.IsInfinity(mainWin.Top))
                {
                    win.Top = mainWin.Top;
                }
                win.Width = targetWidth;
                win.Height = targetHeight;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginLoader] Could not align plugin window: {ex.Message}");
        }
    }

    public void OpenPluginsFolder()
    {
        string path = Directory.Exists(LocalPluginsPath) ? LocalPluginsPath : UserPluginsPath;
        EnsureDirectory(path);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LogWarn($"Could not open plugins folder: {ex.Message}");
        }
    }

    // ── FindPluginType: narrow exceptions so legit errors are surfaced ────────
    private Type? FindPluginType(Assembly asm, string sourceDir)
    {
        try
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            return types.FirstOrDefault(t =>
                typeof(ILogTailPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
        }
        catch (FileNotFoundException ex)
        {
            LogWarn($"[{Path.GetFileName(sourceDir)}] Missing dependency: {ex.FileName}");
            return null;
        }
        catch (BadImageFormatException ex)
        {
            LogWarn($"[{Path.GetFileName(sourceDir)}] Bad image format: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            LogWarn($"[{Path.GetFileName(sourceDir)}] GetTypes() error: {ex.Message}");
            return null;
        }
    }

    private static void EnsureDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
        catch { /* Ignore creation errors */ }
    }

    // ── Internal logging helpers ─────────────────────────────────────────────
    private void LogInfo(string message)
    {
        Debug.WriteLine($"[PluginLoader] {message}");
        RunOnUI(() => LoadLog.Add($"[INFO]  {message}"));
    }

    private void LogWarn(string message)
    {
        Debug.WriteLine($"[PluginLoader][WARN] {message}");
        RunOnUI(() => LoadLog.Add($"[WARN]  {message}"));
    }

    private void LogError(string message)
    {
        Debug.WriteLine($"[PluginLoader][ERROR] {message}");
        RunOnUI(() => LoadLog.Add($"[ERROR] {message}"));
    }
}

public class LoadedPluginInfo
{
    public ILogTailPlugin? Plugin { get; private set; }
    public PluginManifest Manifest { get; }
    public string DirectoryPath { get; }
    public Assembly? Assembly { get; private set; }
    public PluginHostSession? HostSession { get; private set; }

    public string Name => !string.IsNullOrWhiteSpace(Manifest?.Name) ? Manifest.Name : (Plugin?.PluginName ?? "(Unnamed Plugin)");
    public string Version => !string.IsNullOrWhiteSpace(Manifest?.Version) ? Manifest.Version : (Plugin?.Version ?? "1.0.0");
    public string Author => !string.IsNullOrWhiteSpace(Manifest?.Author) ? Manifest.Author : "Unknown";
    public string Description => Manifest?.Description ?? string.Empty;

    public LoadedPluginInfo(ILogTailPlugin plugin, PluginManifest manifest, string directoryPath, Assembly assembly, PluginHostSession? hostSession = null)
    {
        Plugin = plugin;
        Manifest = manifest;
        DirectoryPath = directoryPath;
        Assembly = assembly;
        HostSession = hostSession;
    }

    public void ReleaseReferences()
    {
        Plugin = null;
        Assembly = null;
        HostSession?.Deactivate();
        HostSession = null;
    }
}

public class PluginMenuGroup
{
    public string PluginId { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public ObservableCollection<PluginMenuCommandViewModel> Items { get; } = new();
}

public class PluginMenuCommandViewModel
{
    public string PluginId { get; set; } = string.Empty;
    public string CommandId { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string? Shortcut { get; set; }
    public bool SeparatorBefore { get; set; }
    public IRelayCommand? Command { get; set; }
}

/// <summary>
/// Isolated AssemblyLoadContext for loading plugin assemblies and their dependencies.
/// isCollectible=true so the ALC and all loaded assemblies can be GC'd after Unload().
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Prefer host's PluginContract so interface types match identically
        if (assemblyName.Name == "LogTailPro.PluginContract")
        {
            return typeof(ILogTailPlugin).Assembly;
        }

        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
