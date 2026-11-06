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
Console.WriteLine("Timeline service checks passed: timezone ordering, ties, source identity, malformed/undated input, reconciliation and cancellation.");
