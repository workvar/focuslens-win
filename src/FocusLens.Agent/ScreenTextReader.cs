using FocusLens.Platform.Windows.Capture;
using FocusLens.Platform.Windows.Ocr;

namespace FocusLens.Agent;

/// <summary>
/// Gets the text visible in a window. Tries UI Automation first (cheap, exact); falls back to
/// on-device OCR of an in-memory render when the app exposes little text. No image is ever saved.
/// </summary>
public sealed class ScreenTextReader
{
    private const int MinTextLength = 40;
    private readonly WindowsOcr _ocr = new();

    public bool CanOcr => _ocr.IsAvailable;

    public async Task<string?> ReadAsync(IntPtr hwnd, string appId, bool allowOcr)
    {
        var text = UiaTextReader.Read(hwnd);
        if (text.Length >= MinTextLength) return text;

        if (!allowOcr || !_ocr.IsAvailable || BrowserCatalog.IsBrowser(appId)) return string.IsNullOrEmpty(text) ? null : text;

        var png = WindowCapturer.CapturePng(hwnd);
        return png is null ? null : await _ocr.RecognizeAsync(png);
    }
}
