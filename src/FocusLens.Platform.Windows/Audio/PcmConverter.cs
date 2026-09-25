using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace FocusLens.Platform.Windows.Audio;

/// <summary>Converts a capture device's native format to 16 kHz, 16-bit mono PCM as data arrives.</summary>
public sealed class PcmConverter : IDisposable
{
    private readonly BufferedWaveProvider _input;
    private readonly IWaveProvider _output;
    private readonly byte[] _scratch = new byte[16000 * 2];

    public PcmConverter(WaveFormat source)
    {
        _input = new BufferedWaveProvider(source)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(10),
            ReadFully = false,
        };

        ISampleProvider samples = _input.ToSampleProvider();
        if (samples.WaveFormat.Channels > 1)
            samples = new MultiplexingSampleProvider(new[] { samples }, 1); // first channel only for the mic
        if (samples.WaveFormat.SampleRate != ChunkedWavWriter.Format.SampleRate)
            samples = new WdlResamplingSampleProvider(samples, ChunkedWavWriter.Format.SampleRate);
        _output = new SampleToWaveProvider16(samples);
    }

    /// <summary>Feeds raw device bytes and passes any converted PCM to the callback.</summary>
    public void Convert(byte[] buffer, int count, Action<byte[], int> onPcm)
    {
        _input.AddSamples(buffer, 0, count);
        int read;
        while ((read = _output.Read(_scratch, 0, _scratch.Length)) > 0)
            onPcm(_scratch, read);
    }

    public void Dispose() => _input.ClearBuffer();
}
