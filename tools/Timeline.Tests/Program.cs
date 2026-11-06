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

Console.WriteLine("All service checks passed: Combined Timeline, Structured JSON, and Request/Trace ID Correlation.");
