using System.IO;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace FreewriteWindows;

public sealed class LocalTranscriptionService
{
    private static readonly string ModelDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Freewrite",
        "models");

    private static readonly string ModelPath = Path.Combine(ModelDirectory, "ggml-base.bin");

    private readonly SemaphoreSlim _modelGate = new(1, 1);
    private bool _modelReady;

    public string ModelFilePath => ModelPath;

    public bool IsModelInstalled => File.Exists(ModelPath);

    public async Task EnsureModelAsync(IProgress<string>? progress = null)
    {
        if (_modelReady && File.Exists(ModelPath))
        {
            return;
        }

        await _modelGate.WaitAsync();
        try
        {
            if (File.Exists(ModelPath))
            {
                _modelReady = true;
                return;
            }

            Directory.CreateDirectory(ModelDirectory);
            progress?.Report("Downloading local speech model (one time, ~140 MB)...");
            await using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Base);
            await using var fileWriter = File.Create(ModelPath);
            await modelStream.CopyToAsync(fileWriter);
            _modelReady = true;
            progress?.Report("Model ready.");
        }
        finally
        {
            _modelGate.Release();
        }
    }

    public async Task<string> TranscribeWavAsync(string wavPath, IProgress<string>? progress = null)
    {
        await EnsureModelAsync(progress);

        var builder = new StringBuilder();
        using var whisperFactory = WhisperFactory.FromPath(ModelPath);
        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage("en")
            .Build();

        await using var fileStream = File.OpenRead(wavPath);
        await foreach (var segment in processor.ProcessAsync(fileStream))
        {
            if (!string.IsNullOrWhiteSpace(segment.Text))
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(segment.Text.Trim());
            }
        }

        return FormatTranscript(builder.ToString());
    }

    private static string FormatTranscript(string text)
    {
        var normalized = string.Join(
            " ",
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var trimmed = normalized.Trim();
        var firstLetterIndex = trimmed.TakeWhile(ch => !char.IsLetter(ch)).Count();
        if (firstLetterIndex < trimmed.Length)
        {
            trimmed = trimmed[..firstLetterIndex]
                + char.ToUpperInvariant(trimmed[firstLetterIndex])
                + trimmed[(firstLetterIndex + 1)..];
        }

        if (trimmed[^1] is not ('.' or '!' or '?'))
        {
            trimmed += ".";
        }

        return trimmed;
    }
}
