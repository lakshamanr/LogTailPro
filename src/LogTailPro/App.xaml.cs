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
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"Themes/{theme}.xaml", UriKind.Relative)
        });

        MainWindow window = new() { DataContext = mainVm };

        // Restore window position from settings (unless CLI overrides it below)
        if (!double.IsNaN(mainVm.Settings.WindowTop))  window.Top  = mainVm.Settings.WindowTop;
        if (!double.IsNaN(mainVm.Settings.WindowLeft)) window.Left = mainVm.Settings.WindowLeft;
        if (mainVm.Settings.WindowMaximized)
            window.WindowState = WindowState.Maximized;

        // Parse CLI arguments: -wp/-ws/-tc/-ti flags + file paths
        ParseCliArgs(e.Args, window, mainVm, out List<string> filePaths);

        window.Show();

        // Open files specified on the command line
        foreach (string path in filePaths)
        {
            if (File.Exists(path))
                _ = mainVm.OpenFileTabAsync(path);
        }

        // Auto-open last file when no CLI files were given and setting is enabled
        if (filePaths.Count == 0
            && mainVm.Settings.AutomaticallyOpenLastFileOnStartup
            && mainVm.Settings.RecentFiles.Count > 0)
        {
            string lastFile = mainVm.Settings.RecentFiles[0];
            if (File.Exists(lastFile))
                _ = mainVm.OpenFileTabAsync(lastFile);
        }
    }

    private static void ParseCliArgs(
        string[] args, MainWindow window, MainViewModel mainVm,
        out List<string> filePaths)
    {
        filePaths = new List<string>();
        int tileCount = 0;
        int tileIndex = 0;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            // -wp / --window-position left top width height
            if ((arg == "-wp" || arg == "--window-position") && i + 4 < args.Length)
            {
                if (double.TryParse(args[i + 1], out double left)  &&
                    double.TryParse(args[i + 2], out double top)   &&
                    double.TryParse(args[i + 3], out double width) &&
                    double.TryParse(args[i + 4], out double height))
                {
                    window.Left   = left;
                    window.Top    = top;
                    window.Width  = width;
                    window.Height = height;
                    window.WindowState = WindowState.Normal;
                }
                i += 4;
                continue;
            }

            // -ws / --window-state  0=Normal 1=Minimized 2=Maximized
            if ((arg == "-ws" || arg == "--window-state") && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int state))
                    window.WindowState = state switch
                    {
                        1 => WindowState.Minimized,
                        2 => WindowState.Maximized,
                        _ => WindowState.Normal
                    };
                i++;
                continue;
            }

            // -tc / --tile-window-count N
            if ((arg == "-tc" || arg == "--tile-window-count") && i + 1 < args.Length)
            {
                int.TryParse(args[i + 1], out tileCount);
                i++;
                continue;
            }

            // -ti / --tile-window-index N (0-based)
            if ((arg == "-ti" || arg == "--tile-window-index") && i + 1 < args.Length)
            {
                int.TryParse(args[i + 1], out tileIndex);
                i++;
                continue;
            }

            // Anything else that isn't a switch: treat as file path
            if (!arg.StartsWith('-'))
                filePaths.Add(arg);
        }

        // Apply window tiling if -tc/-ti were both specified
        if (tileCount > 1)
            ApplyTiling(window, tileCount, tileIndex);
    }

    private static void ApplyTiling(MainWindow window, int count, int index)
    {
        Rect area = SystemParameters.WorkArea;
        int cols = count <= 2 ? count : (int)Math.Ceiling(Math.Sqrt(count));
        int rows = (int)Math.Ceiling((double)count / cols);

        int col = index % cols;
        int row = index / cols;

        double tileW = area.Width  / cols;
        double tileH = area.Height / rows;

        window.Left   = area.Left + col * tileW;
        window.Top    = area.Top  + row * tileH;
        window.Width  = tileW;
        window.Height = tileH;
        window.WindowState = WindowState.Normal;
    }
}
