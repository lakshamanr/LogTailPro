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

Console.WriteLine("Timeline and Structured JSON service checks passed: timezone ordering, Serilog CLEF, malformed handling, nested properties and typed field filters.");
