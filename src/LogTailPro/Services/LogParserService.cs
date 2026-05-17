using System.IO;
using System.Text;

namespace LogTailPro.Services;

/// <summary>Result returned by the tail-load operation.</summary>
public sealed class TailLoadResult
{
    /// <summary>Lines read from the tail of the file (oldest first).</summary>
    public List<string> Lines { get; init; } = new();

    /// <summary>Detected encoding.</summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <summary>Total byte length of the file at read time.</summary>
    public long FileSize { get; init; }

    /// <summary>Byte offset where the first returned line begins in the file.</summary>
    public long TailStartByteOffset { get; init; }
}

/// <summary>
/// Reads the tail of a log file by seeking backwards in 64 KB chunks.
/// Never loads the full file into memory — suitable for files of any size.
/// </summary>
public sealed class LogParserService
{
    private const int ChunkSize = 65_536; // 64 KB

    /// <summary>
    /// Reads the last <paramref name="maxLines"/> lines from <paramref name="filePath"/>
    /// by reading backwards in chunks. Reports progress from 0–100.
    /// </summary>
    public async Task<TailLoadResult> LoadTailAsync(
        string filePath,
        int maxLines,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        Encoding encoding = await DetectEncodingAsync(filePath, ct);
        long fileSize = new FileInfo(filePath).Length;

        if (fileSize == 0)
        {
            return new TailLoadResult
            {
                Lines = new List<string>(),
                Encoding = encoding,
                FileSize = 0,
                TailStartByteOffset = 0
            };
        }

        using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: ChunkSize,
            useAsync: true);

        List<string> lines = new(maxLines);
        byte[] buffer = new byte[ChunkSize];
        string leftover = string.Empty;
        long position = fileSize;

        while (position > 0 && lines.Count < maxLines)
        {
            ct.ThrowIfCancellationRequested();

            long readFrom = Math.Max(0, position - ChunkSize);
            int bytesToRead = (int)(position - readFrom);

            stream.Seek(readFrom, SeekOrigin.Begin);
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bytesToRead), ct);

            // Prepend leftover from the previous (right-ward) chunk so line boundaries join correctly
            string chunk = encoding.GetString(buffer, 0, bytesRead) + leftover;
            string[] chunkLines = chunk.Split('\n');

            // The first segment is incomplete (leftover for the next left-ward iteration)
            leftover = chunkLines[0];

            // Complete lines are chunkLines[1..^0] (index 1 onward), added in reverse
            for (int i = chunkLines.Length - 1; i >= 1; i--)
            {
                lines.Insert(0, chunkLines[i].TrimEnd('\r'));
                if (lines.Count >= maxLines) break;
            }

            position = readFrom;
            progress?.Report((int)((fileSize - position) * 100 / fileSize));
        }

        // If we read all the way to the start, include the leftover (first line of file)
        if (position == 0 && !string.IsNullOrEmpty(leftover))
            lines.Insert(0, leftover.TrimEnd('\r'));

        progress?.Report(100);

        return new TailLoadResult
        {
            Lines = lines,
            Encoding = encoding,
            FileSize = fileSize,
            TailStartByteOffset = position
        };
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
        catch { }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}
