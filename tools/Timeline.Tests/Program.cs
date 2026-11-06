using System.Globalization;
using LogTailPro.Models;
using LogTailPro.Services;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static LogLine Line(long number, string text) => new() { LineNumber = number, Text = text };

TimelineService service = new();
LogLine earlier = Line(2, "2026-11-06T12:00:00+02:00 earlier");
LogLine later = Line(1, "2026-11-06T10:01:00Z later");
LogLine tie = Line(1, "2026-11-06T10:00:00Z tie");
LogLine undated = Line(3, "stack trace without timestamp");
LogLine timeOnly = Line(4, "09:00:00 time without a date");
TimelineSource first = new("C:/a/app.log", "app.log", [later, earlier, undated, timeOnly]);
TimelineSource second = new("C:/b/app.log", "app.log", [tie]);
IReadOnlyList<LogLine> merged = service.Merge([first, second]);
Check(merged.Count == 5, "Rows lost or duplicated");
Check(ReferenceEquals(merged[0].OriginalLine, earlier), "UTC ordering or source tie failed");
Check(ReferenceEquals(merged[1].OriginalLine, tie), "Equal timestamp order failed");
Check(ReferenceEquals(merged[2].OriginalLine, later), "Chronological ordering failed");
Check(merged[3].OriginalLine == undated && merged[4].OriginalLine == timeOnly, "Unknown timestamps must sort last");
Check(merged[0].Text == earlier.Text && earlier.SourceFilePath == null, "Source text/records were modified");
Check(merged[0].SourceFilePath != merged[1].SourceFilePath, "Duplicate basenames lost source identity");
Check(merged[0].SourceLineNumber == 2, "Original line number lost");
Check(TimelineService.ParseTimestamp(Line(1, "2026-11-06T05:00:00-0500")) == merged[0].TimelineTimestamp, "Compact negative offset failed");
Check(TimelineService.ParseTimestamp(Line(1, "2026-11-06 10:00:00,1234567Z"))?.Ticks % TimeSpan.TicksPerSecond == 1234567, "Fractional seconds lost");
Check(TimelineService.ParseTimestamp(Line(1, "2026-99-99T10:00:00Z")) == null, "Malformed date accepted");
Check(TimelineService.ParseTimestamp(timeOnly) == null, "Time-only input received invented date");
DateTimeOffset localExpected = new DateTimeOffset(new DateTime(2026, 11, 6, 10, 0, 0)).ToUniversalTime();
Check(TimelineService.ParseTimestamp(Line(1, "2026-11-06 10:00:00")) == localExpected, "Local timezone policy failed");
IReadOnlyList<LogLine> reconciled = service.Merge([first with { Lines = [earlier] }, second]);
Check(reconciled.Count == 2 && reconciled.All(row => row.OriginalLine != later), "Removed source rows survived reconciliation");
Check(service.Merge([]).Count == 0, "Empty source set failed");
using CancellationTokenSource cancelled = new();
cancelled.Cancel();
try { service.Merge([first], cancelled.Token); throw new Exception("Cancellation ignored"); }
catch (OperationCanceledException) { }

// ── Phase 2 Structured JSON Tests ───────────────────────────────────────────

// 1. Serilog Compact CLEF format with @t, @l, @m, @mt, @x, custom properties
string jsonClef = "{\"@t\":\"2026-11-06T10:15:30.500+02:00\",\"@l\":\"Information\",\"@m\":\"User alice logged in\",\"@mt\":\"User {UserId} logged in\",\"@x\":\"System.Exception: fail\",\"UserId\":42,\"StatusCode\":200,\"IsAdmin\":true,\"Details\":{\"Role\":\"Operator\",\"Tags\":[\"prod\",\"web\"]}}";
StructuredLogEvent? clefEv = JsonLogParser.TryParse(jsonClef);
Check(clefEv != null && clefEv.IsValidJson, "Valid JSON CLEF record failed to parse");
StructuredLogEvent validClef = clefEv!;
Check(validClef.Timestamp.HasValue && validClef.Timestamp.Value.Offset == TimeSpan.FromHours(2), "CLEF timestamp offset preserved");
Check(validClef.Level == "Information", "CLEF level mismatch");
Check(validClef.Message == "User alice logged in", "Rendered message mismatch");
Check(validClef.MessageTemplate == "User {UserId} logged in", "Message template mismatch");
Check(validClef.Message != validClef.MessageTemplate, "Rendered message must not be substituted for template");
Check(validClef.Exception != null && validClef.Exception.StartsWith("System.Exception"), "Exception field mismatch");

// Verify primitive type preservation
Check(validClef.TryGetProperty("UserId", out object? userId) && userId is long uId && uId == 42, "Integer property preservation failed");
Check(validClef.TryGetProperty("StatusCode", out object? sc) && sc is long sCode && sCode == 200, "Integer StatusCode failed");
Check(validClef.TryGetProperty("IsAdmin", out object? admin) && admin is bool bAdmin && bAdmin, "Boolean property failed");
Check(validClef.TryGetProperty("Details.Role", out object? role) && role as string == "Operator", "Nested dot-notation property failed");
Check(validClef.TryGetProperty("Details.Tags", out object? tags) && tags is IReadOnlyList<object?> tList && tList.Count == 2, "Array property failed");

// 2. CLEF default level rule: when @l is missing and @m is present, default level is "Information"
string jsonClefNoLevel = "{\"@t\":\"2026-11-06T10:15:30Z\",\"@m\":\"Processed item\",\"ItemId\":99}";
StructuredLogEvent? clefNoLevelEv = JsonLogParser.TryParse(jsonClefNoLevel);
Check(clefNoLevelEv != null && clefNoLevelEv.Level == "Information", "Serilog CLEF default level 'Information' rule failed");

// 3. Distinct Message vs MessageTemplate: when @m is absent and @mt is present, Message must NOT be populated with @mt
string jsonTemplateOnly = "{\"@t\":\"2026-11-06T10:15:30Z\",\"@mt\":\"Order {OrderId} created\",\"OrderId\":\"ORD-123\"}";
StructuredLogEvent? templateOnlyEv = JsonLogParser.TryParse(jsonTemplateOnly);
Check(templateOnlyEv != null && templateOnlyEv.Message == null, "Message was incorrectly populated from template");
Check(templateOnlyEv!.MessageTemplate == "Order {OrderId} created", "Template failed to parse");

// 4. Malformed JSON handling: invalid JSON text parsed as IsValidJson = false, never throws
string malformedJson = "{\"@t\":\"2026-11-06T10:15:30Z\", \"unclosed string: 123";
StructuredLogEvent? malformedEv = JsonLogParser.TryParse(malformedJson);
Check(malformedEv != null && !malformedEv.IsValidJson && !string.IsNullOrEmpty(malformedEv.ParseError), "Malformed JSON was not caught gracefully");
Check(malformedEv!.RawText == malformedJson, "Malformed JSON raw text was lost");

// 5. Plain text line returns null for structured event
Check(JsonLogParser.TryParse("2026-11-06 10:00:00 plain text log") == null, "Plain text line returned structured event");
Check(JsonLogParser.TryParse("") == null, "Empty string returned structured event");

// 6. Property node tree generation
IReadOnlyList<LogPropertyNode> nodes = LogPropertyNode.FromEvent(validClef);
Check(nodes.Any(n => n.Name == "Timestamp"), "Timestamp node missing");
Check(nodes.Any(n => n.Name == "Level" && n.Value == "Information"), "Level node missing");
Check(nodes.Any(n => n.Name == "Message"), "Message node missing");
Check(nodes.Any(n => n.Name == "MessageTemplate"), "Template node missing");
Check(nodes.Any(n => n.Name == "Exception"), "Exception node missing");
LogPropertyNode? detailsNode = nodes.FirstOrDefault(n => n.Name == "Details");
Check(detailsNode != null && detailsNode.HasChildren && detailsNode.Children.Count == 2, "Nested object hierarchy failed");

// 7. Structured JSON timeline ordering with UTC offsets
LogLine jsonRowParis = Line(1, "{\"@t\":\"2026-11-06T12:00:00+02:00\",\"@m\":\"Paris event\"}"); // 10:00:00 UTC
LogLine jsonRowUtc = Line(2, "{\"@t\":\"2026-11-06T10:05:00Z\",\"@m\":\"UTC event\"}");         // 10:05:00 UTC
LogLine jsonRowNy = Line(3, "{\"@t\":\"2026-11-06T04:55:00-05:00\",\"@m\":\"NY event\"}");       // 09:55:00 UTC
TimelineSource jsonSrc = new("C:/app.json", "app.json", [jsonRowParis, jsonRowUtc, jsonRowNy]);
IReadOnlyList<LogLine> jsonMerged = service.Merge([jsonSrc]);
Check(jsonMerged.Count == 3, "Merged JSON count mismatch");
Check(jsonMerged[0].Text.Contains("NY event"), "Earliest UTC event (NY 09:55 UTC) should be first");
Check(jsonMerged[1].Text.Contains("Paris event"), "Middle UTC event (Paris 10:00 UTC) should be second");
Check(jsonMerged[2].Text.Contains("UTC event"), "Latest UTC event (UTC 10:05 UTC) should be third");

// 8. Typed field filter evaluation and syntax parsing
LogLine clefLine = new() { LineNumber = 1, Text = jsonClef, StructuredEvent = validClef };

// Numeric operators
Check(TypedFieldFilter.TryParse("StatusCode == 200", out TypedFieldFilter? tf1) && tf1!.Matches(clefLine), "StatusCode == 200 failed");
Check(TypedFieldFilter.TryParse("StatusCode >= 200", out TypedFieldFilter? tf2) && tf2!.Matches(clefLine), "StatusCode >= 200 failed");
Check(TypedFieldFilter.TryParse("StatusCode > 199", out TypedFieldFilter? tf3) && tf3!.Matches(clefLine), "StatusCode > 199 failed");
Check(TypedFieldFilter.TryParse("StatusCode <= 200", out TypedFieldFilter? tf4) && tf4!.Matches(clefLine), "StatusCode <= 200 failed");
Check(TypedFieldFilter.TryParse("StatusCode < 201", out TypedFieldFilter? tf5) && tf5!.Matches(clefLine), "StatusCode < 201 failed");
Check(TypedFieldFilter.TryParse("StatusCode != 404", out TypedFieldFilter? tf6) && tf6!.Matches(clefLine), "StatusCode != 404 failed");
Check(TypedFieldFilter.TryParse("StatusCode > 300", out TypedFieldFilter? tf7) && !tf7!.Matches(clefLine), "StatusCode > 300 should be false");

// String and Level aliases
Check(TypedFieldFilter.TryParse("Level == \"Information\"", out TypedFieldFilter? tf8) && tf8!.Matches(clefLine), "Level == Information failed");
Check(TypedFieldFilter.TryParse("@l == Information", out TypedFieldFilter? tf9) && tf9!.Matches(clefLine), "@l == Information failed");

// Existence operators
Check(TypedFieldFilter.TryParse("exists(UserId)", out TypedFieldFilter? tf10) && tf10!.Matches(clefLine), "exists(UserId) failed");
Check(TypedFieldFilter.TryParse("UserId exists", out TypedFieldFilter? tf11) && tf11!.Matches(clefLine), "UserId exists failed");
Check(TypedFieldFilter.TryParse("!exists(NonExistentField)", out TypedFieldFilter? tf12) && tf12!.Matches(clefLine), "!exists failed");
Check(TypedFieldFilter.TryParse("NonExistentField !exists", out TypedFieldFilter? tf13) && tf13!.Matches(clefLine), "!exists suffix failed");
Check(TypedFieldFilter.TryParse("exists(NonExistentField)", out TypedFieldFilter? tf14) && !tf14!.Matches(clefLine), "exists on missing field must be false");

// Dot-notation nested filter
Check(TypedFieldFilter.TryParse("Details.Role == Operator", out TypedFieldFilter? tf15) && tf15!.Matches(clefLine), "Nested Details.Role failed");

// Missing/null/type-mismatch semantics
Check(TypedFieldFilter.TryParse("Details.Role > 100", out TypedFieldFilter? tfMismatch) && !tfMismatch!.Matches(clefLine), "Type mismatch (string > number) must evaluate to false");
Check(TypedFieldFilter.TryParse("NonExistentField > 100", out TypedFieldFilter? tfMissing) && !tfMissing!.Matches(clefLine), "Missing field numeric comparison must evaluate to false");
LogLine plainLine = Line(99, "2026-11-06 10:00:00 plain line");
Check(tf10!.Matches(plainLine) == false, "Exists on plain-text line must be false");
Check(tf12!.Matches(plainLine) == true, "!exists on plain-text line must be true");

// 9. CorrelationService: Suggested ID extraction and exact matching
CorrelationService corrService = new();

// A. Structured log line with TraceId
LogLine structuredTraceLine = new()
{
    LineNumber = 10,
    Text = "{\"@t\":\"2026-11-06T10:00:00Z\",\"@tr\":\"4bf92f3577b34da6a3ce929d0e0e4736\",\"@m\":\"Handled request\"}",
    StructuredEvent = JsonLogParser.TryParse("{\"@t\":\"2026-11-06T10:00:00Z\",\"@tr\":\"4bf92f3577b34da6a3ce929d0e0e4736\",\"@m\":\"Handled request\"}")
};
Check(CorrelationService.ExtractSuggestedId(structuredTraceLine) == "4bf92f3577b34da6a3ce929d0e0e4736", "ExtractSuggestedId failed on structured @tr");
Check(CorrelationService.MatchesCorrelationId(structuredTraceLine, "4bf92f3577b34da6a3ce929d0e0e4736"), "MatchesCorrelationId failed on structured @tr");
Check(CorrelationService.MatchesCorrelationId(structuredTraceLine, "4BF92F3577B34DA6A3CE929D0E0E4736"), "MatchesCorrelationId should be case-insensitive");

// B. Plain text lines with token boundaries and substring exclusion
LogLine exactTokenLine = Line(11, "2026-11-06 10:00:01 INFO [req-123] Processed order");
LogLine prefixLine = Line(12, "2026-11-06 10:00:02 INFO [req-12345] Processed other order");
LogLine suffixLine = Line(13, "2026-11-06 10:00:03 INFO [pre-req-123] Processed other order");
LogLine shortTokenLine = Line(14, "2026-11-06 10:00:04 INFO token=abc status=ok");
LogLine shortSubstrLine = Line(15, "2026-11-06 10:00:05 INFO token=abcdef status=ok");

Check(CorrelationService.MatchesCorrelationId(exactTokenLine, "req-123"), "Exact token req-123 should match");
Check(!CorrelationService.MatchesCorrelationId(prefixLine, "req-123"), "req-123 must NOT match req-12345");
Check(!CorrelationService.MatchesCorrelationId(suffixLine, "req-123"), "req-123 must NOT match pre-req-123");
Check(CorrelationService.MatchesCorrelationId(shortTokenLine, "abc"), "Exact token abc should match");
Check(!CorrelationService.MatchesCorrelationId(shortSubstrLine, "abc"), "abc must NOT match abcdef");
Check(!CorrelationService.MatchesCorrelationId(exactTokenLine, ""), "Empty ID must not match");
Check(!CorrelationService.MatchesCorrelationId(exactTokenLine, "   "), "Whitespace ID must not match");

// C. Nested JSON property correlation
LogLine nestedPropLine = new()
{
    LineNumber = 16,
    Text = "{\"@t\":\"2026-11-06T10:00:06Z\",\"Details\":{\"RequestId\":\"req-nested-999\"}}",
    StructuredEvent = JsonLogParser.TryParse("{\"@t\":\"2026-11-06T10:00:06Z\",\"Details\":{\"RequestId\":\"req-nested-999\"}}")
};
Check(CorrelationService.MatchesCorrelationId(nestedPropLine, "req-nested-999"), "Nested JSON RequestId should match");
Check(!CorrelationService.MatchesCorrelationId(nestedPropLine, "req-nested-99"), "Nested JSON substring must not match");

// D. Plain-text GUID and W3C trace regex extraction
LogLine guidLine = Line(17, "2026-11-06 10:00:07 Error occurred in transaction e4b3c2a1-0000-0000-0000-000000000001 details follow");
Check(CorrelationService.ExtractSuggestedId(guidLine) == "e4b3c2a1-0000-0000-0000-000000000001", "ExtractSuggestedId failed on GUID");
LogLine kvLine = Line(18, "2026-11-06 10:00:08 request_id: my-custom-req-555 finished");
Check(CorrelationService.ExtractSuggestedId(kvLine) == "my-custom-req-555", "ExtractSuggestedId failed on key-value request_id");

// 10. CorrelationService: Multi-source chronological merge, bounding, and cancellation
LogLine source1Match1 = Line(1, "2026-11-06 10:00:10 TraceId: t-100 ServiceA start");
LogLine source1NonMatch = Line(2, "2026-11-06 10:00:11 ServiceA heartbeat");
LogLine source1Match2 = Line(3, "2026-11-06 10:00:30 TraceId: t-100 ServiceA done");

LogLine source2Match1 = Line(1, "2026-11-06 10:00:05 TraceId: t-100 ServiceB received");
LogLine source2Match2 = Line(2, "2026-11-06 10:00:20 TraceId: t-100 ServiceB processed");

TimelineSource srcA = new("C:/serviceA.log", "serviceA.log", [source1Match1, source1NonMatch, source1Match2]);
TimelineSource srcB = new("C:/serviceB.log", "serviceB.log", [source2Match1, source2Match2]);

CorrelationResult corrAll = corrService.MergeCorrelated([srcA, srcB], "t-100");
Check(corrAll.TotalMatches == 4, "Total matches count mismatch");
Check(corrAll.Rows.Count == 4, "Merged rows count mismatch");
Check(!corrAll.IsTruncated, "Should not be truncated when under limit");
Check(corrAll.SearchedSourcesCount == 2, "Searched sources count mismatch");

// Verify chronological order: 10:00:05 (B#1) -> 10:00:10 (A#1) -> 10:00:20 (B#2) -> 10:00:30 (A#2)
Check(ReferenceEquals(corrAll.Rows[0].OriginalLine, source2Match1), "Earliest match should be B#1 (10:00:05)");
Check(ReferenceEquals(corrAll.Rows[1].OriginalLine, source1Match1), "Second match should be A#1 (10:00:10)");
Check(ReferenceEquals(corrAll.Rows[2].OriginalLine, source2Match2), "Third match should be B#2 (10:00:20)");
Check(ReferenceEquals(corrAll.Rows[3].OriginalLine, source1Match2), "Fourth match should be A#2 (10:00:30)");
Check(corrAll.Rows[0].SourceFileName == "serviceB.log", "SourceFileName lost");
Check(corrAll.Rows[1].SourceFileName == "serviceA.log", "SourceFileName lost");

// Verify bounding and truncation
CorrelationResult corrBounded = corrService.MergeCorrelated([srcA, srcB], "t-100", maxResults: 2);
Check(corrBounded.TotalMatches == 4, "TotalMatches should still report 4");
Check(corrBounded.Rows.Count == 2, "Rows should be bounded to 2");
Check(corrBounded.IsTruncated, "IsTruncated must be true");

// Verify cancellation
using CancellationTokenSource corrCts = new();
corrCts.Cancel();
try
{
    corrService.MergeCorrelated([srcA, srcB], "t-100", ct: corrCts.Token);
    throw new Exception("Correlation merge did not throw OperationCanceledException on cancelled token");
}
catch (OperationCanceledException) { }

// 11. WorkspaceService: Atomic saving, schema versioning, round-trip, legacy format and missing files
WorkspaceService wsService = new();
string testWsDir = Path.Combine(Path.GetTempPath(), "LogTailPro-WsTest-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testWsDir);
try
{
    string wsFile = Path.Combine(testWsDir, "test.ltpw");
    string realLogFile = Path.Combine(testWsDir, "real.log");
    File.WriteAllText(realLogFile, "2026-11-06 10:00:00 line 1\n2026-11-06 10:00:01 line 2\n");

    // A. Round-trip version 2 workspace with file, timeline, and correlation tabs
    Workspace wsV2 = new()
    {
        Name = "DevWorkspace",
        ActiveTabId = realLogFile,
        ActiveTabIndex = 0,
        HighlightRules = [new HighlightRule { Pattern = "error", ForegroundColor = "#FF0000" }],
        DockLayoutXml = "<LayoutRoot><Panels/></LayoutRoot>",
        Tabs = 
        [
            new WorkspaceTab
            {
                TabType = WorkspaceTabType.File,
                FilePath = realLogFile,
                FileName = "real.log",
                SearchText = "line",
                IsRegex = false,
                IsCaseSensitive = true,
                FilterMode = FilterMode.IncludeMatching,
                BookmarkedLines = [1, 2],
                IsFollowTail = false,
                ScrollVerticalOffset = 42.5,
                IsStructuredViewEnabled = true
            },
            new WorkspaceTab
            {
                TabType = WorkspaceTabType.Timeline,
                FileName = "Combined Timeline",
                SourceFilePaths = [realLogFile, "C:/fake2.log"],
                IsFollowTail = true
            },
            new WorkspaceTab
            {
                TabType = WorkspaceTabType.Correlation,
                FileName = "Correlate: req-100",
                CorrelationId = "req-100",
                SourceFilePaths = [realLogFile]
            }
        ]
    };

    wsService.SaveAsync(wsFile, wsV2).GetAwaiter().GetResult();
    Check(File.Exists(wsFile), "Workspace file was not created");

    WorkspaceLoadResult loadRes = wsService.LoadAsync(wsFile).GetAwaiter().GetResult();
    Check(loadRes.Success && loadRes.Workspace != null, "Failed to load saved workspace");
    Workspace loadedWs = loadRes.Workspace!;
    Check(loadedWs.SchemaVersion == 2, "SchemaVersion mismatch");
    Check(loadedWs.Name == "DevWorkspace", "Workspace name mismatch");
    Check(loadedWs.ActiveTabId == realLogFile, "ActiveTabId mismatch");
    Check(loadedWs.Tabs.Count == 3, "Tabs count mismatch");
    Check(loadedWs.Tabs[0].TabType == WorkspaceTabType.File, "Tab 0 type mismatch");
    Check(loadedWs.Tabs[0].BookmarkedLines.Count == 2, "Tab 0 bookmarks count mismatch");
    Check(loadedWs.Tabs[0].ScrollVerticalOffset == 42.5, "Tab 0 scroll offset mismatch");
    Check(loadedWs.Tabs[1].TabType == WorkspaceTabType.Timeline, "Tab 1 type mismatch");
    Check(loadedWs.Tabs[1].SourceFilePaths.Count == 2, "Tab 1 source paths count mismatch");
    Check(loadedWs.Tabs[2].TabType == WorkspaceTabType.Correlation, "Tab 2 type mismatch");
    Check(loadedWs.Tabs[2].CorrelationId == "req-100", "Tab 2 CorrelationId mismatch");
    Check(loadedWs.HighlightRules?.Count == 1, "HighlightRules count mismatch");
    Check(loadedWs.DockLayoutXml == "<LayoutRoot><Panels/></LayoutRoot>", "DockLayoutXml mismatch");

    // B. Missing files detection
    string missingWsFile = Path.Combine(testWsDir, "missing.ltpw");
    Workspace wsMissing = new()
    {
        Tabs = [new WorkspaceTab { TabType = WorkspaceTabType.File, FilePath = "C:/non_existent_12345.log" }]
    };
    wsService.SaveAsync(missingWsFile, wsMissing).GetAwaiter().GetResult();
    WorkspaceLoadResult missingRes = wsService.LoadAsync(missingWsFile).GetAwaiter().GetResult();
    Check(missingRes.Success, "Missing files workspace should still load successfully");
    Check(missingRes.MissingFiles.Count == 1 && missingRes.MissingFiles[0] == "C:/non_existent_12345.log", "Missing files list did not detect missing file");

    // C. Legacy format (Schema 1) upgrade
    string legacyWsFile = Path.Combine(testWsDir, "legacy.ltpw");
    string legacyJson = "{\"Name\":\"OldWorkspace\",\"ActiveTabIndex\":0,\"Tabs\":[{\"FilePath\":\"" + realLogFile.Replace("\\", "\\\\") + "\",\"SearchText\":\"old\",\"IsRegex\":false,\"IsCaseSensitive\":false,\"FilterMode\":0,\"BookmarkedLines\":[1]}]}";
    File.WriteAllText(legacyWsFile, legacyJson);
    WorkspaceLoadResult legacyRes = wsService.LoadAsync(legacyWsFile).GetAwaiter().GetResult();
    Check(legacyRes.Success && legacyRes.Workspace != null, "Failed to load legacy workspace");
    Check(legacyRes.Workspace!.SchemaVersion == 2, "Legacy workspace not upgraded to schema 2");
    Check(legacyRes.Workspace.Tabs[0].TabType == WorkspaceTabType.File, "Legacy tab not assigned WorkspaceTabType.File");
    Check(legacyRes.Workspace.Tabs[0].SearchText == "old", "Legacy tab SearchText lost");

    // D. Interrupted save preserves previous file intact
    string atomicWsFile = Path.Combine(testWsDir, "atomic.ltpw");
    File.WriteAllText(atomicWsFile, "ORIGINAL CONTENT PRESERVED");
    using CancellationTokenSource cancelCts = new();
    cancelCts.Cancel();
    try
    {
        wsService.SaveAsync(atomicWsFile, wsV2, cancelCts.Token).GetAwaiter().GetResult();
        throw new Exception("Atomic save did not throw OperationCanceledException");
    }
    catch (OperationCanceledException) { }
    Check(File.ReadAllText(atomicWsFile) == "ORIGINAL CONTENT PRESERVED", "Interrupted save damaged previous file");

    // E. Corrupt JSON error reporting
    string corruptWsFile = Path.Combine(testWsDir, "corrupt.ltpw");
    File.WriteAllText(corruptWsFile, "{ \"Name\": \"BadJson\", unquoted ");
    WorkspaceLoadResult corruptRes = wsService.LoadAsync(corruptWsFile).GetAwaiter().GetResult();
    Check(!corruptRes.Success && corruptRes.Workspace == null, "Corrupt JSON should fail gracefully");
    Check(!string.IsNullOrEmpty(corruptRes.ErrorMessage), "Corrupt JSON should report error message");
}
finally
{
    try { Directory.Delete(testWsDir, true); } catch { }
}

// â”€â”€ Phase 5 Multiline Exception Grouping Tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
MultilineGroupingService groupingService = new();

// 1. .NET Stack trace with nested exceptions, boundary markers, and blank lines
List<LogLine> dotNetLines =
[
    Line(1, "2026-11-06 10:00:00 [Error] Failed processing request"),
    Line(2, "System.NullReferenceException: Object reference not set to an instance of an object."),
    Line(3, "   at System.Data.SqlClient.SqlConnection.Open()"),
    Line(4, "   at LogTailPro.Data.Repository.GetData() in C:\\src\\Repo.cs:line 45"),
    Line(5, ""),
    Line(6, "---> System.InvalidOperationException: Connection string invalid."),
    Line(7, "   at System.Data.ProviderBase.DbConnectionFactory.GetConnection()"),
    Line(8, "--- End of inner exception stack trace ---"),
    Line(9, "   at LogTailPro.App.Main()")
];

groupingService.GroupLines(dotNetLines);
Check(!dotNetLines[0].IsContinuation && dotNetLines[0].Continuations.Count == 8, ".NET exception header did not collect all 8 continuations");
Check(dotNetLines[0].HasContinuations, "HasContinuations should be true");
Check(dotNetLines[0].ContinuationBadge == "+8 frames", "ContinuationBadge format mismatch");
Check(dotNetLines.Skip(1).All(l => l.IsContinuation && ReferenceEquals(l.GroupParent, dotNetLines[0])), "Continuation lines must point to root header");
Check(dotNetLines[4].IsContinuation, "Blank line inside stack trace was not preserved as continuation");

// 2. Generic indented lines must NOT be grouped as exceptions
List<LogLine> indentedLines =
[
    Line(1, "Configuration loaded:"),
    Line(2, "  server: localhost"),
    Line(3, "  port: 8080"),
    Line(4, "    max_connections: 100")
];
groupingService.GroupLines(indentedLines);
Check(indentedLines.All(l => !l.IsContinuation && !l.HasContinuations), "Generic indented lines were incorrectly classified as exception continuations");

// 3. Java Exception frames with Caused by and omitted frames
List<LogLine> javaLines =
[
    Line(1, "Exception in thread \"main\" java.lang.RuntimeException: Database failed"),
    Line(2, "\tat org.example.Service.run(Service.java:42)"),
    Line(3, "Caused by: java.io.IOException: Stream closed"),
    Line(4, "\t... 24 common frames omitted")
];
groupingService.GroupLines(javaLines);
Check(!javaLines[0].IsContinuation && javaLines[0].Continuations.Count == 3, "Java exception did not group 3 continuation frames");
Check(javaLines[1].IsContinuation && javaLines[2].IsContinuation && javaLines[3].IsContinuation, "Java continuation frames not marked");

// 4. Python Traceback frames
List<LogLine> pyLines =
[
    Line(1, "Traceback (most recent call last):"),
    Line(2, "  File \"app.py\", line 42, in process"),
    Line(3, "    result = compute()"),
    Line(4, "During handling of the above exception, another exception occurred:"),
    Line(5, "  File \"db.py\", line 10, in connect")
];
groupingService.GroupLines(pyLines);
Check(!pyLines[0].IsContinuation && pyLines[0].Continuations.Count == 4, "Python traceback did not group all frames");

// 5. Final event without successor
List<LogLine> finalLines =
[
    Line(1, "2026-11-06 10:00:00 [Info] Startup complete"),
    Line(2, "2026-11-06 10:00:01 [Error] System.Exception: Crash"),
    Line(3, "   at System.IO.File.Open()")
];
groupingService.GroupLines(finalLines);
Check(!finalLines[0].HasContinuations, "First line should not have continuations");
Check(!finalLines[1].IsContinuation && finalLines[1].Continuations.Count == 1, "Final event did not attach continuation frame");
Check(finalLines[2].IsContinuation, "Final frame was not marked continuation");

// 6. Partial continuation when tail starts mid-trace
List<LogLine> partialLines =
[
    Line(100, "   at System.Data.SqlClient.SqlConnection.Open()"),
    Line(101, "2026-11-06 10:00:02 [Info] Next event")
];
groupingService.GroupLines(partialLines, isLoadedFromBeginning: false);
Check(partialLines[0].IsPartialContinuation, "Mid-trace line was not flagged as IsPartialContinuation");
Check(!partialLines[0].IsContinuation, "Partial continuation without header is root");
Check(!partialLines[1].IsContinuation, "Next event after partial continuation is separate root");

// 7. Live append with AttachLine
List<LogLine> liveLines =
[
    Line(1, "2026-11-06 10:00:00 [Error] System.Exception: Live failure")
];
groupingService.GroupLines(liveLines);
LogLine newFrame1 = Line(2, "   at App.Service.DoWork()");
groupingService.AttachLine(liveLines, newFrame1);
Check(liveLines.Count == 2, "Live lines count mismatch");
Check(liveLines[0].Continuations.Count == 1 && ReferenceEquals(liveLines[0].Continuations[0], newFrame1), "New frame not attached to header");
Check(newFrame1.IsContinuation && ReferenceEquals(newFrame1.GroupParent, liveLines[0]), "New frame parent mismatch");

LogLine normalLine = Line(3, "2026-11-06 10:00:01 [Info] Recovered");
groupingService.AttachLine(liveLines, normalLine);
Check(liveLines.Count == 3, "Normal line not added to list");
Check(!normalLine.IsContinuation && normalLine.GroupParent == null, "Normal line incorrectly attached to exception group");
Check(liveLines[0].Continuations.Count == 1, "Header continuations modified after closing group");

// 8. Correlation ID matching on continuation frames
LogLine corrHeader = Line(1, "2026-11-06 10:00:00 [Error] Unhandled exception occurred");
LogLine corrFrame = Line(2, "   at App.Service.Execute(RequestId: 550e8400-e29b-41d4-a716-446655440000)");
corrHeader.Continuations.Add(corrFrame);
corrFrame.IsContinuation = true;
corrFrame.GroupParent = corrHeader;

string? extractedId = CorrelationService.ExtractSuggestedId(corrHeader);
Check(extractedId == "550e8400-e29b-41d4-a716-446655440000", "ExtractSuggestedId did not find ID in continuation frame");
Check(CorrelationService.MatchesCorrelationId(corrHeader, "550e8400-e29b-41d4-a716-446655440000"), "MatchesCorrelationId did not match ID in continuation frame");

// â”€â”€ Phase 6 Error Timeline Chart Tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
ErrorTimelineChartService chartService = new();

// 1. Severity Normalization
LogLine errLine = Line(1, "2026-11-06 10:00:00 [Error] Database failed");
LogLine warnLine = Line(2, "2026-11-06 10:01:00 [Warning] High CPU usage");
LogLine infoLine = Line(3, "2026-11-06 10:02:00 [Info] Processing finished");
LogLine otherLine = Line(4, "2026-11-06 10:03:00 Background worker running");

Check(ErrorTimelineChartService.NormalizeSeverity(errLine) == LogSeverity.Error, "Plain text [Error] normalization failed");
Check(ErrorTimelineChartService.NormalizeSeverity(warnLine) == LogSeverity.Warning, "Plain text [Warning] normalization failed");
Check(ErrorTimelineChartService.NormalizeSeverity(infoLine) == LogSeverity.Information, "Plain text [Info] normalization failed");
Check(ErrorTimelineChartService.NormalizeSeverity(otherLine) == LogSeverity.Other, "Plain text neutral normalization failed");

// Structured JSON severity normalization
LogLine clefFatal = Line(5, "{\"@t\":\"2026-11-06T10:00:00Z\",\"@l\":\"Fatal\",\"@m\":\"Kernel panic\"}"); clefFatal.StructuredEvent = JsonLogParser.TryParse("{\"@t\":\"2026-11-06T10:00:00Z\",\"@l\":\"Fatal\",\"@m\":\"Kernel panic\"}");
LogLine clefDebug = Line(6, "{\"@t\":\"2026-11-06T10:00:00Z\",\"@l\":\"Debug\",\"@m\":\"Trace step\"}"); clefDebug.StructuredEvent = JsonLogParser.TryParse("{\"@t\":\"2026-11-06T10:00:00Z\",\"@l\":\"Debug\",\"@m\":\"Trace step\"}");
Check(ErrorTimelineChartService.NormalizeSeverity(clefFatal) == LogSeverity.Error, "CLEF Fatal should normalize to Error");
Check(ErrorTimelineChartService.NormalizeSeverity(clefDebug) == LogSeverity.Information, "CLEF Debug should normalize to Information");

// 2. Multiline Grouping does NOT inflate event counts
LogLine multilineRoot = Line(10, "2026-11-06 10:10:00 [Error] Critical crash");
LogLine frame1 = Line(11, "   at App.Service.Run()"); frame1.IsContinuation = true; frame1.GroupParent = multilineRoot;
LogLine frame2 = Line(12, "   at App.Main()"); frame2.IsContinuation = true; frame2.GroupParent = multilineRoot;
multilineRoot.Continuations.Add(frame1);
multilineRoot.Continuations.Add(frame2);

ErrorTimelineChartData groupData = chartService.BuildChartData([multilineRoot, frame1, frame2], "test.log");
Check(groupData.TotalEvents == 1, $"Expected 1 total event for grouped exception, got {groupData.TotalEvents}");
Check(groupData.TotalErrors == 1, $"Expected 1 error for grouped exception, got {groupData.TotalErrors}");

// 3. Adaptive time bucketing and known bucket counts
List<LogLine> timedSeries =
[
    Line(1, "2026-11-06T10:00:00Z [Error] Err 1"),
    Line(2, "2026-11-06T10:00:15Z [Warning] Warn 1"),
    Line(3, "2026-11-06T10:01:00Z [Info] Info 1"),
    Line(4, "2026-11-06T10:01:45Z [Info] Info 2"),
    Line(5, "2026-11-06T10:02:00Z [Error] Err 2")
];
ErrorTimelineChartData timedData = chartService.BuildChartData(timedSeries, "app.log");
Check(timedData.TotalEvents == 5, "Total events count mismatch");
Check(timedData.TotalErrors == 2, "Total errors count mismatch");
Check(timedData.TotalWarnings == 1, "Total warnings count mismatch");
Check(timedData.TotalInfos == 2, "Total info count mismatch");
Check(timedData.UndatedCount == 0, "Undated count should be 0");
Check(timedData.Buckets.Count > 0, "Buckets should not be empty");
int sumAcrossBuckets = timedData.Buckets.Sum(b => b.TotalCount);
Check(sumAcrossBuckets == 5, $"Sum across buckets ({sumAcrossBuckets}) should equal total dated events (5)");

// 4. Undated events counted separately without invented dates
List<LogLine> mixedDatedAndUndated =
[
    Line(1, "2026-11-06T10:00:00Z [Info] Valid timestamp"),
    Line(2, "Undated plain text record without date"),
    Line(3, "[Error] Undated error occurred")
];
ErrorTimelineChartData mixedData = chartService.BuildChartData(mixedDatedAndUndated, "mixed.log");
Check(mixedData.TotalEvents == 3, "Total mixed events mismatch");
Check(mixedData.UndatedCount == 2, "Undated count mismatch");
Check(mixedData.UndatedErrors == 1, "Undated errors count mismatch");
Check(mixedData.Buckets.Sum(b => b.TotalCount) == 1, "Only 1 event should be plotted on time axis");

// 5. Large data time duration bounds bucket count
TimeSpan hugeDuration = TimeSpan.FromDays(365);
TimeSpan adaptiveInterval = ErrorTimelineChartService.CalculateBucketInterval(hugeDuration);
int estimatedBuckets = (int)(hugeDuration.TotalSeconds / adaptiveInterval.TotalSeconds);
Check(estimatedBuckets <= 120, $"Bucket count for 1-year duration should be bounded <= 120, got {estimatedBuckets}");

// 6. Cancellation check
using CancellationTokenSource chartCts = new();
chartCts.Cancel();
try
{
    chartService.BuildChartData(timedSeries, "app.log", chartCts.Token);
    throw new Exception("BuildChartData did not respect cancellation token");
}
catch (OperationCanceledException) { }

Console.WriteLine("All service checks passed: Combined Timeline, Structured JSON, Request/Trace ID Correlation, Saved Workspaces, Multiline Exception Grouping, and Error Timeline Chart.");
