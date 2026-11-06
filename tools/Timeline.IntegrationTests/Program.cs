using System.IO;
using System.Windows;
using System.Windows.Controls;
using LogTailPro.Models;
using LogTailPro.ViewModels;
using LogTailPro.Views;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application app = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Exception? failure = null;
        app.Dispatcher.InvokeAsync(async () =>
        {
            try { await VerifyAsync(); }
            catch (Exception ex) { failure = ex; }
            finally { app.Shutdown(); }
        });
        app.Run();
        if (failure != null) throw new InvalidOperationException("Timeline integration failed", failure);
        Console.WriteLine("Timeline integration passed: real file tailing, filters, pause/resume, retention, rotation, navigation, source closure and disposal.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task UntilAsync(Func<bool> condition, string description)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException(description);
            await Task.Delay(50);
        }
    }

    private static async Task VerifyAsync()
    {
        string folder = Path.Combine(Path.GetTempPath(), "LogTailPro-Timeline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string a = Path.Combine(folder, "a.log");
        string b = Path.Combine(folder, "b.log");
        await File.WriteAllTextAsync(a, "2026-11-06T10:02:00Z a2\n2026-11-06T10:04:00Z a4\n");
        await File.WriteAllTextAsync(b, "2026-11-06T10:03:00Z b3\n");
        AppSettings settings = new() { MaxLinesInMemory = 3, MaxLoadMegabytes = 1 };
        using LogTabViewModel sourceA = new(a, settings);
        using LogTabViewModel sourceB = new(b, settings);
        try
        {
            await sourceA.InitializeAsync();
            await sourceB.InitializeAsync();
            Check(sourceA.IsLoaded && sourceB.IsLoaded, "Sources did not load");
            using LogTabViewModel timeline = new(new[] { sourceA, sourceB }, settings);
            await UntilAsync(() => timeline.VisibleLines.Count == 3, "Initial merge");
            Check(timeline.AllLines[0].Text.EndsWith("a2") && timeline.AllLines[1].Text.EndsWith("b3"), "Initial order");

            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/LogTailPro;component/Themes/Theme.xaml", UriKind.Relative)
            });
            // Load and lay out the actual views to catch XAML resource/binding construction errors.
            LogTabView view = new() { DataContext = timeline };
            view.Measure(new Size(1000, 600));
            view.Arrange(new Rect(0, 0, 1000, 600));
            ListView list = (ListView)view.FindName("LogListView");
            GridView grid = (GridView)list.View;
            Check(grid.Columns[3].Width == 245 && grid.Columns[4].Width == 180,
                "Timeline timestamp/source columns did not bind their widths");
            view.DataContext = sourceA;
            Check(grid.Columns[3].Width == 0 && grid.Columns[4].Width == 0,
                "Timeline columns leaked into an ordinary source tab");
            view.DataContext = timeline;
            TimelineSourcesDialog picker = new(new[] { sourceA, sourceB });
            Check(picker.SelectedSources.Count == 2, "Source picker defaults");
            picker.Close();

            sourceA.SearchText = "not present";
            sourceA.FilterModeIndex = 1;
            await UntilAsync(() => sourceA.VisibleLines.Count == 0, "Source filter");
            Check(timeline.AllLines.Count == 3, "Source display filter changed timeline");
            timeline.SearchText = "b3";
            timeline.FilterModeIndex = 1;
            await UntilAsync(() => timeline.VisibleLines.Count == 1, "Timeline filter");
            timeline.FilterModeIndex = 0;
            timeline.SearchText = "";
            await UntilAsync(() => timeline.VisibleLines.Count == 3, "Clear timeline filter");

            timeline.PauseSearchCommand.Execute(null);
            await File.AppendAllTextAsync(a, "2026-11-06T10:01:00Z late\n");
            await UntilAsync(() => sourceA.AllLines.Count == 3, "Source append");
            await Task.Delay(600);
            Check(timeline.AllLines.Count == 3, "Paused timeline changed");
            timeline.PauseSearchCommand.Execute(null);
            await UntilAsync(() => timeline.AllLines.Count == 4, "Resume reconciliation");
            Check(timeline.AllLines[0].Text.EndsWith("late"), "Late arrival was not inserted chronologically");

            await File.AppendAllTextAsync(a, "2026-11-06T10:05:00Z a5\n");
            await UntilAsync(() => timeline.AllLines.Any(row => row.Text.EndsWith("a5")), "Retained append");
            Check(timeline.AllLines.Count == 4 && !timeline.AllLines.Any(row => row.Text.EndsWith("a2")), "Retention reconciliation failed");
            LogLine original = sourceB.AllLines[0];
            timeline.SelectedLogLine = timeline.AllLines.Single(row => ReferenceEquals(row.OriginalLine, original));
            LogLine? requested = null;
            timeline.OriginalLineRequested += (_, row) => requested = row.OriginalLine;
            timeline.OpenOriginalLineCommand.Execute(null);
            Check(ReferenceEquals(requested, original), "Original navigation lost identity");
            sourceB.NavigateToLine(original);
            Check(ReferenceEquals(sourceB.SelectedLogLine, original), "Original selection failed");

            await File.WriteAllTextAsync(a, "2026-11-06T10:06:00Z rotated\n");
            await UntilAsync(() => timeline.AllLines.Count == 2 && timeline.AllLines.Any(row => row.Text.EndsWith("rotated")), "Rotation reconciliation");
            sourceB.Dispose();
            await UntilAsync(() => timeline.AllLines.Count == 1, "Closed source removal");
            timeline.Dispose();
            await File.AppendAllTextAsync(a, "2026-11-06T10:07:00Z after-close\n");
            await UntilAsync(() => sourceA.AllLines.Count == 2, "Source remains active after timeline closure");
            await Task.Delay(400);
            Check(timeline.AllLines.Count == 1, "Disposed timeline still updated");
        }
        finally
        {
            sourceA.Dispose();
            sourceB.Dispose();
            Directory.Delete(folder, true);
        }
    }
}
