using System.Text;
using Windows.Media.SpeechRecognition;

namespace FreewriteWindows;

public sealed class WindowsTranscriptCapture : IDisposable
{
    private readonly StringBuilder _transcript = new();
    private SpeechRecognizer? _recognizer;
    private bool _isStarted;

    public async Task<bool> TryStartAsync()
    {
        try
        {
            _recognizer = new SpeechRecognizer();
            _recognizer.Constraints.Add(new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "FreewriteDictation"));
            var compilationResult = await _recognizer.CompileConstraintsAsync();
            if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
            {
                Dispose();
                return false;
            }

            _recognizer.ContinuousRecognitionSession.AutoStopSilenceTimeout = TimeSpan.Zero;
            _recognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
            await _recognizer.ContinuousRecognitionSession.StartAsync();
            _isStarted = true;
            return true;
        }
        catch
        {
            Dispose();
            return false;
        }
    }

    public async Task<string?> StopAsync()
    {
        var recognizer = _recognizer;
        if (recognizer is not null && _isStarted)
        {
            try
            {
                if (recognizer.State != SpeechRecognizerState.Idle)
                {
                    await recognizer.ContinuousRecognitionSession.StopAsync();
                }
            }
            catch
            {
                // Keep any transcript fragments collected before stop failed.
            }
        }

        _isStarted = false;
        var text = NormalizeTranscript(_transcript.ToString());
        Dispose();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private void ContinuousRecognitionSession_ResultGenerated(
        SpeechContinuousRecognitionSession sender,
        SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        if (args.Result.Status != SpeechRecognitionResultStatus.Success)
        {
            return;
        }

        if (args.Result.Confidence is not (SpeechRecognitionConfidence.Medium or SpeechRecognitionConfidence.High))
        {
            return;
        }

        var text = args.Result.Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        lock (_transcript)
        {
            if (_transcript.Length > 0)
            {
                _transcript.AppendLine();
                _transcript.AppendLine();
            }

            _transcript.Append(FormatSentence(text));
        }
    }

    private static string NormalizeTranscript(string text)
    {
        return string.Join(
            "\n\n",
            text.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries)
                .Select(part => FormatSentence(part.Trim()))
                .Where(part => part.Length > 0));
    }

    private static string FormatSentence(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var trimmed = text.Trim();
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

    public void Dispose()
    {
        if (_recognizer is null)
        {
            return;
        }

        _recognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
        _recognizer.Dispose();
        _recognizer = null;
    }
}
