using System.Text.RegularExpressions;
using LogTailPro.Models;

namespace LogTailPro.Services;

/// <summary>
/// Detects event starts and continuation lines for .NET stack traces, Java exceptions, Python tracebacks,
/// and groups them into expandable multiline events.
/// </summary>
public sealed class MultilineGroupingService
{
    // .NET stack trace frame: '   at System.Data.SqlClient.SqlConnection.Open()'
    private static readonly Regex DotNetStackFrameRegex = new(
        @"^\s+at\s+[\w\.\<\>\$`]+\s*\(.*?\)",
        RegexOptions.Compiled);

    // .NET inner exception or frame boundary:
    // '--- End of inner exception stack trace ---'
    // '--- End of stack trace from previous location where exception was thrown ---'
    // '---> System.InvalidOperationException: ...'
    private static readonly Regex DotNetBoundaryRegex = new(
        @"^\s*(?:--- End of |--->\s+\w[\w\.]*Exception:)",
        RegexOptions.Compiled);

    // Java exception frame:
    // '   at org.springframework.boot.SpringApplication.run(SpringApplication.java:315)'
    // 'Caused by: java.lang.NullPointerException'
    // '   ... 24 common frames omitted'
    // '   ... 10 more'
    private static readonly Regex JavaExceptionRegex = new(
        @"^\s*(?:Caused by:|\.\.\.\s+\d+\s+(?:more|common frames omitted))",
        RegexOptions.Compiled);

    // Python traceback frame:
    // '  File "app.py", line 42, in process'
    // 'During handling of the above exception, another exception occurred:'
    // 'Traceback (most recent call last):'
    private static readonly Regex PythonTracebackRegex = new(
        @"^\s*(?:File\s+""[^""]+"",\s+line\s+\d+|During handling of the above exception|Traceback \(most recent call last\):)",
        RegexOptions.Compiled);

    // Exception header keyword indicator:
    // 'System.Exception:', 'NullReferenceException', 'java.lang.RuntimeException', 'Error: ', etc.
    private static readonly Regex ExceptionKeywordRegex = new(
        @"(?:Exception|Error|Fault|Crash|Traceback):\s+|\b[a-zA-Z0-9_\.]*(?:Exception|Error|Fault|Crash|Traceback)\b",
        RegexOptions.Compiled);

    // Exception type header:
    // 'System.NullReferenceException: Object reference not set...'
    // 'java.lang.RuntimeException: Database failed'
    // 'ZeroDivisionError: division by zero'
    private static readonly Regex ExceptionTypeHeaderRegex = new(
        @"^\s*[a-zA-Z0-9_\.]+(?:Exception|Error)(?:<[^>]+>)?:\s*",
        RegexOptions.Compiled);

    /// <summary>
    /// Checks whether a line of text is a recognized stack trace continuation frame or boundary.
    /// Does not classify generic indented text as a continuation.
    /// </summary>
    public static bool IsContinuationLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        return DotNetStackFrameRegex.IsMatch(text)
            || DotNetBoundaryRegex.IsMatch(text)
            || JavaExceptionRegex.IsMatch(text)
            || PythonTracebackRegex.IsMatch(text);
    }

    /// <summary>
    /// Checks whether a line appears to initiate an exception event with following continuations.
    /// </summary>
    public static bool HasExceptionIndicator(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return ExceptionKeywordRegex.IsMatch(text);
    }

    /// <summary>
    /// Groups a collection of loaded lines into multiline events where applicable.
    /// Marks continuation lines with <see cref="LogLine.IsContinuation"/> and attaches them
    /// to <see cref="LogLine.Continuations"/> on the root event line.
    /// </summary>
    public void GroupLines(IList<LogLine> lines, bool isLoadedFromBeginning = true)
    {
        if (lines.Count == 0) return;

        LogLine? currentHeader = null;
        bool inExceptionGroup = false;

        for (int i = 0; i < lines.Count; i++)
        {
            LogLine line = lines[i];

            // If line is already a structured JSON event, Serilog compact/JSON CLEF stores its
            // exception inside the @x property, so JSON lines are complete events in themselves.
            if (line.HasStructuredEvent)
            {
                currentHeader = null;
                inExceptionGroup = false;
                continue;
            }

            bool isCont = IsContinuationLine(line.Text);

            // Handle exception type/message line (e.g. 'System.NullReferenceException: ...')
            // when it follows an error log header and is followed by stack trace frames
            if (!isCont && inExceptionGroup && ExceptionTypeHeaderRegex.IsMatch(line.Text) && i + 1 < lines.Count && IsContinuationLine(lines[i + 1].Text))
            {
                isCont = true;
            }

            // Handle blank lines inside a stack trace:
            // if we are inside an exception group and the next line is also a continuation frame
            if (!isCont && inExceptionGroup && string.IsNullOrWhiteSpace(line.Text) && i + 1 < lines.Count && IsContinuationLine(lines[i + 1].Text))
            {
                isCont = true;
            }

            // Handle Python code snippet immediately following a File "..." traceback frame
            if (!isCont && inExceptionGroup && i > 0 && PythonTracebackRegex.IsMatch(lines[i - 1].Text) && line.Text.StartsWith("    "))
            {
                isCont = true;
            }

            if (isCont)
            {
                if (currentHeader != null)
                {
                    line.IsContinuation = true;
                    line.GroupParent = currentHeader;
                    currentHeader.Continuations.Add(line);
                    inExceptionGroup = true;
                    continue;
                }
                else if (i == 0 && !isLoadedFromBeginning)
                {
                    // Tail started in the middle of a stack trace
                    line.IsPartialContinuation = true;
                }
            }

            // Not a continuation or not attached to current header -> new event root
            currentHeader = line;
            currentHeader.Continuations.Clear();
            currentHeader.IsContinuation = false;
            currentHeader.GroupParent = null;

            // Check if this event header starts an exception that expects continuation frames
            inExceptionGroup = HasExceptionIndicator(line.Text);
        }
    }

    /// <summary>
    /// Appends a new incoming line to the existing lines, attaching it as a continuation
    /// if it belongs to the previous exception group.
    /// </summary>
    public void AttachLine(IList<LogLine> existingLines, LogLine newLine)
    {
        if (existingLines.Count == 0)
        {
            existingLines.Add(newLine);
            return;
        }

        LogLine lastLine = existingLines[^1];
        LogLine? activeHeader = lastLine.IsContinuation
            ? lastLine.GroupParent
            : (lastLine.HasContinuations || HasExceptionIndicator(lastLine.Text) ? lastLine : null);

        bool isCont = IsContinuationLine(newLine.Text);
        if (!isCont && activeHeader != null && ExceptionTypeHeaderRegex.IsMatch(newLine.Text))
        {
            isCont = true;
        }
        else if (!isCont && activeHeader != null && PythonTracebackRegex.IsMatch(lastLine.Text) && newLine.Text.StartsWith("    "))
        {
            isCont = true;
        }

        if (activeHeader != null && isCont)
        {
            newLine.IsContinuation = true;
            newLine.GroupParent = activeHeader;
            activeHeader.Continuations.Add(newLine);
            existingLines.Add(newLine);
        }
        else if (isCont)
        {
            newLine.IsContinuation = true;
            newLine.GroupParent = lastLine;
            lastLine.Continuations.Add(newLine);
            existingLines.Add(newLine);
        }
        else
        {
            newLine.IsContinuation = false;
            newLine.GroupParent = null;
            existingLines.Add(newLine);
        }
    }
}
