using System;
using System.IO;
using System.Windows;
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

                MessageBox.Show(
                    $"Added plugin folder:\n{folder}\n\nTotal loaded plugins: {_mainViewModel.PluginLoaderService.LoadedPlugins.Count}",
                    "Plugin Folder Added",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
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
