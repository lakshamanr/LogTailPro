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
        window.Show();

        // Handle file path passed as command-line argument
        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
            _ = mainVm.OpenFileTabAsync(e.Args[0]);
    }
}
