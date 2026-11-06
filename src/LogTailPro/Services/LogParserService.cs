using System.IO;
using System.Text;

namespace LogTailPro.Services;

/// <summary>Result returned by the file-load operation.</summary>
public sealed class TailLoadResult
{
    /// <summary>Lines read from the file (oldest first).</summary>
    public List<string> Lines { get; init; } = new();

    /// <summary>Detected encoding.</summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <summary>Total byte length of the file at read time.</summary>
    public long FileSize { get; init; }

    /// <summary>Byte offset where the first returned line begins in the file.</summary>
    public long TailStartByteOffset { get; init; }

    /// <summary>
    /// Byte offset just after the last byte that was read. File watching must continue from here
    /// (NOT from <see cref="TailStartByteOffset"/>) or the whole loaded range is delivered twice.
    /// </summary>
    public long ReadEndOffset { get; init; }

    /// <summary>1-based number of the first returned line within the whole file (absolute, not tail-relative).</summary>
    public long FirstLineNumber { get; init; } = 1;

    /// <summary>Bytes at the start of the file that were not loaded (0 when the whole file was read).</summary>
    public long SkippedBytes { get; init; }
}

/// <summary>
/// Streams a log file forward in 1 MB chunks using an incremental decoder, so multi-byte characters
/// split across chunks are decoded correctly. Optional limits (max lines / max bytes) keep very
/// large files from exhausting memory; both can be disabled (0) to load everything.
/// </summary>
public sealed class LogParserService
{
    private const int ChunkSize = 1 << 20; // 1 MB

    /// <summary>Loads lines from <paramref name="filePath"/>.</summary>
    /// <param name="filePath">File to read (opened with full sharing so writers are never blocked).</param>
    /// <param name="maxLines">Keep only the last N lines; 0 or less = unlimited.</param>
    /// <param name="maxBytes">Only read the last N bytes (aligned to a line start); 0 or less = whole file.</param>
    /// <param name="progress">Receives 0–100.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<TailLoadResult> LoadTailAsync(
        string filePath,
        int maxLines,
        long maxBytes,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        Encoding encoding = await DetectEncodingAsync(filePath, ct);
        int bomLength = encoding.Preamble.Length;

        using FileStream stream = new(
            filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: ChunkSize, useAsync: true);

        long fileSize = stream.Length;
        if (fileSize <= bomLength)
        {
            progress?.Report(100);
            return new TailLoadResult
            {
                Encoding = encoding,
                FileSize = fileSize,
                TailStartByteOffset = fileSize,
                ReadEndOffset = fileSize
            };
        }

        long start = bomLength;
        long baseLine = 1;
        bool byteNewline = encoding is not UnicodeEncoding; // UTF-16 is always read whole (newline is 2 bytes)

        // Optional byte cap: begin at a line boundary near (fileSize - maxBytes) and count the lines
        // we skip so that line numbers stay absolute.
        if (maxBytes > 0 && byteNewline && fileSize - bomLength > maxBytes)
        {
            long cut = fileSize - maxBytes;
            start = await FindLineStartAsync(stream, cut, fileSize, ct);
            baseLine = 1 + await CountNewlinesAsync(stream, start, ct);
        }

        long remaining = fileSize - start;
        stream.Seek(start, SeekOrigin.Begin);

        Decoder decoder = encoding.GetDecoder();
        byte[] buffer = new byte[ChunkSize];
        char[] chars = new char[encoding.GetMaxCharCount(ChunkSize)];
        StringBuilder pending = new();

        Queue<string>? ring = maxLines > 0 ? new Queue<string>() : null;
        List<string>? list = maxLines > 0 ? null : new List<string>((int)Math.Min(remaining / 60 + 16, 4_000_000));
        long dropped = 0;
        long readTotal = 0;

        void Emit(string line)
        {
            if (ring != null)
            {
                ring.Enqueue(line);
                if (ring.Count > maxLines) { ring.Dequeue(); dropped++; }
            }
            else
            {
                list!.Add(line);
            }
        }

        int lastPercent = -1;
        while (readTotal < remaining)
        {
            ct.ThrowIfCancellationRequested();

            int want = (int)Math.Min(buffer.Length, remaining - readTotal);
            int read = await stream.ReadAsync(buffer.AsMemory(0, want), ct);
            if (read <= 0) break;
            readTotal += read;

            int charCount = decoder.GetChars(buffer, 0, read, chars, 0, flush: readTotal >= remaining);
            SplitLines(chars, charCount, pending, Emit);

            int percent = (int)(readTotal * 100 / remaining);
            if (percent != lastPercent)
            {
                lastPercent = percent;
                progress?.Report(percent);
            }
        }

        // A final line without a trailing newline is still a line.
        if (pending.Length > 0)
            Emit(pending.ToString().TrimEnd('\r'));

        progress?.Report(100);

        return new TailLoadResult
        {
            Lines = ring != null ? ring.ToList() : list!,
            Encoding = encoding,
            FileSize = fileSize,
            TailStartByteOffset = start,
            ReadEndOffset = start + readTotal,
            FirstLineNumber = baseLine + dropped,
            SkippedBytes = start > bomLength ? start : 0
        };
    }

    /// <summary>
    /// Splits decoded characters into lines. Kept synchronous because spans cannot live across awaits.
    /// An unfinished line is carried over in <paramref name="pending"/> to the next chunk.
    /// </summary>
    private static void SplitLines(char[] chars, int count, StringBuilder pending, Action<string> emit)
    {
        ReadOnlySpan<char> span = chars.AsSpan(0, count);
        while (true)
        {
            int nl = span.IndexOf('\n');
            if (nl < 0)
            {
                pending.Append(span);
                return;
            }

            ReadOnlySpan<char> part = span[..nl];
            if (pending.Length == 0)
            {
                emit(part.TrimEnd('\r').ToString());
            }
            else
            {
                pending.Append(part);
                emit(pending.ToString().TrimEnd('\r'));
                pending.Clear();
            }
            span = span[(nl + 1)..];
        }
    }

    /// <summary>Returns the offset of the first line that begins at or after <paramref name="cut"/>.</summary>
    private static async Task<long> FindLineStartAsync(FileStream stream, long cut, long fileSize, CancellationToken ct)
    {
        byte[] buf = new byte[64 * 1024];
        long pos = Math.Max(0, cut - 1); // include the byte before cut: if it is a newline, cut is already a line start
        while (pos < fileSize)
        {
            ct.ThrowIfCancellationRequested();
            stream.Seek(pos, SeekOrigin.Begin);
            int read = await stream.ReadAsync(buf.AsMemory(0, (int)Math.Min(buf.Length, fileSize - pos)), ct);
            if (read <= 0) break;
            int idx = buf.AsSpan(0, read).IndexOf((byte)'\n');
            if (idx >= 0) return pos + idx + 1;
            pos += read;
        }
        return cut; // no newline found: just start at the cut
    }

    /// <summary>Counts newline bytes in [0, <paramref name="end"/>) using vectorised span counting.</summary>
    private static async Task<long> CountNewlinesAsync(FileStream stream, long end, CancellationToken ct)
    {
        byte[] buf = new byte[4 * 1024 * 1024];
        long count = 0, pos = 0;
        stream.Seek(0, SeekOrigin.Begin);
        while (pos < end)
        {
            ct.ThrowIfCancellationRequested();
            int read = await stream.ReadAsync(buf.AsMemory(0, (int)Math.Min(buf.Length, end - pos)), ct);
            if (read <= 0) break;
            count += buf.AsSpan(0, read).Count((byte)'\n');
            pos += read;
        }
        return count;
    }

    /// <summary>
    /// Detects the file encoding from its BOM.
    /// Defaults to UTF-8 without BOM if no BOM is present.
    /// </summary>
    public async Task<Encoding> DetectEncodingAsync(string filePath, CancellationToken ct)
    {
        try
        {
            using FileStream fs = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            byte[] bom = new byte[4];
            int read = await fs.ReadAsync(bom.AsMemory(0, 4), ct);

            if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;
            if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode; // UTF-16 LE
            if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;
        }
        catch
        {
            // fall through to the default
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}
