using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace FreewriteWindows;

public sealed partial class HumanEntry
{
    private const string TimestampFormat = "yyyy-MM-dd-HH-mm-ss";

    public required Guid Id { get; init; }
    public required string Date { get; init; }
    public required string Filename { get; init; }
    public required string PreviewText { get; set; }
    public required EntryType EntryType { get; set; }
    public string? VideoFilename { get; set; }
    public DateTime Timestamp { get; init; }

    public static HumanEntry CreateNew()
    {
        var now = DateTime.Now;
        var id = Guid.NewGuid();
        var timestamp = now.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        return new HumanEntry
        {
            Id = id,
            Date = now.ToString("MMM d", CultureInfo.CurrentCulture),
            Filename = $"[{id.ToString().ToUpperInvariant()}]-[{timestamp}].md",
            PreviewText = string.Empty,
            EntryType = EntryType.Text,
            Timestamp = now
        };
    }

    public static HumanEntry CreateVideoEntry()
    {
        var entry = CreateNew();
        entry.EntryType = EntryType.Video;
        entry.PreviewText = "Video Entry";
        entry.VideoFilename = entry.Filename.Replace(".md", ".mov", StringComparison.OrdinalIgnoreCase);
        return entry;
    }

    public static HumanEntry? FromMarkdownFile(FileInfo file, bool hasVideo, string previewText)
    {
        var parsed = ParseCanonicalFilename(file.Name);
        if (parsed is null)
        {
            return null;
        }

        var (id, timestamp) = parsed.Value;
        var videoFilename = file.Name.Replace(".md", ".mov", StringComparison.OrdinalIgnoreCase);
        return new HumanEntry
        {
            Id = id,
            Date = timestamp.ToString("MMM d", CultureInfo.CurrentCulture),
            Filename = file.Name,
            PreviewText = hasVideo ? previewText : previewText,
            EntryType = hasVideo ? EntryType.Video : EntryType.Text,
            VideoFilename = hasVideo ? videoFilename : null,
            Timestamp = timestamp
        };
    }

    public static (Guid Id, DateTime Timestamp)? ParseCanonicalFilename(string filename)
    {
        var match = CanonicalEntryRegex().Match(filename);
        if (!match.Success)
        {
            return null;
        }

        if (!Guid.TryParse(match.Groups["id"].Value, out var id))
        {
            return null;
        }

        if (!DateTime.TryParseExact(
                match.Groups["timestamp"].Value,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp))
        {
            return null;
        }

        return (id, timestamp);
    }

    [GeneratedRegex(@"^\[(?<id>[0-9a-fA-F-]{36})\]-\[(?<timestamp>\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2})\]\.md$")]
    private static partial Regex CanonicalEntryRegex();
}
