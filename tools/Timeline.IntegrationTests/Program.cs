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
        Console.WriteLine("Timeline and structured JSON integration passed: real file tailing, Serilog CLEF, typed filtering, inspector layout, toggle view, live appends and disposal.");
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
        string c = Path.Combine(folder, "c.json");

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

            // ── Phase 2: Structured JSON Real-File Integration Tests ─────────────
            string initialJson =
                "{\"@t\":\"2026-11-06T10:10:00+01:00\",\"@l\":\"Information\",\"@m\":\"Order 1 created\",\"@mt\":\"Order {OrderId} created\",\"OrderId\":1001,\"StatusCode\":200,\"User\":{\"Name\":\"Bob\",\"Id\":42}}\n" +
                "{\"@t\":\"2026-11-06T10:11:00+01:00\",\"@l\":\"Warning\",\"@m\":\"Payment delayed\",\"@mt\":\"Payment delayed\",\"OrderId\":1001,\"StatusCode\":408}\n" +
                "{\"@t\":\"2026-11-06T10:12:00+01:00\",\"@l\":\"Error\",\"@m\":\"Payment failed\",\"@x\":\"System.TimeoutException: dead\",\"StatusCode\":500}\n" +
                "Plain non-json log line without brackets\n" +
                "{\"@t\":\"2026-11-06T10:13:00+01:00\", malformed line\n";

            await File.WriteAllTextAsync(c, initialJson);
            AppSettings settingsJson = new() { MaxLinesInMemory = 20, MaxLoadMegabytes = 1 };
            using LogTabViewModel sourceC = new(c, settingsJson);
            await sourceC.InitializeAsync();
            Check(sourceC.IsLoaded, "JSON file sourceC did not load");
            Check(sourceC.AllLines.Count == 5, "JSON line count mismatch");
            Check(sourceC.HasStructuredLogs, "sourceC did not detect structured logs");

            // Verify view layout with structured columns
            view.DataContext = sourceC;
            await UntilAsync(() => grid.Columns[5].Width == 70 && grid.Columns[6].Width == 190, "Structured column sizing");

            // Verify selection & Property Inspector
            sourceC.SelectedLogLine = sourceC.AllLines[0];
            Check(sourceC.IsSelectedLineStructured, "First line not marked structured");
            Check(sourceC.SelectedLineLevel == "Information", "Inspector level mismatch");
            Check(sourceC.SelectedLineMessage == "Order 1 created", "Inspector message mismatch");
            Check(sourceC.SelectedLineTemplate == "Order {OrderId} created", "Inspector template mismatch");
            Check(sourceC.SelectedLineProperties.Any(p => p.Name == "OrderId" && p.Value == "1001"), "Inspector OrderId mismatch");
            Check(sourceC.SelectedLineProperties.Any(p => p.Name == "User" && p.HasChildren), "Inspector nested User object mismatch");

            // Verify DetailsPanel layout
            MainViewModel mainVm = new(new LogTailPro.Services.SettingsService());
            mainVm.Tabs.Add(sourceC);
            mainVm.SelectedTab = sourceC;
            DetailsPanel detailsPanel = new() { DataContext = mainVm };
            detailsPanel.Measure(new Size(400, 600));
            detailsPanel.Arrange(new Rect(0, 0, 400, 600));

            // Verify typed field filtering: StatusCode >= 400
            sourceC.SearchText = "StatusCode >= 400";
            sourceC.FilterModeIndex = 1; // Include matching
            await UntilAsync(() => sourceC.VisibleLines.Count == 2, "Typed filter StatusCode >= 400");
            Check(sourceC.VisibleLines.All(l => l.StructuredEvent?.TryGetProperty("StatusCode", out object? s) == true && Convert.ToInt32(s) >= 400),
                "Typed filter StatusCode >= 400 included non-matching lines");

            // Verify typed field filtering: StatusCode == 200
            sourceC.SearchText = "StatusCode == 200";
            await UntilAsync(() => sourceC.VisibleLines.Count == 1, "Typed filter StatusCode == 200");
            Check(sourceC.VisibleLines[0].Text.Contains("Order 1 created"), "Typed filter StatusCode == 200 matched wrong line");

            // Verify existence filtering: exists(User.Id)
            sourceC.SearchText = "exists(User.Id)";
            await UntilAsync(() => sourceC.VisibleLines.Count == 1, "Typed filter exists(User.Id)");
            Check(sourceC.VisibleLines[0].Text.Contains("Bob"), "Typed filter exists(User.Id) matched wrong line");

            // Reset filter
            sourceC.FilterModeIndex = 0;
            sourceC.SearchText = "";
            await UntilAsync(() => sourceC.VisibleLines.Count == 5, "Clear typed filter");

            // Verify toggle to Raw View
            sourceC.ToggleStructuredViewCommand.Execute(null);
            Check(!sourceC.IsStructuredViewEnabled, "Structured view toggle failed to disable");
            await UntilAsync(() => grid.Columns[5].Width == 0 && grid.Columns[6].Width == 0, "Structured columns hidden in Raw View");

            sourceC.ToggleStructuredViewCommand.Execute(null);
            Check(sourceC.IsStructuredViewEnabled, "Structured view toggle failed to re-enable");
            await UntilAsync(() => grid.Columns[5].Width == 70 && grid.Columns[6].Width == 190, "Structured columns restored in Structured View");

            // Verify live tail append of structured JSON record
            await File.AppendAllTextAsync(c, "{\"@t\":\"2026-11-06T10:14:00+01:00\",\"@l\":\"Fatal\",\"@m\":\"System halted\",\"ErrorCode\":999}\n");
            await UntilAsync(() => sourceC.AllLines.Count == 6, "Live JSON append");
            LogLine appended = sourceC.AllLines[^1];
            Check(appended.StructuredLevel == "Fatal", "Live appended JSON record did not parse Level correctly");
            Check(appended.StructuredEvent?.TryGetProperty("ErrorCode", out object? ec) == true && Convert.ToInt32(ec) == 999, "Live appended JSON record did not parse custom property");

            sourceC.Dispose();
        }
        finally
        {
            sourceA.Dispose();
            sourceB.Dispose();
            Directory.Delete(folder, true);
        }
    }
}
