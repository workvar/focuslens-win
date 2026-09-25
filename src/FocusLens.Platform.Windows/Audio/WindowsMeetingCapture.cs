using System.Threading.Channels;
using FocusLens.Core.Meetings;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace FocusLens.Platform.Windows.Audio;

/// <summary>
/// Records two tracks: the microphone ("You") and everything the PC plays ("Others", via WASAPI
/// loopback). Each track is chunked into WAV files that are announced on <see cref="Chunks"/>.
/// </summary>
public sealed class WindowsMeetingCapture : IMeetingCapture
{
    private readonly Channel<AudioChunk> _channel = Channel.CreateUnbounded<AudioChunk>();
    private readonly List<string> _files = new();

    private WasapiCapture? _mic;
    private WasapiLoopbackCapture? _loopback;
    private ChunkedWavWriter? _micWriter;
    private ChunkedWavWriter? _systemWriter;
    private PcmConverter? _micConverter;
    private PcmConverter? _systemConverter;
    private string _directory = "";

    public ChannelReader<AudioChunk> Chunks => _channel.Reader;

    public Task StartAsync(string directory, string? appId)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);

        try
        {
            _micWriter = new ChunkedWavWriter(directory, TranscriptTrack.Mic, _channel.Writer);
            _systemWriter = new ChunkedWavWriter(directory, TranscriptTrack.System, _channel.Writer);

            _mic = new WasapiCapture();
            _micConverter = new PcmConverter(_mic.WaveFormat);
            _mic.DataAvailable += (_, e) => _micConverter.Convert(e.Buffer, e.BytesRecorded, _micWriter.Write);
            _mic.StartRecording();

            _loopback = new WasapiLoopbackCapture();
            _systemConverter = new PcmConverter(_loopback.WaveFormat);
            _loopback.DataAvailable += (_, e) => _systemConverter.Convert(e.Buffer, e.BytesRecorded, _systemWriter.Write);
            _loopback.StartRecording();
        }
        catch (UnauthorizedAccessException ex)
        {
            Cleanup();
            throw new MeetingCaptureException(MeetingError.MicrophoneDenied, inner: ex);
        }
        catch (Exception ex)
        {
            Cleanup();
            throw new MeetingCaptureException(MeetingError.AudioCaptureFailed, inner: ex);
        }
        return Task.CompletedTask;
    }

    public void Pause()
    {
        _micWriter?.SetPaused(true);
        _systemWriter?.SetPaused(true);
    }

    public void Resume()
    {
        _micWriter?.SetPaused(false);
        _systemWriter?.SetPaused(false);
    }

    public async Task StopAsync()
    {
        _mic?.StopRecording();
        _loopback?.StopRecording();
        await Task.Delay(300); // let the last DataAvailable callbacks drain
        _micWriter?.Flush();
        _systemWriter?.Flush();
        Cleanup();
        _channel.Writer.TryComplete();
    }

    public void DeleteAudio()
    {
        try
        {
            if (Directory.Exists(_directory))
                foreach (var file in Directory.EnumerateFiles(_directory, "*.wav")) File.Delete(file);
        }
        catch
        {
            // Audio cleanup is best effort.
        }
    }

    private void Cleanup()
    {
        _mic?.Dispose();
        _loopback?.Dispose();
        _micConverter?.Dispose();
        _systemConverter?.Dispose();
        _mic = null;
        _loopback = null;
    }
}

public sealed class WindowsMeetingCaptureFactory : IMeetingCaptureFactory
{
    public IMeetingCapture Create() => new WindowsMeetingCapture();
}
