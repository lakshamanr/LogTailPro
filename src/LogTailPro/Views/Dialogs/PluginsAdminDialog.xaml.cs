using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
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
                var result = await PluginManagementService.AddPluginFolderAsync(_mainViewModel, dialog.FolderName);

                if (result.LoadedFromFolder.Count > 0)
                {
                    string pluginList = string.Join("\n", result.LoadedFromFolder.Select(p => $"• {p.Name} (v{p.Version})"));
                    MessageBox.Show(
                        $"Added plugin folder:\n{result.NormalizedPath}\n\nSuccessfully loaded {result.LoadedFromFolder.Count} plugin(s) from this folder:\n{pluginList}",
                        "Plugin Folder Added",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Plugin directory was added:\n{result.NormalizedPath}\n\nHowever, 0 plugins were loaded from this location.\n\nPlease ensure:\n" +
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

            var result = await PluginManagementService.RemovePluginAsync(_mainViewModel, info);

            if (!result.Success)
            {
                string title = result.RestartRequired ? "Restart Required" : "Removal Failed";
                MessageBoxImage icon = result.RestartRequired ? MessageBoxImage.Warning : MessageBoxImage.Error;
                MessageBox.Show(
                    result.ErrorMessage ?? "Could not remove extension.",
                    title,
                    MessageBoxButton.OK,
                    icon);
                return;
            }

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
