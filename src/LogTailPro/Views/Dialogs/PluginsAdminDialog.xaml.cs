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

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
