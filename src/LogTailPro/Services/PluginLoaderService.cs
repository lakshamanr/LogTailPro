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

    public ObservableCollection<PluginMenuGroup> MenuGroups { get; } = new();
    public ObservableCollection<LoadedPluginInfo> LoadedPlugins { get; } = new();

    // Track all ALC instances so we can Unload() them on reload (prevents assembly leak)
    private readonly List<PluginLoadContext> _loadedContexts = new();

    // ── Plugin init timeout ──────────────────────────────────────────────────
    private static readonly TimeSpan PluginInitTimeout = TimeSpan.FromSeconds(10);

    private static void RunOnUI(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public static string LocalPluginsDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

    public static string UserPluginsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogTailPro", "plugins");

    public async Task InitializeAndLoadAsync(IPluginHost host)
    {
        _host = host;
        await ReloadPluginsAsync();
    }

    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    public async Task ReloadPluginsAsync()
    {
        await _reloadLock.WaitAsync();
        try
        {
            LogInfo("── Plugin reload started ──────────────────────────────────────");

            // 1. Shutdown existing plugins gracefully
            foreach (var p in LoadedPlugins)
            {
                try
                {
                    p.Plugin.Shutdown();
                }
                catch (Exception ex)
                {
                    LogWarn($"Shutdown error for '{p.Name}': {ex.Message}");
                }
            }

            // 2. Unload isolated ALCs to free memory (assembly leak prevention)
            foreach (var alc in _loadedContexts)
            {
                try { alc.Unload(); } catch { /* best-effort */ }
            }
            _loadedContexts.Clear();

            RunOnUI(() =>
            {
                LoadedPlugins.Clear();
                MenuGroups.Clear();
                LoadLog.Clear();
                LogInfo("── Plugin reload started ──────────────────────────────────────");
            });

            // 3. Ensure default discovery directories exist
            EnsureDirectory(LocalPluginsDirectory);
            EnsureDirectory(UserPluginsDirectory);

            // 4. Scan directories (Priority: Custom > User AppData > App Local)
            var candidateDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var custom in CustomPluginDirectories)
            {
                if (Directory.Exists(custom))
                {
                    if (File.Exists(Path.Combine(custom, "plugin.json")) || Directory.GetFiles(custom, "*.dll").Length > 0)
                    {
                        candidateDirs.Add(custom);
                    }
                    foreach (var sub in Directory.GetDirectories(custom))
                    {
                        candidateDirs.Add(sub);
                    }
                }
            }

            if (Directory.Exists(UserPluginsDirectory))
            {
                foreach (var sub in Directory.GetDirectories(UserPluginsDirectory))
                {
                    candidateDirs.Add(sub);
                }
            }

            if (Directory.Exists(LocalPluginsDirectory))
            {
                foreach (var sub in Directory.GetDirectories(LocalPluginsDirectory))
                {
                    candidateDirs.Add(sub);
                }
            }

            LogInfo($"Discovered {candidateDirs.Count} candidate director{(candidateDirs.Count == 1 ? "y" : "ies")}.");

            // 5. Load each discovered plugin with deduplication
            var loadedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in candidateDirs)
            {
                try
                {
                    await LoadPluginFromDirectoryAsync(dir, loadedIds);
                }
                catch (Exception ex)
                {
                    // Catch-all so one bad plugin never crashes the entire reload
                    LogError($"Unexpected error loading plugin from '{Path.GetFileName(dir)}': {ex.Message}");
                }
            }

            LogInfo($"── Reload complete: {LoadedPlugins.Count} plugin(s) loaded ──────────────");
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private async Task LoadPluginFromDirectoryAsync(string dir, HashSet<string> loadedIds)
    {
        string manifestPath = Path.Combine(dir, "plugin.json");
        PluginManifest? manifest = null;

        if (File.Exists(manifestPath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(manifestPath);
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
            return;
        }

        // ── Version gate ─────────────────────────────────────────────────────
        if (manifest != null && !string.IsNullOrWhiteSpace(manifest.MinLogTailProVersion))
        {
            if (_host != null &&
                Version.TryParse(manifest.MinLogTailProVersion, out var required) &&
                Version.TryParse(_host.LogTailProVersion?.Split('+')[0], out var current) &&
                current < required)
            {
                LogWarn($"[{manifest.Id ?? Path.GetFileName(dir)}] Requires LogTailPro >= {manifest.MinLogTailProVersion} (current: {_host.LogTailProVersion}). Skipped.");
                return;
            }
        }

        // Find assembly file
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
                    LogError($"[{Path.GetFileName(dir)}] Not a valid .NET assembly '{manifest.EntryAssembly}': {ex.Message}");
                    return;
                }
                catch (FileNotFoundException ex)
                {
                    LogError($"[{Path.GetFileName(dir)}] Missing dependency for '{manifest.EntryAssembly}': {ex.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    LogError($"[{Path.GetFileName(dir)}] Failed loading entry assembly '{manifest.EntryAssembly}': {ex.Message}");
                    return;
                }
            }
            else
            {
                LogError($"[{Path.GetFileName(dir)}] Entry assembly '{manifest.EntryAssembly}' not found in directory.");
                return;
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
                        // Not a plugin DLL — unload its ALC immediately
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
            return;
        }

        // Track ALC for unload on next reload
        if (alc != null)
        {
            _loadedContexts.Add(alc);
        }

        // Instantiate
        ILogTailPlugin plugin;
        try
        {
            var instance = Activator.CreateInstance(pluginType);
            if (instance is not ILogTailPlugin p)
            {
                LogError($"[{Path.GetFileName(dir)}] Created instance does not implement ILogTailPlugin.");
                return;
            }
            plugin = p;
        }
        catch (Exception ex)
        {
            LogError($"[{Path.GetFileName(dir)}] Activator.CreateInstance failed: {ex.InnerException?.Message ?? ex.Message}");
            return;
        }

        // Dedup by plugin runtime ID
        if (loadedIds.Contains(plugin.PluginId))
        {
            LogInfo($"[{plugin.PluginId}] Skipped (duplicate plugin ID).");
            return;
        }
        loadedIds.Add(plugin.PluginId);
        if (manifest != null && !string.IsNullOrWhiteSpace(manifest.Id))
        {
            loadedIds.Add(manifest.Id);
        }

        // Default manifest if not provided
        manifest ??= new PluginManifest
        {
            Id = plugin.PluginId,
            Name = plugin.PluginName,
            Version = plugin.Version,
            EntryAssembly = Path.GetFileName(assemblyPath),
            Menu = new PluginMenuDefinition
            {
                Header = plugin.PluginName,
                Items = new List<PluginMenuItem>
                {
                    new() { Id = "default", Header = $"Open {plugin.PluginName}" }
                }
            }
        };

        // ── InitializeAsync with timeout ─────────────────────────────────────
        if (_host != null)
        {
            using var cts = new CancellationTokenSource(PluginInitTimeout);
            try
            {
                await plugin.InitializeAsync(_host).WaitAsync(cts.Token);
                LogInfo($"[{plugin.PluginId}] Initialized successfully (v{plugin.Version}).");
            }
            catch (OperationCanceledException)
            {
                LogError($"[{plugin.PluginId}] InitializeAsync timed out after {PluginInitTimeout.TotalSeconds}s. Plugin skipped.");
                try { plugin.Shutdown(); } catch { }
                return;
            }
            catch (Exception ex)
            {
                // Plugin init failed — log and skip; don't crash the reload
                LogError($"[{plugin.PluginId}] InitializeAsync threw: {ex.Message}");
                try { plugin.Shutdown(); } catch { }
                return;
            }
        }

        var pluginInfo = new LoadedPluginInfo(plugin, manifest, dir, asm);

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

        RunOnUI(() =>
        {
            LoadedPlugins.Add(pluginInfo);
            MenuGroups.Add(group);
        });
    }

    public void RegisterDynamicMenuItem(string pluginId, PluginMenuItem item)
    {
        RunOnUI(() =>
        {
            var group = MenuGroups.FirstOrDefault(g => g.PluginId.Equals(pluginId, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                group = new PluginMenuGroup
                {
                    PluginId = pluginId,
                    Header = pluginId
                };
                MenuGroups.Add(group);
            }

            group.Items.Add(new PluginMenuCommandViewModel
            {
                PluginId = pluginId,
                CommandId = item.Id,
                Header = item.Header,
                Shortcut = item.Shortcut,
                SeparatorBefore = item.SeparatorBefore,
                Command = new RelayCommand(() => ExecutePluginCommand(pluginId, item.Id))
            });
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
            var priorWindows = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().ToList() ?? new();

            pluginInfo.Plugin.ExecuteCommand(commandId, context);

            // Automatically align newly launched plugin window with LogTailPro main window
            if (System.Windows.Application.Current?.MainWindow is { } mainWin)
            {
                System.Windows.Application.Current.Dispatcher?.InvokeAsync(() =>
                {
                    foreach (System.Windows.Window win in System.Windows.Application.Current.Windows)
                    {
                        if (ReferenceEquals(win, mainWin) || priorWindows.Contains(win)) continue;
                        AlignPluginWindow(win, mainWin);
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
                double targetWidth = Math.Max(win.MinWidth > 0 ? win.MinWidth : 800, mainWin.ActualWidth > 0 ? mainWin.ActualWidth : mainWin.Width);
                double targetHeight = Math.Max(win.MinHeight > 0 ? win.MinHeight : 600, mainWin.ActualHeight > 0 ? mainWin.ActualHeight : mainWin.Height);

                win.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                win.Left = mainWin.Left;
                win.Top = mainWin.Top;
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
        string path = Directory.Exists(LocalPluginsDirectory) ? LocalPluginsDirectory : UserPluginsDirectory;
        EnsureDirectory(path);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
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
    public ILogTailPlugin Plugin { get; }
    public PluginManifest Manifest { get; }
    public string DirectoryPath { get; }
    public Assembly Assembly { get; }

    public string Name => !string.IsNullOrWhiteSpace(Manifest?.Name) ? Manifest.Name : (Plugin?.PluginName ?? "(Unnamed Plugin)");
    public string Version => !string.IsNullOrWhiteSpace(Manifest?.Version) ? Manifest.Version : (Plugin?.Version ?? "1.0.0");
    public string Author => !string.IsNullOrWhiteSpace(Manifest?.Author) ? Manifest.Author : "Unknown";
    public string Description => Manifest?.Description ?? string.Empty;

    public LoadedPluginInfo(ILogTailPlugin plugin, PluginManifest manifest, string directoryPath, Assembly assembly)
    {
        Plugin = plugin;
        Manifest = manifest;
        DirectoryPath = directoryPath;
        Assembly = assembly;
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
