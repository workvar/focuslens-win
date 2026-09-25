using FocusLens.Core.Logging;
using FocusLens.Platform.Windows.Capture;
using FocusLens.Platform.Windows.Ocr;

namespace FocusLens.Agent;

/// <summary>
/// Gets the text visible in a window. Tries UI Automation first (cheap, exact); when the app exposes
/// little text, adds on-device OCR of an in-memory render. No image is ever saved.
/// </summary>
public sealed class ScreenTextReader
{
    private const int MinTextLength = 40;
    private static readonly TimeSpan ReportEvery = TimeSpan.FromMinutes(10);

    private readonly WindowsOcr _ocr = new();
    private readonly FileLog? _log;
    private readonly Dictionary<string, DateTime> _reported = new();
    private readonly object _gate = new();

    public ScreenTextReader(FileLog? log = null) => _log = log;

    public bool CanOcr => _ocr.IsAvailable;

    public async Task<string?> ReadAsync(IntPtr hwnd, string appId, bool allowOcr)
    {
        var uia = UiaTextReader.Read(hwnd);
        if (uia.Length >= MinTextLength)
        {
            Report(appId, "uia", uia.Length, 0);
            return uia;
        }

        if (!allowOcr || !_ocr.IsAvailable || BrowserCatalog.IsBrowser(appId))
        {
            Report(appId, "uia-only", uia.Length, 0);
            return string.IsNullOrEmpty(uia) ? null : uia;
        }

        var png = WindowCapturer.CapturePng(hwnd);
        var ocr = png is null ? null : await _ocr.RecognizeAsync(png);
        Report(appId, "ocr", uia.Length, ocr?.Length ?? 0);

        if (string.IsNullOrEmpty(ocr)) return string.IsNullOrEmpty(uia) ? null : uia;
        return string.IsNullOrEmpty(uia) ? ocr : uia + Environment.NewLine + ocr;
    }

    /// <summary>Logs how each app is being read, at most once per app and method every 10 minutes.</summary>
    private void Report(string appId, string method, int uiaChars, int ocrChars)
    {
        if (_log is null) return;
        var key = appId + "|" + method;
        lock (_gate)
        {
            if (_reported.TryGetValue(key, out var last) && DateTime.UtcNow - last < ReportEvery) return;
            _reported[key] = DateTime.UtcNow;
        }
        _log.Info($"Screen text {appId}: method={method} uiaChars={uiaChars} ocrChars={ocrChars}");
    }
}
