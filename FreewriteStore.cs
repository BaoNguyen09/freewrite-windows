using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace FreewriteWindows;

public sealed class FreewriteStore
{
    private readonly Dictionary<string, BitmapImage> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);

    public FreewriteStore(string? configuredFolder)
    {
        RootDirectory = string.IsNullOrWhiteSpace(configuredFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Freewrite")
            : configuredFolder;
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(VideosDirectory);
    }

    public string RootDirectory { get; private set; }
    public string VideosDirectory => Path.Combine(RootDirectory, "Videos");

    public void ChangeRoot(string folder)
    {
        RootDirectory = folder;
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(VideosDirectory);
        _thumbnailCache.Clear();
    }

    public IReadOnlyList<HumanEntry> LoadEntries()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(VideosDirectory);

        return Directory
            .EnumerateFiles(RootDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                var file = new FileInfo(path);
                var videoFilename = file.Name.Replace(".md", ".mov", StringComparison.OrdinalIgnoreCase);
                var hasVideo = HasVideoAsset(videoFilename);
                var content = SafeReadAllText(file.FullName);
                var preview = hasVideo
                    ? VideoPreviewText(videoFilename)
                    : PreviewTextFromContent(content, 30);
                return HumanEntry.FromMarkdownFile(file, hasVideo, preview);
            })
            .Where(entry => entry is not null)
            .Cast<HumanEntry>()
            .OrderByDescending(entry => entry.Timestamp)
            .ThenByDescending(entry => entry.Filename, StringComparer.Ordinal)
            .ToList();
    }

    public string EntryPath(HumanEntry entry) => Path.Combine(RootDirectory, entry.Filename);

    public string VideoEntryDirectory(string videoFilename)
    {
        var baseName = Path.GetFileNameWithoutExtension(videoFilename);
        return Path.Combine(VideosDirectory, baseName);
    }

    public string ManagedVideoPath(string videoFilename) => Path.Combine(VideoEntryDirectory(videoFilename), videoFilename);

    public string ThumbnailPath(string videoFilename) => Path.Combine(VideoEntryDirectory(videoFilename), "thumbnail.jpg");

    public string TranscriptPath(string videoFilename) => Path.Combine(VideoEntryDirectory(videoFilename), "transcript.md");

    public string ResolveVideoPath(string videoFilename)
    {
        var managed = ManagedVideoPath(videoFilename);
        if (File.Exists(managed))
        {
            return managed;
        }

        var flat = Path.Combine(VideosDirectory, videoFilename);
        if (File.Exists(flat))
        {
            return flat;
        }

        var root = Path.Combine(RootDirectory, videoFilename);
        return File.Exists(root) ? root : managed;
    }

    public bool HasVideoAsset(string videoFilename)
    {
        return File.Exists(ManagedVideoPath(videoFilename))
            || File.Exists(Path.Combine(VideosDirectory, videoFilename))
            || File.Exists(Path.Combine(RootDirectory, videoFilename));
    }

    public string ReadEntry(HumanEntry entry)
    {
        return File.Exists(EntryPath(entry)) ? SafeReadAllText(EntryPath(entry)).TrimStart('\n', '\r') : string.Empty;
    }

    public void SaveEntry(HumanEntry entry, string text)
    {
        if (entry.EntryType != EntryType.Text)
        {
            return;
        }

        File.WriteAllText(EntryPath(entry), text);
        entry.PreviewText = PreviewTextFromContent(text, 30);
    }

    public void SaveNewEntry(HumanEntry entry, string text)
    {
        File.WriteAllText(EntryPath(entry), text);
        entry.PreviewText = PreviewTextFromContent(text, 30);
    }

    public HumanEntry SaveImportedVideo(string sourceVideoPath, string? transcript, HumanEntry? replacementEntry)
    {
        var entry = replacementEntry is not null
            ? new HumanEntry
            {
                Id = replacementEntry.Id,
                Date = replacementEntry.Date,
                Filename = replacementEntry.Filename,
                PreviewText = PreviewTextFromTranscript(transcript),
                EntryType = EntryType.Video,
                VideoFilename = replacementEntry.Filename.Replace(".md", ".mov", StringComparison.OrdinalIgnoreCase),
                Timestamp = replacementEntry.Timestamp
            }
            : HumanEntry.CreateVideoEntry();

        var videoFilename = entry.VideoFilename ?? entry.Filename.Replace(".md", ".mov", StringComparison.OrdinalIgnoreCase);
        var videoDirectory = VideoEntryDirectory(videoFilename);
        Directory.CreateDirectory(videoDirectory);

        var destination = ManagedVideoPath(videoFilename);
        var sourceFullPath = Path.GetFullPath(sourceVideoPath);
        var destinationFullPath = Path.GetFullPath(destination);
        if (!sourceFullPath.Equals(destinationFullPath, StringComparison.OrdinalIgnoreCase) && File.Exists(destination))
        {
            File.Delete(destination);
        }

        if (!sourceFullPath.Equals(destinationFullPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourceVideoPath, destination);
        }
        SaveNewEntry(entry, "Video Entry");

        var cleanedTranscript = transcript?.Trim();
        var transcriptPath = TranscriptPath(videoFilename);
        if (!string.IsNullOrWhiteSpace(cleanedTranscript))
        {
            File.WriteAllText(transcriptPath, cleanedTranscript);
        }
        else if (File.Exists(transcriptPath))
        {
            File.Delete(transcriptPath);
        }

        entry.PreviewText = PreviewTextFromTranscript(cleanedTranscript);
        return entry;
    }

    public void DeleteEntry(HumanEntry entry)
    {
        var path = EntryPath(entry);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        if (entry.VideoFilename is null)
        {
            return;
        }

        _thumbnailCache.Remove(entry.VideoFilename);

        var managedDirectory = VideoEntryDirectory(entry.VideoFilename);
        var candidates = new[]
        {
            ManagedVideoPath(entry.VideoFilename),
            ThumbnailPath(entry.VideoFilename),
            TranscriptPath(entry.VideoFilename),
            Path.Combine(VideosDirectory, entry.VideoFilename),
            Path.Combine(RootDirectory, entry.VideoFilename)
        };

        foreach (var candidate in candidates.Where(File.Exists))
        {
            File.Delete(candidate);
        }

        if (Directory.Exists(managedDirectory) && !Directory.EnumerateFileSystemEntries(managedDirectory).Any())
        {
            Directory.Delete(managedDirectory);
        }
    }

    public string? LoadTranscript(string videoFilename)
    {
        var path = TranscriptPath(videoFilename);
        if (!File.Exists(path))
        {
            return null;
        }

        var text = SafeReadAllText(path).Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public BitmapImage? LoadThumbnail(string videoFilename)
    {
        if (_thumbnailCache.TryGetValue(videoFilename, out var cached))
        {
            return cached;
        }

        var path = ThumbnailPath(videoFilename);
        if (!File.Exists(path))
        {
            return null;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path);
        image.EndInit();
        image.Freeze();
        _thumbnailCache[videoFilename] = image;
        return image;
    }

    public static string PreviewTextFromContent(string content, int maxChars)
    {
        var normalized = Regex.Replace(content.Replace('\n', ' ').Replace('\r', ' '), @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized.Length <= maxChars ? normalized : normalized[..maxChars] + "...";
    }

    public static string PreviewTextFromTranscript(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return "Video Entry";
        }

        var normalized = Regex.Replace(transcript.Replace('\n', ' ').Replace('\r', ' '), @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Video Entry";
        }

        var preview = normalized[..Math.Min(10, normalized.Length)].TrimEnd('.', ',', '!', '?', ';', ':', '"', '\'');
        return string.IsNullOrWhiteSpace(preview) ? "Video Entry" : preview + "...";
    }

    public string VideoPreviewText(string videoFilename)
    {
        return PreviewTextFromTranscript(LoadTranscript(videoFilename));
    }

    private static string SafeReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return string.Empty;
        }
    }
}
