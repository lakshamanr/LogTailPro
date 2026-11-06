using System.IO;
using System.Windows;
using LogTailPro.Services;
using LogTailPro.ViewModels;

namespace LogTailPro;

/// <summary>Application entry point — wires up services and creates the main window.</summary>
public partial class App : Application
{
    private SingleInstanceService? _singleInstance;

    /// <summary>Switches that position or tile the window: they ask for a separate window, so they skip single-instance.</summary>
    private static readonly HashSet<string> WindowPlacementSwitches = new(StringComparer.Ordinal)
    {
        "-wp", "--window-position", "-ws", "--window-state",
        "-tc", "--tile-window-count", "-ti", "--tile-window-index"
    };

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One instance at a time: a second launch (e.g. Explorer "Open with") hands its files to the
        // running window, which adds them as tabs, and exits. Explicit window placement/tiling switches
        // (-wp, -ws, -tc, -ti) opt out because they exist to run several windows side by side.
        if (!e.Args.Any(WindowPlacementSwitches.Contains))
        {
            _singleInstance = new SingleInstanceService();
            if (!_singleInstance.TryBecomePrimary())
            {
                bool forwarded = _singleInstance.TrySendToPrimary(e.Args);
                _singleInstance.Dispose();
                _singleInstance = null;

                if (forwarded)
                {
                    Shutdown();
                    return;
                }
                // The running instance could not be reached (hung or still starting): open normally
                // rather than silently dropping the user's file.
            }
        }

        SettingsService settingsService = new();
        MainViewModel mainVm = new(settingsService);

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

        // From now on, files forwarded by later launches arrive here.
        _singleInstance?.SetHandler(args =>
            Dispatcher.InvokeAsync(() => HandleForwardedArgs(args, window, mainVm)));
    }

    /// <summary>Opens files sent by another launch as new tabs and brings the window to the front.</summary>
    private static void HandleForwardedArgs(IReadOnlyList<string> args, MainWindow window, MainViewModel mainVm)
    {
        BringToFront(window);

        foreach (string path in args)
        {
            if (!path.StartsWith('-') && File.Exists(path))
                _ = mainVm.OpenFileTabAsync(path);
        }
    }

    private static void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Show();
        window.Activate();
        // Topmost toggle is the reliable way to raise a window when Activate alone only flashes the taskbar.
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
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
