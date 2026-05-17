using System.IO;
using System.Windows;
using LogTailPro.Services;
using LogTailPro.ViewModels;

namespace LogTailPro;

/// <summary>Application entry point — wires up services and creates the main window.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SettingsService settingsService = new();
        MainViewModel mainVm = new(settingsService);

        // Apply the persisted theme before showing the window
        string theme = mainVm.Settings.Theme;
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri($"Themes/{theme}.xaml", UriKind.Relative)
        });

        MainWindow window = new() { DataContext = mainVm };

        // Restore window position from settings
        if (!double.IsNaN(mainVm.Settings.WindowTop))  window.Top  = mainVm.Settings.WindowTop;
        if (!double.IsNaN(mainVm.Settings.WindowLeft)) window.Left = mainVm.Settings.WindowLeft;
        if (mainVm.Settings.WindowMaximized)
            window.WindowState = System.Windows.WindowState.Maximized;

        window.Show();

        // Handle all file paths passed as command-line arguments
        foreach (string arg in e.Args)
        {
            if (File.Exists(arg))
                _ = mainVm.OpenFileTabAsync(arg);
        }
    }
}
