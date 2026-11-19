using System;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LogTailPro.Services;
using LogTailPro.ViewModels;
using Microsoft.Win32;

namespace LogTailPro.Views.Dialogs;

public partial class PluginsAdminDialog : Window
{
    private readonly MainViewModel _mainViewModel;

    public PluginsAdminDialog(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        DataContext = _mainViewModel.PluginLoaderService;

        // Auto-scroll load log to bottom when new entries arrive
        _mainViewModel.PluginLoaderService.LoadLog.CollectionChanged += LoadLog_CollectionChanged;
        Closed += (_, _) => _mainViewModel.PluginLoaderService.LoadLog.CollectionChanged -= LoadLog_CollectionChanged;

        PluginsListView.SelectionChanged += (_, _) =>
        {
            RemoveBtn.IsEnabled = PluginsListView.SelectedItem is LoadedPluginInfo;
        };
    }

    private void LoadLog_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && LoadLogList.Items.Count > 0)
        {
            LoadLogList.ScrollIntoView(LoadLogList.Items[^1]);
        }
    }


    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Plugin Directory (containing plugin.json or plugin .dll)"
            };

            if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                string folder = dialog.FolderName;
                if (!_mainViewModel.Settings.CustomPluginDirectories.Contains(folder, StringComparer.OrdinalIgnoreCase))
                {
                    _mainViewModel.Settings.CustomPluginDirectories.Add(folder);
                    _mainViewModel.SaveSettings();
                }

                _mainViewModel.PluginLoaderService.CustomPluginDirectories = _mainViewModel.Settings.CustomPluginDirectories;
                await _mainViewModel.PluginLoaderService.ReloadPluginsAsync();

                int loadedCount = _mainViewModel.PluginLoaderService.LoadedPlugins.Count;
                if (loadedCount > 0)
                {
                    MessageBox.Show(
                        $"Added plugin folder:\n{folder}\n\nTotal loaded plugins: {loadedCount}",
                        "Plugin Folder Added",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Plugin directory was added:\n{folder}\n\nHowever, 0 plugins were loaded from this location.\n\nPlease ensure:\n" +
                        "1. A 'plugin.json' manifest is present specifying 'entryAssembly', OR\n" +
                        "2. A compiled .dll implementing 'ILogTailPlugin' is located in the folder.\n" +
                        "3. The plugin assembly targets .NET 9 (net9.0-windows7.0).",
                        "No Plugins Detected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not add plugin directory: {ex.Message}",
                "Plugins Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _mainViewModel.PluginLoaderService.OpenPluginsFolder();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open plugins folder: {ex.Message}",
                "Plugins Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _mainViewModel.PluginLoaderService.CustomPluginDirectories = _mainViewModel.Settings.CustomPluginDirectories;
            await _mainViewModel.PluginLoaderService.ReloadPluginsAsync();
            MessageBox.Show(
                $"Reload complete.\nTotal loaded plugins: {_mainViewModel.PluginLoaderService.LoadedPlugins.Count}",
                "Plugins Reloaded",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not reload plugins: {ex.Message}",
                "Plugins Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void RemoveRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LoadedPluginInfo info })
        {
            await RemovePluginAsync(info);
        }
    }

    private async void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (PluginsListView.SelectedItem is LoadedPluginInfo info)
        {
            await RemovePluginAsync(info);
        }
    }

    private async System.Threading.Tasks.Task RemovePluginAsync(LoadedPluginInfo info)
    {
        try
        {
            var confirm = MessageBox.Show(
                $"Are you sure you want to remove the extension '{info.Name}'?\n\nDirectory:\n{info.DirectoryPath}",
                "Remove Extension",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            // 1. Check if the plugin is from a custom user directory
            var toRemove = _mainViewModel.Settings.CustomPluginDirectories
                .Where(custom => string.Equals(custom, info.DirectoryPath, StringComparison.OrdinalIgnoreCase) ||
                                 info.DirectoryPath.StartsWith(custom.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (toRemove.Count > 0)
            {
                foreach (var path in toRemove)
                {
                    _mainViewModel.Settings.CustomPluginDirectories.Remove(path);
                }
                _mainViewModel.SaveSettings();
            }
            else
            {
                // 2. Check if the plugin is in UserPluginsDirectory (%LOCALAPPDATA%\LogTailPro\plugins)
                string userDir = PluginLoaderService.UserPluginsDirectory;
                if (info.DirectoryPath.StartsWith(userDir, StringComparison.OrdinalIgnoreCase) && Directory.Exists(info.DirectoryPath))
                {
                    try
                    {
                        Directory.Delete(info.DirectoryPath, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PluginsAdmin] Could not delete plugin directory: {ex.Message}");
                    }
                }
            }

            // 3. Reload plugins
            _mainViewModel.PluginLoaderService.CustomPluginDirectories = _mainViewModel.Settings.CustomPluginDirectories;
            await _mainViewModel.PluginLoaderService.ReloadPluginsAsync();

            MessageBox.Show(
                $"Extension '{info.Name}' has been removed.",
                "Extension Removed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not remove extension: {ex.Message}",
                "Removal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
