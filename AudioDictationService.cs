using System.IO;
using NAudio.Wave;

namespace FreewriteWindows;

public sealed class AudioDictationService : IDisposable
{
    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;

    private const float RecordGain = 2.75f;

    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _outputPath;
    private float _peakLevel;
    private float _sessionLevelHigh;

    public string? OutputPath => _outputPath;

    public event Action<float>? LevelChanged;

    public float CurrentPeak => _peakLevel;

    public Task StartAsync(string wavPath)
    {
        Dispose();
        _outputPath = wavPath;
        Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);

        var format = new WaveFormat(SampleRate, BitsPerSample, Channels);
        _waveIn = new WaveInEvent
        {
            WaveFormat = format,
            BufferMilliseconds = 50
        };

        _sessionLevelHigh = 0.02f;
        _writer = new WaveFileWriter(wavPath, format);
        _waveIn.DataAvailable += WaveIn_DataAvailable;
        _waveIn.RecordingStopped += (_, _) =>
        {
            _writer?.Dispose();
            _writer = null;
        };
        _waveIn.StartRecording();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_waveIn is null)
        {
            return Task.CompletedTask;
        }

        _waveIn.StopRecording();
        _waveIn.DataAvailable -= WaveIn_DataAvailable;
        _waveIn.Dispose();
        _waveIn = null;
        _writer?.Dispose();
        _writer = null;
        _peakLevel = 0;
        LevelChanged?.Invoke(0);
        return Task.CompletedTask;
    }

    private void WaveIn_DataAvailable(object? sender, WaveInEventArgs args)
    {
        if (args.BytesRecorded < 2)
        {
            return;
        }

        WriteAmplifiedBuffer(args.Buffer, args.BytesRecorded);
        _writer?.Flush();

        var peak = ComputePeak(args.Buffer, args.BytesRecorded);
        var rms = ComputeRms(args.Buffer, args.BytesRecorded);
        var raw = Math.Max(peak, rms * 1.6f);
        _sessionLevelHigh = Math.Max(_sessionLevelHigh * 0.992f, raw);
        var normalized = raw / Math.Max(_sessionLevelHigh, 0.015f);
        _peakLevel = ToVisualLevel(normalized);
        LevelChanged?.Invoke(_peakLevel);
    }

    private void WriteAmplifiedBuffer(byte[] buffer, int bytesRecorded)
    {
        var sampleCount = bytesRecorded / 2;
        Span<byte> amplified = sampleCount <= 512
            ? stackalloc byte[bytesRecorded]
            : new byte[bytesRecorded];

        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(buffer, i * 2);
            var boosted = (int)Math.Round(sample * RecordGain);
            boosted = Math.Clamp(boosted, short.MinValue, short.MaxValue);
            BitConverter.TryWriteBytes(amplified[(i * 2)..], (short)boosted);
        }

        _writer?.Write(amplified[..bytesRecorded]);
    }

    private static float ComputePeak(byte[] buffer, int bytesRecorded)
    {
        var sampleCount = bytesRecorded / 2;
        var max = 0;
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = Math.Abs(BitConverter.ToInt16(buffer, i * 2));
            if (sample > max)
            {
                max = sample;
            }
        }

        return max / 32768f;
    }

    private static float ComputeRms(byte[] buffer, int bytesRecorded)
    {
        var sampleCount = bytesRecorded / 2;
        if (sampleCount == 0)
        {
            return 0;
        }

        double sum = 0;
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(buffer, i * 2);
            sum += sample * sample;
        }

        return (float)(Math.Sqrt(sum / sampleCount) / 32768.0);
    }

    private static float ToVisualLevel(float normalized)
    {
        if (normalized < 0.1f)
        {
            return normalized * 0.15f;
        }

        var scaled = (normalized - 0.1f) / 0.9f;
        return Math.Clamp(0.02f + (float)Math.Pow(scaled, 0.58), 0f, 1f);
    }

    public void Dispose()
    {
        if (_waveIn is not null)
        {
            try
            {
                _waveIn.StopRecording();
            }
            catch
            {
                // Already stopped.
            }

            _waveIn.DataAvailable -= WaveIn_DataAvailable;
            _waveIn.Dispose();
            _waveIn = null;
        }

        _writer?.Dispose();
        _writer = null;
    }
}
