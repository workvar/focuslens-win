using System.Threading.Channels;
using FocusLens.Core.Meetings;
using NAudio.Wave;

namespace FocusLens.Platform.Windows.Audio;

/// <summary>
/// Writes a continuous PCM stream into fixed-length 16 kHz mono WAV chunks and announces
/// each finished chunk, so transcription can start while the meeting is still running.
/// </summary>
public sealed class ChunkedWavWriter : IDisposable
{
    public static readonly WaveFormat Format = new(16000, 16, 1);

    private readonly string _directory;
    private readonly TranscriptTrack _track;
    private readonly ChannelWriter<AudioChunk> _output;
    private readonly int _chunkMs;
    private readonly object _gate = new();

    private WaveFileWriter? _writer;
    private string? _path;
    private long _chunkStartMs;
    private long _totalBytes;
    private int _index;
    private bool _paused;

    public ChunkedWavWriter(string directory, TranscriptTrack track, ChannelWriter<AudioChunk> output, int chunkMs = 30_000)
    {
        _directory = directory;
        _track = track;
        _output = output;
        _chunkMs = chunkMs;
    }

    public void SetPaused(bool paused)
    {
        lock (_gate) _paused = paused;
    }

    /// <summary>Appends PCM that is already 16 kHz, 16-bit, mono.</summary>
    public void Write(byte[] buffer, int count)
    {
        lock (_gate)
        {
            if (_paused || count <= 0) return;
            _writer ??= OpenNext();
            _writer.Write(buffer, 0, count);
            _totalBytes += count;

            var chunkBytes = _writer.Length;
            var chunkMs = chunkBytes * 1000 / Format.AverageBytesPerSecond;
            if (chunkMs >= _chunkMs) CloseCurrent();
        }
    }

    private WaveFileWriter OpenNext()
    {
        _path = Path.Combine(_directory, $"{_track.Id()}-{_index++:0000}.wav");
        _chunkStartMs = _totalBytes * 1000 / Format.AverageBytesPerSecond;
        return new WaveFileWriter(_path, Format);
    }

    private void CloseCurrent()
    {
        if (_writer is null || _path is null) return;
        var durationMs = (int)(_writer.Length * 1000 / Format.AverageBytesPerSecond);
        _writer.Dispose();
        _writer = null;
        if (durationMs >= 500)
            _output.TryWrite(new AudioChunk(_path, _track, (int)_chunkStartMs, durationMs));
        else
            TryDelete(_path);
        _path = null;
    }

    public void Flush()
    {
        lock (_gate) CloseCurrent();
    }

    public void Dispose() => Flush();

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best effort */ }
    }
}
