using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LogTailPro.ViewModels;

namespace LogTailPro.Services;

public record AddPluginFolderResult(
    string NormalizedPath,
    IReadOnlyList<LoadedPluginInfo> LoadedFromFolder,
    int TotalLoadedCount);

public record RemovePluginResult(bool Success, string? ErrorMessage, bool RestartRequired = false)
{
    public static RemovePluginResult Succeeded() => new(true, null, false);
    public static RemovePluginResult Failed(string error, bool restartRequired = false) => new(false, error, restartRequired);
}

public static class PluginManagementService
{
    public static bool IsPathUnder(string childPath, string parentPath)
    {
        string normChild = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normChild.StartsWith(normParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPathUnderOrEqual(string path1, string path2)
    {
        string norm1 = Path.GetFullPath(path1).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string norm2 = Path.GetFullPath(path2).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase) ||
               norm1.StartsWith(norm2 + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<AddPluginFolderResult> AddPluginFolderAsync(MainViewModel mainViewModel, string folderPath)
    {
        string normalized = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        bool alreadyConfigured = mainViewModel.Settings.CustomPluginDirectories.Any(dir =>
            string.Equals(
                Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                normalized,
                StringComparison.OrdinalIgnoreCase));

        if (!alreadyConfigured)
        {
            mainViewModel.Settings.CustomPluginDirectories.Add(normalized);
            mainViewModel.SaveSettings();
        }

        // If folder was previously in ExcludedPluginDirectories, un-exclude it upon explicit add
        var toUnexclude = mainViewModel.Settings.ExcludedPluginDirectories
            .Where(e => IsPathUnderOrEqual(e, normalized) || IsPathUnderOrEqual(normalized, e))
            .ToList();
        if (toUnexclude.Count > 0)
        {
            foreach (var ex in toUnexclude)
            {
                mainViewModel.Settings.ExcludedPluginDirectories.Remove(ex);
            }
            mainViewModel.SaveSettings();
        }

        mainViewModel.PluginLoaderService.CustomPluginDirectories = mainViewModel.Settings.CustomPluginDirectories;
        mainViewModel.PluginLoaderService.ExcludedPluginDirectories = mainViewModel.Settings.ExcludedPluginDirectories;
        await mainViewModel.PluginLoaderService.ReloadPluginsAsync();

        var loadedFromFolder = mainViewModel.PluginLoaderService.LoadedPlugins
            .Where(p => IsPathUnderOrEqual(p.DirectoryPath, normalized))
            .ToList();

        return new AddPluginFolderResult(
            normalized,
            loadedFromFolder,
            mainViewModel.PluginLoaderService.LoadedPlugins.Count);
    }

    public static async Task<RemovePluginResult> RemovePluginAsync(MainViewModel mainViewModel, LoadedPluginInfo info)
    {
        return await mainViewModel.PluginLoaderService.ExecuteWithReloadLockAsync(async () =>
        {
            string pluginId = info.Plugin?.PluginId ?? info.Manifest?.Id ?? string.Empty;
            string pluginName = info.Name;
            string canonicalPluginDir = Path.GetFullPath(info.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string canonicalLocalDir = Path.GetFullPath(mainViewModel.PluginLoaderService.LocalPluginsPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string canonicalUserDir = Path.GetFullPath(mainViewModel.PluginLoaderService.UserPluginsPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            bool isManagedSubdir =
                (IsPathUnder(canonicalPluginDir, canonicalLocalDir) && canonicalPluginDir.Length > canonicalLocalDir.Length) ||
                (IsPathUnder(canonicalPluginDir, canonicalUserDir) && canonicalPluginDir.Length > canonicalUserDir.Length);

            // Step 1: Retire the selected plugin first and release host-owned references
            await mainViewModel.PluginLoaderService.RetirePluginAsync(info);

            // Step 2: Handle directory removal or exclusion
            if (isManagedSubdir)
            {
                // Managed plugin directory (%LOCALAPPDATA%\LogTailPro\plugins\<id> or <appBase>\plugins\<id>)
                // Execute GC and deletion on a background thread (avoid forced GC on WPF dispatcher)
                bool deleteSucceeded = false;
                string? deleteError = null;

                await Task.Run(() =>
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    if (Directory.Exists(canonicalPluginDir))
                    {
                        try
                        {
                            Directory.Delete(canonicalPluginDir, recursive: true);
                            deleteSucceeded = !Directory.Exists(canonicalPluginDir);
                        }
                        catch (Exception ex)
                        {
                            deleteError = ex.Message;
                            deleteSucceeded = false;
                        }
                    }
                    else
                    {
                        deleteSucceeded = true;
                    }
                });

                if (!deleteSucceeded)
                {
                    if (!mainViewModel.Settings.ExcludedPluginDirectories.Any(e =>
                        string.Equals(Path.GetFullPath(e).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), canonicalPluginDir, StringComparison.OrdinalIgnoreCase)))
                    {
                        mainViewModel.Settings.ExcludedPluginDirectories.Add(canonicalPluginDir);
                    }

                    // Report actual outcome and whether restart is needed. A failed deletion NEVER reports success.
                    return RemovePluginResult.Failed(
                        $"Could not delete plugin files at '{canonicalPluginDir}': {deleteError}. " +
                        "The extension was retired from the current session, but Windows is still locking the files. " +
                        "A restart of the application is required to complete removal.",
                        restartRequired: true);
                }
            }
            else
            {
                // External custom directory: preserve external directories and files on disk.
                var customDirs = mainViewModel.Settings.CustomPluginDirectories.ToList();
                bool modified = false;

                foreach (var custom in customDirs)
                {
                    string normCustom = Path.GetFullPath(custom).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (string.Equals(normCustom, canonicalPluginDir, StringComparison.OrdinalIgnoreCase))
                    {
                        // Direct custom folder registration: remove this directory from custom list
                        mainViewModel.Settings.CustomPluginDirectories.Remove(custom);
                        modified = true;
                    }
                    else if (IsPathUnder(canonicalPluginDir, normCustom))
                    {
                        // Selected plugin belongs to a shared discovery root:
                        // Keep the root registered and persist an exclusion for the selected plugin!
                        if (!mainViewModel.Settings.ExcludedPluginDirectories.Any(e =>
                            string.Equals(Path.GetFullPath(e).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), canonicalPluginDir, StringComparison.OrdinalIgnoreCase)))
                        {
                            mainViewModel.Settings.ExcludedPluginDirectories.Add(canonicalPluginDir);
                            modified = true;
                        }
                    }
                }

                if (modified)
                {
                    mainViewModel.SaveSettings();
                }
            }

            // Step 3: Reload plugins while holding the reload lock so discovery updates immediately
            mainViewModel.PluginLoaderService.CustomPluginDirectories = mainViewModel.Settings.CustomPluginDirectories;
            mainViewModel.PluginLoaderService.ExcludedPluginDirectories = mainViewModel.Settings.ExcludedPluginDirectories;
            await mainViewModel.PluginLoaderService.ReloadInternalAsync();

            // Step 4: Verify actual removal before reporting success
            bool stillLoaded = !string.IsNullOrWhiteSpace(pluginId) && mainViewModel.PluginLoaderService.LoadedPlugins
                .Any(p => string.Equals(p.Plugin?.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(p.Manifest?.Id, pluginId, StringComparison.OrdinalIgnoreCase));

            if (stillLoaded)
            {
                return RemovePluginResult.Failed($"Extension '{pluginName}' is still loaded after removal attempt. Please verify that the plugin is not duplicated in another directory: {info.DirectoryPath}");
            }

            return RemovePluginResult.Succeeded();
        });
    }
}
