using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace FocusLens.Platform.Windows.Ocr;

/// <summary>On-device OCR using the built-in Windows.Media.Ocr engine (no network, no images kept).</summary>
public sealed class WindowsOcr
{
    private readonly OcrEngine? _engine = OcrEngine.TryCreateFromUserProfileLanguages();

    public bool IsAvailable => _engine is not null;

    public async Task<string?> RecognizeAsync(byte[] png)
    {
        if (_engine is null) return null;
        try
        {
            using var stream = new global::Windows.Storage.Streams.InMemoryRandomAccessStream();
            using (var writer = new global::Windows.Storage.Streams.DataWriter(stream))
            {
                writer.WriteBytes(png);
                await writer.StoreAsync();
                writer.DetachStream();
            }
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            var result = await _engine.RecognizeAsync(bitmap);
            var text = result.Text?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }
}
