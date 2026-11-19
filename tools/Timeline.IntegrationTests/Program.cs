using System.IO;
using System.Windows;
using System.Windows.Controls;
using LogTailPro.Models;
using LogTailPro.PluginContract;
using LogTailPro.Services;
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
        Console.WriteLine("All phases 1-6 integration passed: combined timeline, structured JSON, Request/Trace ID correlation, saved workspaces, multiline exception grouping, and interactive error timeline chart.");
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
            MainViewModel mainVm = new(new LogTailPro.Services.SettingsService(Path.Combine(folder, "settings-main.json")));
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

            // ── Phase 3: Request/Trace ID Correlation Real-File Integration Tests ──
            string d = Path.Combine(folder, "d.log");
            string e = Path.Combine(folder, "e.json");

            string dContent =
                "2026-11-06T10:20:00Z [req-corr-777] Gateway: Incoming order\n" +
                "2026-11-06T10:20:01Z [req-corr-999] Gateway: Other order\n" +
                "2026-11-06T10:20:05Z [req-corr-777] Gateway: Order completed\n";

            string eContent =
                "{\"@t\":\"2026-11-06T10:20:02Z\",\"@l\":\"Information\",\"RequestId\":\"req-corr-777\",\"@m\":\"Backend: Payment charged\"}\n" +
                "{\"@t\":\"2026-11-06T10:20:03Z\",\"@l\":\"Information\",\"RequestId\":\"req-corr-777\",\"@m\":\"Backend: Inventory reserved\"}\n" +
                "{\"@t\":\"2026-11-06T10:20:04Z\",\"@l\":\"Information\",\"RequestId\":\"req-corr-7774\",\"@m\":\"Backend: Similar but different id\"}\n";

            await File.WriteAllTextAsync(d, dContent);
            await File.WriteAllTextAsync(e, eContent);

            using LogTabViewModel sourceD = new(d, settingsJson);
            using LogTabViewModel sourceE = new(e, settingsJson);
            await sourceD.InitializeAsync();
            await sourceE.InitializeAsync();
            Check(sourceD.IsLoaded && sourceE.IsLoaded, "Sources D and E did not load");

            // Verify suggested ID extraction on selection
            sourceD.SelectedLogLine = sourceD.AllLines[0];
            Check(sourceD.SuggestedCorrelationId == "req-corr-777", "Suggested correlation ID mismatch on raw line");
            sourceE.SelectedLogLine = sourceE.AllLines[0];
            Check(sourceE.SuggestedCorrelationId == "req-corr-777", "Suggested correlation ID mismatch on JSON line");

            // Create Correlation Tab across sourceD and sourceE
            using LogTabViewModel corrTab = new([sourceD, sourceE], "req-corr-777", settingsJson);
            Check(corrTab.IsCorrelationTab, "IsCorrelationTab must be true");
            Check(corrTab.CorrelationId == "req-corr-777", "CorrelationId mismatch");

            // Await correlation merge
            await UntilAsync(() => corrTab.AllLines.Count == 4, "Correlation tab merge count");

            // Verify chronological order:
            // 10:20:00 (D#1) -> 10:20:02 (E#1) -> 10:20:03 (E#2) -> 10:20:05 (D#3)
            Check(corrTab.AllLines[0].Text.Contains("Gateway: Incoming order"), "Correlation row 0 mismatch");
            Check(corrTab.AllLines[1].Text.Contains("Backend: Payment charged"), "Correlation row 1 mismatch");
            Check(corrTab.AllLines[2].Text.Contains("Backend: Inventory reserved"), "Correlation row 2 mismatch");
            Check(corrTab.AllLines[3].Text.Contains("Gateway: Order completed"), "Correlation row 3 mismatch");

            // Verify substring exclusion: req-corr-7774 and req-corr-999 are not in corrTab
            Check(!corrTab.AllLines.Any(r => r.Text.Contains("req-corr-7774")), "Substring req-corr-7774 incorrectly included");
            Check(!corrTab.AllLines.Any(r => r.Text.Contains("req-corr-999")), "req-corr-999 incorrectly included");

            // Verify view layout for correlation tab
            view.DataContext = corrTab;
            await UntilAsync(() => grid.Columns[3].Width == 245 && grid.Columns[4].Width == 180, "Correlation columns layout");

            // Verify navigation back to original line
            LogLine originalBackendLine = sourceE.AllLines[0];
            corrTab.SelectedLogLine = corrTab.AllLines[1];
            LogLine? corrRequested = null;
            corrTab.OriginalLineRequested += (_, row) => corrRequested = row.OriginalLine;
            corrTab.OpenOriginalLineCommand.Execute(null);
            Check(ReferenceEquals(corrRequested, originalBackendLine), "Correlation original navigation lost identity");
            sourceE.NavigateToLine(corrRequested!);
            Check(ReferenceEquals(sourceE.SelectedLogLine, originalBackendLine), "Correlation original selection failed");

            // Verify live tail append to source D matching the correlation ID
            await File.AppendAllTextAsync(d, "2026-11-06T10:20:06Z [req-corr-777] Gateway: Audit logged\n");
            await UntilAsync(() => corrTab.AllLines.Count == 5, "Live correlated append");
            Check(corrTab.AllLines[^1].Text.Contains("Audit logged"), "Appended correlated row mismatch");

            // Verify live tail append of unmatching ID is ignored
            await File.AppendAllTextAsync(e, "{\"@t\":\"2026-11-06T10:20:07Z\",\"@l\":\"Information\",\"RequestId\":\"req-corr-888\",\"@m\":\"Ignored\"}\n");
            await Task.Delay(400);
            Check(corrTab.AllLines.Count == 5, "Uncorrelated append should not be added to correlation tab");

            corrTab.Dispose();
            sourceD.Dispose();
            sourceE.Dispose();

            // ── Phase 4: Saved Workspaces Real-File Integration Tests ──────────
            string wsLogA = Path.Combine(folder, "ws_a.log");
            string wsLogB = Path.Combine(folder, "ws_b.log");
            string wsPath = Path.Combine(folder, "project.ltpw");

            await File.WriteAllTextAsync(wsLogA, "2026-11-06T10:30:00Z line 1 error in auth\n2026-11-06T10:30:05Z line 2 info\n");
            await File.WriteAllTextAsync(wsLogB, "2026-11-06T10:30:02Z line 3 trace-xyz processing\n");

            MainViewModel wsMainVm = new(new LogTailPro.Services.SettingsService(Path.Combine(folder, "settings-ws.json")));
            string capturedLayoutXml = string.Empty;
            wsMainVm.RequestLayoutXml = () => "<LayoutRoot><TestMockLayout/></LayoutRoot>";
            wsMainVm.RestoreLayoutXml = xml => capturedLayoutXml = xml;

            await wsMainVm.OpenFileTabAsync(wsLogA);
            await wsMainVm.OpenFileTabAsync(wsLogB);
            Check(wsMainVm.Tabs.Count == 2, "Workspace source tabs count mismatch");

            LogTabViewModel wsTabA = wsMainVm.Tabs[0];
            LogTabViewModel wsTabB = wsMainVm.Tabs[1];

            wsTabA.SearchText = "error";
            wsTabA.FilterMode = FilterMode.IncludeMatching;
            wsTabA.IsRegexMode = true;
            wsTabA.IsFollowTail = false;
            wsTabA.SavedScrollVertical = 25.0;
            wsTabA.RestoreBookmarks([1]);

            // Add combined timeline tab
            LogTabViewModel wsTimeline = new([wsTabA, wsTabB], wsMainVm.Settings);
            wsMainVm.Tabs.Add(wsTimeline);

            // Add correlation tab
            LogTabViewModel wsCorrelation = new([wsTabA, wsTabB], "trace-xyz", wsMainVm.Settings);
            wsMainVm.Tabs.Add(wsCorrelation);

            // Set active tab to correlation tab
            wsMainVm.SelectedTab = wsCorrelation;

            // Save workspace
            await wsMainVm.SaveWorkspaceAsync(wsPath);
            Check(File.Exists(wsPath), "Workspace file project.ltpw was not written");

            // Create new MainViewModel and load workspace
            MainViewModel restoredMainVm = new(new LogTailPro.Services.SettingsService(Path.Combine(folder, "settings-restored.json")));
            string restoredLayoutXml = string.Empty;
            restoredMainVm.RestoreLayoutXml = xml => restoredLayoutXml = xml;

            await restoredMainVm.LoadWorkspaceAsync(wsPath);

            // Verify all 4 tabs restored: 2 file tabs, 1 timeline, 1 correlation
            Check(restoredMainVm.Tabs.Count == 4, "Restored workspace tab count mismatch");
            LogTabViewModel restoredTabA = restoredMainVm.Tabs.Single(t => string.Equals(t.FilePath, wsLogA, StringComparison.OrdinalIgnoreCase));
            LogTabViewModel restoredTabB = restoredMainVm.Tabs.Single(t => string.Equals(t.FilePath, wsLogB, StringComparison.OrdinalIgnoreCase));
            LogTabViewModel restoredTimeline = restoredMainVm.Tabs.Single(t => t.IsTimeline && !t.IsCorrelationTab);
            LogTabViewModel restoredCorrelation = restoredMainVm.Tabs.Single(t => t.IsCorrelationTab);

            // Check restored properties on file tab A
            Check(restoredTabA.SearchText == "error", "Restored SearchText mismatch");
            Check(restoredTabA.FilterMode == FilterMode.IncludeMatching, "Restored FilterMode mismatch");
            Check(restoredTabA.IsRegexMode, "Restored IsRegexMode mismatch");
            Check(!restoredTabA.IsFollowTail, "Restored IsFollowTail mismatch");
            Check(restoredTabA.SavedScrollVertical == 25.0, "Restored SavedScrollVertical mismatch");
            Check(restoredTabA.Bookmarks.Any(b => b.LineNumber == 1), "Restored bookmark line 1 missing");

            // Check restored timeline and correlation tabs
            Check(restoredTimeline.TimelineSources.Count == 2, "Restored timeline sources count mismatch");
            Check(restoredCorrelation.TimelineSources.Count == 2, "Restored correlation sources count mismatch");
            Check(restoredCorrelation.CorrelationId == "trace-xyz", "Restored CorrelationId mismatch");

            // Check active tab was preserved
            Check(ReferenceEquals(restoredMainVm.SelectedTab, restoredCorrelation), "Restored ActiveTab mismatch");

            // Check layout XML was passed to RestoreLayoutXml delegate
            Check(restoredLayoutXml == "<LayoutRoot><TestMockLayout/></LayoutRoot>", "Restored LayoutXml mismatch");

            // Verify non-destructive load on corrupt JSON
            string corruptLtpw = Path.Combine(folder, "corrupt.ltpw");
            await File.WriteAllTextAsync(corruptLtpw, "{ \"Broken\": JSON ");
            await restoredMainVm.LoadWorkspaceAsync(corruptLtpw);
            Check(restoredMainVm.Tabs.Count == 4, "Corrupt workspace load destroyed current open tabs");
            Check(restoredMainVm.StatusMessage.Contains("Failed to load workspace"), "StatusMessage should indicate failure on corrupt load");

            foreach (var t in wsMainVm.Tabs.ToList()) t.Dispose();
            foreach (var t in restoredMainVm.Tabs.ToList()) t.Dispose();

            // â”€â”€ Phase 5 Multiline Exception Grouping Integration â”€â”€â”€â”€â”€â”€â”€â”€
            string excLog = Path.Combine(folder, "exceptions.log");
            string excContent = "2026-11-06 10:00:00 [Info] Application initialized\n" +
                                "2026-11-06 10:00:01 [Error] Failed to process database operation\n" +
                                "System.InvalidOperationException: Database connection refused (TraceId: 777e8400-e29b-41d4-a716-446655447777)\n" +
                                "   at App.Data.Database.Connect()\n" +
                                "   at App.Services.OrderService.Process()\n";
            await File.WriteAllTextAsync(excLog, excContent);

            using LogTabViewModel excTab = new(excLog, new AppSettings { MaxLinesInMemory = 1000 });
            await excTab.InitializeAsync();
            Check(excTab.IsLoaded, "Exceptions tab failed to load");
            Check(excTab.AllLines.Count == 5, "Exceptions total line count mismatch");

            // 1. Verify exception grouped on load (collapsed by default)
            Check(excTab.IsExceptionGroupingEnabled, "Exception grouping should be enabled by default");
            Check(excTab.VisibleLines.Count == 2, $"Expected 2 visible lines when collapsed, got {excTab.VisibleLines.Count}");

            LogLine excRoot = excTab.AllLines[1];
            Check(excRoot.HasContinuations, "Root line should have continuations");
            Check(excRoot.Continuations.Count == 3, $"Expected 3 continuations on root, got {excRoot.Continuations.Count}");
            Check(excRoot.ExpanderGlyph == "\u25b6", "Expander glyph should be \\u25b6 when collapsed");
            Check(excRoot.ContinuationBadge == "+3 frames", "Continuation badge mismatch");

            // 2. Expand and verify all lines visible
            excTab.ToggleGroupExpandedCommand.Execute(excRoot);
            await UntilAsync(() => excTab.VisibleLines.Count == 5, "Expanding exception group");
            Check(excRoot.ExpanderGlyph == "\u25bc", "Expander glyph should be \\u25bc when expanded");

            // 3. Collapse and verify back to 2 lines
            excTab.ToggleGroupExpandedCommand.Execute(excRoot);
            await UntilAsync(() => excTab.VisibleLines.Count == 2, "Collapsing exception group");
            Check(excRoot.ExpanderGlyph == "\u25b6", "Expander glyph should be \\u25b6 when collapsed");

            // 4. Search matching inside collapsed continuation frames
            excTab.FilterMode = FilterMode.IncludeMatching;
            excTab.SearchText = "Database.Connect";
            await UntilAsync(() => excRoot.IsSearchMatch && excTab.VisibleLines.Count == 1, "Search matching inside continuation");
            Check(ReferenceEquals(excTab.VisibleLines[0], excRoot), "VisibleLines should show root matching search inside hidden continuation");
            excTab.SearchText = string.Empty;
            excTab.FilterMode = FilterMode.None;
            await UntilAsync(() => excTab.VisibleLines.Count == 2, "Clearing search on exceptions");

            // 5. Toggle grouping off (Plain Line View)
            excTab.ToggleExceptionGroupingCommand.Execute(null);
            await UntilAsync(() => excTab.VisibleLines.Count == 5, "Plain line view should show all lines");
            Check(!excTab.IsExceptionGroupingEnabled, "Exception grouping should now be disabled");
            excTab.ToggleExceptionGroupingCommand.Execute(null);
            await UntilAsync(() => excTab.VisibleLines.Count == 2, "Re-enabling exception grouping");

            // 6. Live append of additional stack frame
            await File.AppendAllTextAsync(excLog, "   at App.Program.Main()\n");
            await UntilAsync(() => excTab.AllLines.Count == 6, "Live append of stack frame");
            Check(excRoot.Continuations.Count == 4, $"Expected 4 continuations after live append, got {excRoot.Continuations.Count}");
            Check(excRoot.ContinuationBadge == "+4 frames", "Updated continuation badge mismatch");
            Check(excTab.VisibleLines.Count == 2, "Visible lines should remain 2 after collapsed frame append");

            // 7. Live append of normal log line closes the exception group
            await File.AppendAllTextAsync(excLog, "2026-11-06 10:00:02 [Info] Continuing normal operation\n");
            await UntilAsync(() => excTab.AllLines.Count == 7, "Live append of closing normal line");
            Check(excTab.VisibleLines.Count == 3, "Visible lines should be 3 after normal line appended");
            Check(excRoot.Continuations.Count == 4, "Root continuations count should remain 4");

            // 7. Inspector details extraction from continuations
            excTab.SelectedLogLine = excRoot;
            Check(excTab.HasSelectedLineException, "HasSelectedLineException should be true for root with continuations");
            Check(excTab.SelectedLineException.Contains("Database connection refused"), "SelectedLineException missing exception message");
            Check(excTab.SelectedLineException.Contains("at App.Data.Database.Connect()"), "SelectedLineException missing stack frame");

            // â”€â”€ Phase 6 Error Timeline Chart Integration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            string chartLog = Path.Combine(folder, "chart_test.log");
            string chartContent = "2026-11-06 10:00:00 [Info] Server starting\n" +
                                  "2026-11-06 10:05:00 [Warning] High memory usage\n" +
                                  "2026-11-06 10:10:00 [Error] Database connection dropped\n" +
                                  "System.Exception: Socket closed\n" +
                                  "   at DB.Open()\n" +
                                  "2026-11-06 10:15:00 [Info] Server recovered\n" +
                                  "Undated diagnostics line\n";
            await File.WriteAllTextAsync(chartLog, chartContent);

            using LogTabViewModel chartTab = new(chartLog, new AppSettings { MaxLinesInMemory = 1000 });
            await chartTab.InitializeAsync();
            Check(chartTab.IsLoaded, "Chart tab failed to load");

            // 1. Wait for chart aggregation and verify event counts (grouping did not inflate counts)
            await UntilAsync(() => chartTab.TimelineChart.Buckets.Count > 0, "Chart buckets populated");
            Check(chartTab.TimelineChart.TotalEventCount == 5, $"Expected 5 total events (not inflating exception frames), got {chartTab.TimelineChart.TotalEventCount}");
            Check(chartTab.TimelineChart.ErrorCount == 1, $"Expected 1 error count, got {chartTab.TimelineChart.ErrorCount}");
            Check(chartTab.TimelineChart.WarningCount == 1, $"Expected 1 warning count, got {chartTab.TimelineChart.WarningCount}");
            Check(chartTab.TimelineChart.InfoCount == 2, $"Expected 2 info count, got {chartTab.TimelineChart.InfoCount}");
            Check(chartTab.TimelineChart.UndatedCount == 1, $"Expected 1 undated event, got {chartTab.TimelineChart.UndatedCount}");
            Check(chartTab.TimelineChart.HasUndatedEvents, "HasUndatedEvents should be true");

            // 2. Select bucket containing the error to activate time-range filter
            TimeBucket errBucket = chartTab.TimelineChart.Buckets.First(b => b.ErrorCount > 0);
            chartTab.TimelineChart.SelectBucketCommand.Execute(errBucket);
            await UntilAsync(() => chartTab.TimelineChart.HasTimeRangeFilter, "Time-range filter activated");
            Check(errBucket.IsSelected, "Error bucket should be marked IsSelected");
            await UntilAsync(() => chartTab.VisibleLines.Count == 1, "Time-range filter should restrict visible lines to 1");
            Check(chartTab.VisibleLines[0].LineNumber == 3, "Visible line should be the error event at line 3");

            // 3. Clear time-range filter
            chartTab.TimelineChart.ClearFilterCommand.Execute(null);
            await UntilAsync(() => !chartTab.TimelineChart.HasTimeRangeFilter, "Time filter cleared");
            await UntilAsync(() => chartTab.VisibleLines.Count == 5, "Visible lines should restore after clearing filter");

            // 4. Live append updates chart error count
            await File.AppendAllTextAsync(chartLog, "2026-11-06 10:20:00 [Error] Late arrival error\n");
            await UntilAsync(() => chartTab.TimelineChart.ErrorCount == 2, "Live append should update chart error count");
            Check(chartTab.TimelineChart.TotalEventCount == 6, "Total event count should be 6 after live append");

            // 5. Layout and render actual ErrorTimelineChartView
            ErrorTimelineChartView chartView = new() { DataContext = chartTab.TimelineChart };
            Check(ReferenceEquals(chartView.ActiveChartViewModel, chartTab.TimelineChart), "Direct DataContext did not set ActiveChartViewModel");
            Check(ReferenceEquals(chartView.Canvas.DataContext, chartTab.TimelineChart), "Direct DataContext did not propagate to ChartCanvas");

            LogTailPro.MainWindow mainWindow = new() { DataContext = mainVm };
            mainVm.Tabs.Add(chartTab);
            mainVm.SelectedTab = chartTab;
            Check(ReferenceEquals(mainWindow.ErrorChartView.ActiveChartViewModel, chartTab.TimelineChart), "MainViewModel.SelectedTab did not propagate ActiveChartViewModel to ChartView");
            Check(ReferenceEquals(mainWindow.ErrorChartView.Canvas.DataContext, chartTab.TimelineChart), "MainViewModel.SelectedTab did not propagate to ChartCanvas.DataContext");

            // Verify chart panel toggle on legacy layout (layout without chart content id)
            mainVm.RestoreLayoutXml?.Invoke("<LayoutRoot><LayoutPanel Orientation=\"Vertical\"><LayoutPanel Orientation=\"Horizontal\"><LayoutAnchorablePane DockWidth=\"300\"><LayoutAnchorable ContentId=\"process\" Title=\"Process\"/></LayoutAnchorablePane><LayoutDocumentPane/></LayoutPanel></LayoutPanel></LayoutRoot>");
            chartTab.IsChartPanelVisible = true;
            chartTab.IsChartPanelVisible = false;

            // Verify Help panel toggle and layout reset inclusion
            mainVm.IsHelpPanelVisible = true;
            Check(mainVm.IsHelpPanelVisible, "Help panel visible should be true");
            mainVm.IsHelpPanelVisible = false;
            mainVm.RestoreLayoutXml?.Invoke(mainWindow.GetLayoutXml());
            mainVm.IsHelpPanelVisible = true;
            mainVm.IsHelpPanelVisible = false;

            chartView.Measure(new Size(1000, 300));
            chartView.Arrange(new Rect(0, 0, 1000, 300));

            // 6. Verify Unified Search & Results panel rendering, execution and clearing
            SearchPanelView searchView = new() { DataContext = mainVm };
            searchView.Measure(new Size(800, 250));
            searchView.Arrange(new Rect(0, 0, 800, 250));
            chartTab.SearchText = "error";
            chartTab.ExecuteSearchCommand.Execute(null);
            await UntilAsync(() => chartTab.SearchResults.Count > 0, "Search results populated in unified view");
            Check(chartTab.IsSearchPanelVisible, "Search panel should be visible after ExecuteSearch");
            Check(chartTab.SearchResults.Count >= 2, $"Expected at least 2 search results for 'error', got {chartTab.SearchResults.Count}");
            Check(!string.IsNullOrWhiteSpace(chartTab.SearchResultsSummary), "Search results summary should be non-empty");

            // Row selection navigation
            chartTab.SelectedSearchResult = chartTab.SearchResults[0];
            Check(chartTab.SelectedLogLine != null, "Selecting search result row should update SelectedLogLine");

            // Clear search keeps panel open but clears results and query
            chartTab.ClearSearchCommand.Execute(null);
            Check(chartTab.SearchResults.Count == 0, "Search results should be cleared after ClearSearch");
            Check(string.IsNullOrEmpty(chartTab.SearchText), "SearchText should be cleared after ClearSearch");
            Check(chartTab.MatchCount == 0, "MatchCount should be 0 after ClearSearch");

            // 7. Verify Notepad++ Style Plugin System End-to-End
            var pluginLoader = mainVm.PluginLoaderService;
            Check(pluginLoader != null, "PluginLoaderService should not be null");
            Check(Directory.Exists(PluginLoaderService.LocalPluginsDirectory), "Local plugins directory should exist");

            // Verify Context Passing
            mainVm.SelectedTab = chartTab;
            var ctx = mainVm.PluginHost.GetActiveContext();
            Check(ctx != null, "PluginLogContext should not be null");
            Check(ctx!.FilePath == chartLog, "PluginLogContext FilePath mismatch");
            Check(ctx.TabTitle == chartTab.DisplayName, "PluginLogContext TabTitle mismatch");

            // Verify IPluginHost Services (Status, Line Navigation)
            mainVm.PluginHost.SetStatusMessage("EndToEnd Status Verification");
            Check(mainVm.StatusMessage == "EndToEnd Status Verification", "SetStatusMessage failed to update MainViewModel");

            mainVm.PluginHost.NavigateToLine(2, chartLog);
            Check(chartTab.SelectedLogLine != null && chartTab.SelectedLogLine.LineNumber == 2,
                "NavigateToLine failed to select line 2 in active tab");

            // Verify Live LinesAppended event pipeline
            bool linesAppendedEventFired = false;
            EventHandler<LogLinesAppendedEventArgs> appendedHandler = (s, e) =>
            {
                if (e.FilePath == chartLog && e.NewLines.Count == 1)
                {
                    linesAppendedEventFired = true;
                }
            };
            mainVm.PluginHost.LinesAppended += appendedHandler;
            try
            {
                mainVm.PluginHost.RaiseLinesAppended(chartLog, new[] { "Appended log line for test" }, 3);
                Check(linesAppendedEventFired, "PluginHost.LinesAppended event was not received");
            }
            finally
            {
                mainVm.PluginHost.LinesAppended -= appendedHandler;
            }

            // ── Phase 3 Controlled Plugin Fixtures Verification ────────────────
            string fixtureDllSource = typeof(TestPlugins.ValidTestPlugin).Assembly.Location;
            Check(File.Exists(fixtureDllSource), $"TestPluginFixture.dll must exist at: {fixtureDllSource}");

            string fixturesDir = Path.Combine(folder, "ControlledPlugins");
            Directory.CreateDirectory(fixturesDir);

            // Fixture 1: Valid Plugin in a custom directory
            string validPluginDir = Path.Combine(fixturesDir, "ValidPlugin");
            Directory.CreateDirectory(validPluginDir);
            File.Copy(fixtureDllSource, Path.Combine(validPluginDir, "TestPluginFixture.dll"));
            await File.WriteAllTextAsync(Path.Combine(validPluginDir, "plugin.json"), """
            {
                "id": "com.logtailpro.test.valid",
                "name": "Valid Test Plugin",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.ValidTestPlugin",
                "menu": {
                    "header": "Valid Test Plugin",
                    "items": [
                        { "id": "valid_menu_cmd", "header": "Valid Menu Command" }
                    ]
                }
            }
            """);

            // Test 1: Discovery, initialization, command registration, and nonzero count
            var addValidResult = await PluginManagementService.AddPluginFolderAsync(mainVm, validPluginDir);
            Check(addValidResult.LoadedFromFolder.Count == 1, $"Expected 1 plugin from ValidPlugin dir, got {addValidResult.LoadedFromFolder.Count}");
            Check(pluginLoader!.LoadedPlugins.Count >= 1, "Expected nonzero loaded plugins count");
            Check(pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.valid"), "Valid plugin not loaded");

            var validInfo = pluginLoader.LoadedPlugins.First(p => p.Plugin.PluginId == "com.logtailpro.test.valid");
            Type validPluginType = validInfo.Plugin.GetType();
            int initCount = (int)validPluginType.GetField("InitializeCount")!.GetValue(null)!;
            Check(initCount > 0, "ValidTestPlugin.InitializeAsync was not executed");

            // Verify command registration & execution
            var validGroup = pluginLoader.MenuGroups.FirstOrDefault(g => g.PluginId == "com.logtailpro.test.valid");
            Check(validGroup != null, "MenuGroup for valid plugin not created");
            Check(validGroup!.Items.Any(i => i.CommandId == "valid_cmd"), "Dynamic command valid_cmd not registered");
            pluginLoader.ExecutePluginCommand("com.logtailpro.test.valid", "valid_cmd");
            int cmdCount = (int)validPluginType.GetField("CommandExecutedCount")!.GetValue(null)!;
            Check(cmdCount > 0, "ExecutePluginCommand failed to invoke plugin");

            // Verify lines appended event received by active plugin
            mainVm.PluginHost.RaiseLinesAppended(chartLog, new[] { "Line for test" }, 1);
            bool linesRcvd = (bool)validPluginType.GetField("LinesAppendedReceived")!.GetValue(null)!;
            Check(linesRcvd, "Active plugin did not receive LinesAppended event");

            // Test 2: Overlapping and successive reloads & Main Menu command
            int preReloadCount = pluginLoader.LoadedPlugins.Count;
            await Task.WhenAll(
                pluginLoader.ReloadPluginsAsync(),
                pluginLoader.ReloadPluginsAsync(),
                pluginLoader.ReloadPluginsAsync()
            );
            Check(pluginLoader.LoadedPlugins.Count == preReloadCount,
                $"Overlapping reloads count mismatch: expected {preReloadCount}, got {pluginLoader.LoadedPlugins.Count}");
            int shutdownCount = (int)validPluginType.GetField("ShutdownCount")!.GetValue(null)!;
            Check(shutdownCount > 0, "Shutdown was not called on reload");

            // Verify Main-Menu Reload Command
            await mainVm.ReloadPluginsCommand.ExecuteAsync(null);
            Check(mainVm.StatusMessage.Contains("plugin(s) loaded"), "Main-menu ReloadPluginsCommand did not update StatusMessage correctly");

            // Test 3: Broken DLL
            string brokenDir = Path.Combine(fixturesDir, "BrokenDllPlugin");
            Directory.CreateDirectory(brokenDir);
            await File.WriteAllBytesAsync(Path.Combine(brokenDir, "CorruptPlugin.dll"), new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44 });
            var addBrokenResult = await PluginManagementService.AddPluginFolderAsync(mainVm, brokenDir);
            Check(addBrokenResult.LoadedFromFolder.Count == 0, "Broken DLL should not produce loaded plugins");
            Check(pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.valid"), "Valid plugin must remain loaded after broken DLL probe");

            // Test 4: Failed initialization
            string failingDir = Path.Combine(fixturesDir, "FailingPlugin");
            Directory.CreateDirectory(failingDir);
            File.Copy(fixtureDllSource, Path.Combine(failingDir, "TestPluginFixture.dll"));
            await File.WriteAllTextAsync(Path.Combine(failingDir, "plugin.json"), """
            {
                "id": "com.logtailpro.test.failing",
                "name": "Failing Test Plugin",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.FailingTestPlugin"
            }
            """);
            var addFailingResult = await PluginManagementService.AddPluginFolderAsync(mainVm, failingDir);
            Check(addFailingResult.LoadedFromFolder.Count == 0, "Failing plugin should not be marked as loaded");
            Check(!pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.failing"), "Failing plugin should not exist in LoadedPlugins");

            // Test 5: Timed-out initialization with timeout override & stale callback guard
            string timeoutDir = Path.Combine(fixturesDir, "TimeoutPlugin");
            Directory.CreateDirectory(timeoutDir);
            File.Copy(fixtureDllSource, Path.Combine(timeoutDir, "TestPluginFixture.dll"));
            await File.WriteAllTextAsync(Path.Combine(timeoutDir, "plugin.json"), """
            {
                "id": "com.logtailpro.test.timeout",
                "name": "Timeout Test Plugin",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.TimeoutTestPlugin"
            }
            """);
            var originalTimeout = PluginLoaderService.PluginInitTimeout;
            PluginLoaderService.PluginInitTimeout = TimeSpan.FromMilliseconds(200);
            try
            {
                var addTimeoutResult = await PluginManagementService.AddPluginFolderAsync(mainVm, timeoutDir);
                Check(addTimeoutResult.LoadedFromFolder.Count == 0, "Timed out plugin should not be marked as loaded");
                Check(!pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.timeout"), "Timed out plugin should not be in LoadedPlugins");

                // Wait 2.2 seconds to allow the plugin task to complete its delay and attempt stale callback
                await Task.Delay(2200);
                Check(!pluginLoader.MenuGroups.Any(g => g.PluginId == "com.logtailpro.test.timeout"), "Stale registration from timed out plugin must be guarded against");
            }
            finally
            {
                PluginLoaderService.PluginInitTimeout = originalTimeout;
            }

            // Test 6: Fallback to valid copy when higher-priority copy fails
            string fallbackHighPriorityDir = Path.Combine(fixturesDir, "FallbackHighPriority");
            Directory.CreateDirectory(fallbackHighPriorityDir);
            File.Copy(fixtureDllSource, Path.Combine(fallbackHighPriorityDir, "TestPluginFixture.dll"));
            await File.WriteAllTextAsync(Path.Combine(fallbackHighPriorityDir, "plugin.json"), """
            {
                "id": "com.logtailpro.test.fallback",
                "name": "Fallback Test Plugin",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.FallbackFailingPlugin"
            }
            """);

            string fallbackLowPriorityDir = Path.Combine(fixturesDir, "FallbackLowPriority");
            Directory.CreateDirectory(fallbackLowPriorityDir);
            File.Copy(fixtureDllSource, Path.Combine(fallbackLowPriorityDir, "TestPluginFixture.dll"));
            await File.WriteAllTextAsync(Path.Combine(fallbackLowPriorityDir, "plugin.json"), """
            {
                "id": "com.logtailpro.test.fallback",
                "name": "Fallback Test Plugin",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.FallbackValidPlugin"
            }
            """);

            // Configure high-priority before low-priority in CustomPluginDirectories
            mainVm.Settings.CustomPluginDirectories.Add(fallbackHighPriorityDir);
            mainVm.Settings.CustomPluginDirectories.Add(fallbackLowPriorityDir);
            mainVm.SaveSettings();
            await pluginLoader.ReloadPluginsAsync();

            Check(pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.fallback"),
                "Fallback copy should have succeeded when higher priority copy failed");
            var fallbackPlugin = pluginLoader.LoadedPlugins.First(p => p.Plugin.PluginId == "com.logtailpro.test.fallback");
            Check(fallbackPlugin.Plugin.GetType().Name == "FallbackValidPlugin", "FallbackValidPlugin should be loaded");
            bool fallbackInit = (bool)fallbackPlugin.Plugin.GetType().GetField("InitializedSuccessfully")!.GetValue(null)!;
            Check(fallbackInit, "FallbackValidPlugin should be the one successfully initialized");

            // Test 7: Empty Add Folder while another plugin is already loaded
            string emptyDir = Path.Combine(fixturesDir, "EmptyPluginFolder");
            Directory.CreateDirectory(emptyDir);
            int countBeforeEmpty = pluginLoader.LoadedPlugins.Count;
            var addEmptyResult = await PluginManagementService.AddPluginFolderAsync(mainVm, emptyDir);
            Check(addEmptyResult.LoadedFromFolder.Count == 0, "Empty folder should report 0 plugins loaded from it");
            Check(addEmptyResult.TotalLoadedCount == countBeforeEmpty, "Existing plugins should remain loaded");

            // Test 8: Removing one plugin from a shared root while its sibling stays available
            string sharedRoot = Path.Combine(fixturesDir, "SharedRoot");
            Directory.CreateDirectory(sharedRoot);

            string sibADir = Path.Combine(sharedRoot, "SiblingA");
            Directory.CreateDirectory(sibADir);
            File.Copy(fixtureDllSource, Path.Combine(sibADir, "TestPluginFixture.dll"));
            await File.WriteAllTextAsync(Path.Combine(sibADir, "plugin.json"), """
            {
                "id": "com.logtailpro.test.siblinga",
                "name": "Sibling Plugin A",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.SiblingAPlugin"
            }
            """);

            string sibBDir = Path.Combine(sharedRoot, "SiblingB");
            Directory.CreateDirectory(sibBDir);
            File.Copy(fixtureDllSource, Path.Combine(sibBDir, "TestPluginFixture.dll"));
            await File.WriteAllTextAsync(Path.Combine(sibBDir, "plugin.json"), """
            {
                "id": "com.logtailpro.test.siblingb",
                "name": "Sibling Plugin B",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.SiblingBPlugin"
            }
            """);

            var addSharedResult = await PluginManagementService.AddPluginFolderAsync(mainVm, sharedRoot);
            Check(addSharedResult.LoadedFromFolder.Count >= 2, $"Expected at least 2 plugins from shared root, got {addSharedResult.LoadedFromFolder.Count}");

            var sibAInfo = pluginLoader.LoadedPlugins.FirstOrDefault(p => p.Plugin.PluginId == "com.logtailpro.test.siblinga");
            var sibBInfo = pluginLoader.LoadedPlugins.FirstOrDefault(p => p.Plugin.PluginId == "com.logtailpro.test.siblingb");
            Check(sibAInfo != null, "Sibling A should be loaded");
            Check(sibBInfo != null, "Sibling B should be loaded");

            // Remove Sibling A
            var removeResult = await PluginManagementService.RemovePluginAsync(mainVm, sibAInfo!);
            Check(removeResult.Success, $"Removal of Sibling A failed: {removeResult.ErrorMessage}");

            // Verify Sibling A is removed, but Sibling B is still loaded and available
            Check(!pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.siblinga"), "Sibling A must not be loaded after removal");
            Check(pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.siblingb"), "Sibling B must remain loaded after Sibling A removal");
            Check(Directory.Exists(sibADir), "External custom directory on disk must be preserved, not deleted");

            // ── Finding 1: Removal from Managed Plugin Directory (Locked vs Unlocked) ────
            string isolatedManagedRoot = Path.Combine(fixturesDir, "IsolatedManagedRoot");
            Directory.CreateDirectory(isolatedManagedRoot);
            pluginLoader.UserPluginsPath = isolatedManagedRoot;

            // 1a: Managed plugin configured for locked deletion failure test
            string managedLockedDir = Path.Combine(isolatedManagedRoot, "ManagedLockedPlugin");
            Directory.CreateDirectory(managedLockedDir);
            string lockedDllPath = Path.Combine(managedLockedDir, "TestPluginFixture.dll");
            File.Copy(fixtureDllSource, lockedDllPath);
            await File.WriteAllTextAsync(Path.Combine(managedLockedDir, "plugin.json"), """
            {
                "id": "com.logtailpro.test.managed.locked",
                "name": "Managed Locked Plugin",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.ManagedLockedPlugin"
            }
            """);

            // 1b: Managed plugin configured for clean deletion test
            string managedCleanDir = Path.Combine(isolatedManagedRoot, "ManagedCleanPlugin");
            Directory.CreateDirectory(managedCleanDir);
            string cleanDllPath = Path.Combine(managedCleanDir, "TestPluginFixture.dll");
            File.Copy(fixtureDllSource, cleanDllPath);
            await File.WriteAllTextAsync(Path.Combine(managedCleanDir, "plugin.json"), """
            {
                "id": "com.logtailpro.test.managed.clean",
                "name": "Managed Clean Plugin",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.ManagedCleanPlugin"
            }
            """);

            await pluginLoader.ReloadPluginsAsync();
            var managedLockedInfo = pluginLoader.LoadedPlugins.FirstOrDefault(p => p.Plugin?.PluginId == "com.logtailpro.test.managed.locked");
            var managedCleanInfo = pluginLoader.LoadedPlugins.FirstOrDefault(p => p.Plugin?.PluginId == "com.logtailpro.test.managed.clean");
            Check(managedLockedInfo != null, "Managed locked plugin must be loaded from isolated managed root");
            Check(managedCleanInfo != null, "Managed clean plugin must be loaded from isolated managed root");

            // 1a: Test deletion failure while a file in the directory is locked
            using (var lockStream = new FileStream(lockedDllPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var failedManagedRemoval = await PluginManagementService.RemovePluginAsync(mainVm, managedLockedInfo!);
                Check(!failedManagedRemoval.Success, "Failed deletion must never report success");
                Check(failedManagedRemoval.RestartRequired, "Locked deletion must report restartRequired");
                Check(!string.IsNullOrWhiteSpace(failedManagedRemoval.ErrorMessage), "Locked deletion must report error message");
                Check(Directory.Exists(managedLockedDir), "Directory must still exist when deletion fails");
                Check(!pluginLoader.LoadedPlugins.Any(p => p.Plugin?.PluginId == "com.logtailpro.test.managed.locked"),
                    "Failed deletion plugin must still be retired from LoadedPlugins in current session");
            }

            // 1b: Test successful deletion of a loaded DLL in an isolated managed root
            var successfulManagedRemoval = await PluginManagementService.RemovePluginAsync(mainVm, managedCleanInfo!);
            Check(successfulManagedRemoval.Success, "Removal should succeed when files are not locked");
            Check(!Directory.Exists(managedCleanDir), "Managed clean plugin directory must be deleted on successful removal");
            Check(!pluginLoader.LoadedPlugins.Any(p => p.Plugin?.PluginId == "com.logtailpro.test.managed.clean"),
                "Managed clean plugin must be removed from LoadedPlugins");

            // ── Finding 2: Shared Discovery Root with Root Plugin + 2 Children ──────────
            string sharedMultiRoot = Path.Combine(fixturesDir, "SharedMultiRoot");
            Directory.CreateDirectory(sharedMultiRoot);

            // Root directory's own plugin
            File.Copy(fixtureDllSource, Path.Combine(sharedMultiRoot, "TestPluginFixture.dll"));
            await File.WriteAllTextAsync(Path.Combine(sharedMultiRoot, "plugin.json"), """
            {
                "id": "com.logtailpro.test.rootdiscovery",
                "name": "Root Discovery Plugin",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.RootDiscoveryPlugin"
            }
            """);

            // Child A
            string multiChildADir = Path.Combine(sharedMultiRoot, "ChildA");
            Directory.CreateDirectory(multiChildADir);
            File.Copy(fixtureDllSource, Path.Combine(multiChildADir, "TestPluginFixture.dll"));
            await File.WriteAllTextAsync(Path.Combine(multiChildADir, "plugin.json"), """
            {
                "id": "com.logtailpro.test.sharedchilda",
                "name": "Shared Child A Plugin",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.SharedChildAPlugin"
            }
            """);

            // Child B
            string multiChildBDir = Path.Combine(sharedMultiRoot, "ChildB");
            Directory.CreateDirectory(multiChildBDir);
            File.Copy(fixtureDllSource, Path.Combine(multiChildBDir, "TestPluginFixture.dll"));
            await File.WriteAllTextAsync(Path.Combine(multiChildBDir, "plugin.json"), """
            {
                "id": "com.logtailpro.test.sharedchildb",
                "name": "Shared Child B Plugin",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.SharedChildBPlugin"
            }
            """);

            var addMultiResult = await PluginManagementService.AddPluginFolderAsync(mainVm, sharedMultiRoot);
            Check(pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.rootdiscovery"), "Root plugin must be loaded");
            Check(pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.sharedchilda"), "Child A must be loaded");
            Check(pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.sharedchildb"), "Child B must be loaded");

            // Remove Child A from shared root
            var childAInfo = pluginLoader.LoadedPlugins.First(p => p.Plugin.PluginId == "com.logtailpro.test.sharedchilda");
            var removeChildAResult = await PluginManagementService.RemovePluginAsync(mainVm, childAInfo);
            Check(removeChildAResult.Success, "Child A removal must succeed");

            // Verify shared root stays registered, Child A is excluded, Child A's files remain on disk
            Check(!pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.sharedchilda"), "Child A must be removed from LoadedPlugins");
            Check(pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.rootdiscovery"), "Root plugin must remain loaded");
            Check(pluginLoader.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.sharedchildb"), "Child B must remain loaded");
            Check(Directory.Exists(multiChildADir), "Child A external files must remain on disk");
            Check(mainVm.Settings.CustomPluginDirectories.Any(c => string.Equals(c, sharedMultiRoot, StringComparison.OrdinalIgnoreCase)),
                "Shared root must remain registered in CustomPluginDirectories");
            Check(mainVm.Settings.ExcludedPluginDirectories.Any(e => e.Contains("ChildA")),
                "Child A must be persisted in ExcludedPluginDirectories");

            // Verify fresh load from saved settings
            var freshSettings = new SettingsService(Path.Combine(folder, "settings-main.json")).Load();
            Check(freshSettings.CustomPluginDirectories.Any(c => string.Equals(c, sharedMultiRoot, StringComparison.OrdinalIgnoreCase)),
                "Fresh load must preserve shared root in CustomPluginDirectories");
            Check(freshSettings.ExcludedPluginDirectories.Any(e => e.Contains("ChildA")),
                "Fresh load must preserve Child A exclusion");

            MainViewModel freshVm = new(new SettingsService(Path.Combine(folder, "settings-main.json")));
            freshVm.PluginLoaderService.CustomPluginDirectories = freshSettings.CustomPluginDirectories;
            freshVm.PluginLoaderService.ExcludedPluginDirectories = freshSettings.ExcludedPluginDirectories;
            freshVm.PluginLoaderService.UserPluginsPath = isolatedManagedRoot;
            await freshVm.PluginLoaderService.ReloadPluginsAsync();

            Check(freshVm.PluginLoaderService.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.rootdiscovery"), "Root plugin must load after fresh start");
            Check(freshVm.PluginLoaderService.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.sharedchildb"), "Child B must load after fresh start");
            Check(!freshVm.PluginLoaderService.LoadedPlugins.Any(p => p.Plugin.PluginId == "com.logtailpro.test.sharedchilda"), "Excluded Child A must not load after fresh start");

            // ── Finding 3: Diagnostic Instrumentation for Slow Callbacks ────────────────
            string slowDir = Path.Combine(fixturesDir, "SlowSyncPlugin");
            Directory.CreateDirectory(slowDir);
            File.Copy(fixtureDllSource, Path.Combine(slowDir, "TestPluginFixture.dll"));
            await File.WriteAllTextAsync(Path.Combine(slowDir, "plugin.json"), """
            {
                "id": "com.logtailpro.test.slowsync",
                "name": "Slow Sync Init Plugin",
                "version": "1.0.0",
                "entryAssembly": "TestPluginFixture.dll",
                "entryClass": "TestPlugins.SlowSyncInitPlugin"
            }
            """);

            var addSlowResult = await PluginManagementService.AddPluginFolderAsync(mainVm, slowDir);
            var slowInfo = pluginLoader.LoadedPlugins.FirstOrDefault(p => p.Plugin?.PluginId == "com.logtailpro.test.slowsync");
            Check(slowInfo != null, "Slow sync plugin should load");
            Check(pluginLoader.LoadLog.Any(l => l.Contains("Plugin constructor took") || l.Contains("InitializeAsync synchronous portion took")),
                "Instrumentation must log warning when synchronous callbacks exceed threshold");

            // ── Finding 4: Reject Queued Callbacks from Retired Plugin Sessions ──────────
            // Obtain old active session for ValidTestPlugin
            var oldValidInfo = pluginLoader.LoadedPlugins.First(p => p.Plugin.PluginId == "com.logtailpro.test.valid");
            var oldSession = oldValidInfo.HostSession;
            Check(oldSession != null, "HostSession must be present");

            // Simulate a queued delegate that was scheduled from the old session before reload
            Action queuedStaleRegistration = () =>
            {
                pluginLoader.RegisterDynamicMenuItem(oldSession!, new PluginMenuItem
                {
                    Id = "stale_queued_cmd",
                    Header = "Stale Queued Command"
                });
            };

            // Reload to retire oldSession and spawn a new replacement session for the same PluginId
            await pluginLoader.ReloadPluginsAsync();
            var replacementValidInfo = pluginLoader.LoadedPlugins.First(p => p.Plugin.PluginId == "com.logtailpro.test.valid");
            var replacementSession = replacementValidInfo.HostSession;
            Check(replacementSession != null, "Replacement HostSession must be present");
            Check(!ReferenceEquals(oldSession, replacementSession), "Replacement session must be a new instance");
            Check(!oldSession!.IsActive, "Old session must be deactivated after reload");

            // Execute the queued stale registration now on the UI thread
            queuedStaleRegistration();

            // Register a valid command from the new replacement session
            replacementSession!.RegisterMenuItem("com.logtailpro.test.valid", new PluginMenuItem
            {
                Id = "fresh_replacement_cmd",
                Header = "Fresh Replacement Command"
            });

            var validMenuGroup = pluginLoader.MenuGroups.First(g => g.PluginId == "com.logtailpro.test.valid");
            Check(!validMenuGroup.Items.Any(i => i.CommandId == "stale_queued_cmd"),
                "Stale command from retired host session must be rejected");
            Check(validMenuGroup.Items.Any(i => i.CommandId == "fresh_replacement_cmd"),
                "Replacement session must be permitted to register its command");

            // ── Finding 5: Batched Severity Stats Notifications ─────────────────────────
            int infoNotifies = 0;
            int warnNotifies = 0;
            int errorNotifies = 0;
            sourceA.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LogTabViewModel.StatsInfoCount)) infoNotifies++;
                if (e.PropertyName == nameof(LogTabViewModel.StatsWarnCount)) warnNotifies++;
                if (e.PropertyName == nameof(LogTabViewModel.StatsErrorCount)) errorNotifies++;
            };

            int startInfo = sourceA.StatsInfoCount;
            int startWarn = sourceA.StatsWarnCount;
            int startError = sourceA.StatsErrorCount;

            List<LogLine> mixedBatch =
            [
                new LogLine { LineNumber = 100, Text = "2026-11-20T00:00:00Z [INFO] Message 1" },
                new LogLine { LineNumber = 101, Text = "   at System.Continuation1", IsContinuation = true },
                new LogLine { LineNumber = 102, Text = "2026-11-20T00:00:01Z [WARN] Warning 1" },
                new LogLine { LineNumber = 103, Text = "2026-11-20T00:00:02Z [ERROR] Error 1" },
                new LogLine { LineNumber = 104, Text = "   at System.Continuation2", IsContinuation = true },
                new LogLine { LineNumber = 105, Text = "2026-11-20T00:00:03Z [INFO] Message 2" },
                new LogLine { LineNumber = 106, Text = "2026-11-20T00:00:04Z [WARN] Warning 2" },
                new LogLine { LineNumber = 107, Text = "2026-11-20T00:00:05Z [ERROR] Error 2" },
                new LogLine { LineNumber = 108, Text = "2026-11-20T00:00:06Z [INFO] Message 3" }
            ];

            sourceA.UpdateStats(mixedBatch);

            Check(sourceA.StatsInfoCount == startInfo + 3, $"Expected +3 Info, got {sourceA.StatsInfoCount - startInfo}");
            Check(sourceA.StatsWarnCount == startWarn + 2, $"Expected +2 Warn, got {sourceA.StatsWarnCount - startWarn}");
            Check(sourceA.StatsErrorCount == startError + 2, $"Expected +2 Error, got {sourceA.StatsErrorCount - startError}");
            Check(infoNotifies == 1, $"Expected exactly 1 Info notification per batch, got {infoNotifies}");
            Check(warnNotifies == 1, $"Expected exactly 1 Warn notification per batch, got {warnNotifies}");
            Check(errorNotifies == 1, $"Expected exactly 1 Error notification per batch, got {errorNotifies}");

            // Verify batch with single severity produces 0 notifications for unaffected counters
            infoNotifies = 0;
            warnNotifies = 0;
            errorNotifies = 0;
            List<LogLine> infoOnlyBatch =
            [
                new LogLine { LineNumber = 109, Text = "2026-11-20T00:00:07Z [INFO] Msg 4" },
                new LogLine { LineNumber = 110, Text = "2026-11-20T00:00:08Z [INFO] Msg 5" }
            ];
            sourceA.UpdateStats(infoOnlyBatch);
            Check(infoNotifies == 1, $"Expected 1 Info notification, got {infoNotifies}");
            Check(warnNotifies == 0, $"Expected 0 Warn notifications, got {warnNotifies}");
            Check(errorNotifies == 0, $"Expected 0 Error notifications, got {errorNotifies}");

            // Clean up custom plugin directories from test
            mainVm.PluginLoaderService.CustomPluginDirectories.Clear();
            mainVm.PluginLoaderService.ExcludedPluginDirectories.Clear();
            await mainVm.PluginLoaderService.ReloadPluginsAsync();
        }
        finally
        {
            sourceA.Dispose();
            sourceB.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            try { Directory.Delete(folder, true); } catch { /* best-effort cleanup of temp test files */ }
        }
    }
}
