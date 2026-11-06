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

            MainViewModel wsMainVm = new(new LogTailPro.Services.SettingsService());
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
            MainViewModel restoredMainVm = new(new LogTailPro.Services.SettingsService());
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
            chartView.Measure(new Size(1000, 300));
            chartView.Arrange(new Rect(0, 0, 1000, 300));
        }
        finally
        {
            sourceA.Dispose();
            sourceB.Dispose();
            Directory.Delete(folder, true);
        }
    }
}
