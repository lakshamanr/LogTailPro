using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogTailPro.PluginContract;
using Newtonsoft.Json;

namespace LogTailPro.Services;

public sealed partial class PluginLoaderService : ObservableObject
{
    private IPluginHost? _host;
    private readonly List<LoadedPluginInfo> _loadedPlugins = new();

    public List<string> CustomPluginDirectories { get; set; } = new();

    public ObservableCollection<PluginMenuGroup> MenuGroups { get; } = new();
    public IReadOnlyList<LoadedPluginInfo> LoadedPlugins => _loadedPlugins;

    public static string LocalPluginsDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

    public static string UserPluginsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogTailPro", "plugins");

    public async Task InitializeAndLoadAsync(IPluginHost host)
    {
        _host = host;
        await ReloadPluginsAsync();
    }

    public async Task ReloadPluginsAsync()
    {
        // 1. Shutdown existing plugins
        foreach (var p in _loadedPlugins)
        {
            try { p.Plugin.Shutdown(); } catch { /* Ignore shutdown errors */ }
        }
        _loadedPlugins.Clear();
        MenuGroups.Clear();

        // 2. Ensure default discovery directories exist
        EnsureDirectory(LocalPluginsDirectory);
        EnsureDirectory(UserPluginsDirectory);

        // 3. Scan directories (Priority: Custom user directories > User AppData > App Local)
        var candidateDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // A. Custom Directories (e.g. user added Desktop\Testing Plusing)
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

        // B. User AppData (%LOCALAPPDATA%\LogTailPro\plugins)
        if (Directory.Exists(UserPluginsDirectory))
        {
            foreach (var sub in Directory.GetDirectories(UserPluginsDirectory))
            {
                candidateDirs.Add(sub);
            }
        }

        // C. Local App Dir (<AppDir>\plugins)
        if (Directory.Exists(LocalPluginsDirectory))
        {
            foreach (var sub in Directory.GetDirectories(LocalPluginsDirectory))
            {
                candidateDirs.Add(sub);
            }
        }

        // 4. Load each discovered plugin with deduplication
        var loadedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in candidateDirs)
        {
            try
            {
                await LoadPluginFromDirectoryAsync(dir, loadedIds);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginLoader] Error loading plugin from '{dir}': {ex.Message}");
            }
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
                Debug.WriteLine($"[PluginLoader] Failed to parse manifest at '{manifestPath}': {ex.Message}");
            }
        }

        if (manifest != null && !string.IsNullOrWhiteSpace(manifest.Id) && loadedIds.Contains(manifest.Id))
        {
            return;
        }

        // Find assembly file
        string? assemblyPath = null;
        if (manifest != null && !string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            assemblyPath = Path.Combine(dir, manifest.EntryAssembly);
        }
        else
        {
            // Auto-detect first .dll with plugin implementation
            var dlls = Directory.GetFiles(dir, "*.dll");
            assemblyPath = dlls.FirstOrDefault();
        }

        if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
        {
            return;
        }

        // Load assembly using custom AssemblyLoadContext
        var alc = new PluginLoadContext(assemblyPath);
        var asm = alc.LoadFromAssemblyPath(assemblyPath);

        Type? pluginType = null;
        if (manifest != null && !string.IsNullOrWhiteSpace(manifest.EntryClass))
        {
            pluginType = asm.GetType(manifest.EntryClass);
        }

        if (pluginType == null)
        {
            pluginType = asm.GetTypes().FirstOrDefault(t => 
                typeof(ILogTailPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
        }

        if (pluginType == null)
        {
            return;
        }

        if (Activator.CreateInstance(pluginType) is not ILogTailPlugin plugin)
        {
            return;
        }

        if (loadedIds.Contains(plugin.PluginId))
        {
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

        if (_host != null)
        {
            await plugin.InitializeAsync(_host);
        }

        var pluginInfo = new LoadedPluginInfo(plugin, manifest, dir, asm);
        _loadedPlugins.Add(pluginInfo);

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

        MenuGroups.Add(group);
    }

    public void RegisterDynamicMenuItem(string pluginId, PluginMenuItem item)
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
    }

    public void ExecutePluginCommand(string pluginId, string commandId)
    {
        var pluginInfo = _loadedPlugins.FirstOrDefault(p => 
            p.Plugin.PluginId.Equals(pluginId, StringComparison.OrdinalIgnoreCase));

        if (pluginInfo == null) return;

        var context = _host?.GetActiveContext() ?? new PluginLogContext(null);
        try
        {
            pluginInfo.Plugin.ExecuteCommand(commandId, context);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginLoader] Error executing '{commandId}' on plugin '{pluginId}': {ex.Message}");
            _host?.SetStatusMessage($"Plugin Error ({pluginInfo.Plugin.PluginName}): {ex.Message}");
        }
    }

    public void OpenPluginsFolder()
    {
        string path = Directory.Exists(LocalPluginsDirectory) ? LocalPluginsDirectory : UserPluginsDirectory;
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
            Debug.WriteLine($"[PluginLoader] Could not open plugins folder: {ex.Message}");
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
}

public class LoadedPluginInfo
{
    public ILogTailPlugin Plugin { get; }
    public PluginManifest Manifest { get; }
    public string DirectoryPath { get; }
    public Assembly Assembly { get; }

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
        // Prefer host's PluginContract assembly so interface types match identically
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
