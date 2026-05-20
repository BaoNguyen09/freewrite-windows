using FreewriteWindows;

var root = Path.Combine(Path.GetTempPath(), "FreewriteWindowsSmoke", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    var store = new FreewriteStore(root);

    var textEntry = HumanEntry.CreateNew();
    store.SaveNewEntry(textEntry, "First line\nSecond line");
    var loaded = store.LoadEntries();
    Assert(loaded.Count == 1, "loads one text entry");
    Assert(loaded[0].Filename == textEntry.Filename, "preserves canonical filename");
    Assert(loaded[0].PreviewText == "First line Second line", "normalizes text preview");
    Assert(File.Exists(Path.Combine(root, textEntry.Filename)), "writes markdown file");

    var sourceVideo = Path.Combine(root, "source.mp4");
    File.WriteAllBytes(sourceVideo, [0, 1, 2, 3]);
    File.WriteAllText(Path.ChangeExtension(sourceVideo, ".md"), "hello from transcript");

    var videoEntry = store.SaveImportedVideo(sourceVideo, "hello from transcript", replacementEntry: null);
    var videoFilename = videoEntry.VideoFilename ?? throw new InvalidOperationException("Missing video filename.");
    Assert(videoFilename.EndsWith(".mov", StringComparison.OrdinalIgnoreCase), "uses Freewrite-compatible .mov video filename");
    Assert(File.Exists(store.ManagedVideoPath(videoFilename)), "copies video into managed per-entry folder");
    Assert(File.Exists(store.TranscriptPath(videoFilename)), "writes transcript.md");
    Assert(store.LoadEntries().Any(entry => entry.EntryType == EntryType.Video), "loads video entry from markdown metadata and video asset");

    store.DeleteEntry(videoEntry);
    Assert(!File.Exists(store.ManagedVideoPath(videoFilename)), "deletes managed video asset");
    Assert(!File.Exists(store.TranscriptPath(videoFilename)), "deletes transcript asset");

    Console.WriteLine("FreewriteWindows smoke tests passed.");
    return 0;
}
finally
{
    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException("Assertion failed: " + message);
    }
}
