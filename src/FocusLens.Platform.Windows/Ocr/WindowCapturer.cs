using System.Drawing;
using System.Drawing.Imaging;
using FocusLens.Platform.Windows.Native;

namespace FocusLens.Platform.Windows.Ocr;

/// <summary>Renders one window to PNG bytes in memory. Nothing is written to disk.</summary>
public static class WindowCapturer
{
    private const int MaxWidth = 2000;

    public static byte[]? CapturePng(IntPtr hwnd)
    {
        if (!User32.GetWindowRect(hwnd, out var rect) || rect.Width <= 0 || rect.Height <= 0) return null;

        using var full = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(full))
        {
            var hdc = graphics.GetHdc();
            try
            {
                if (!User32.PrintWindow(hwnd, hdc, User32.PW_RENDERFULLCONTENT)) return null;
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }

        using var output = Downscale(full);
        using var stream = new MemoryStream();
        output.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static Bitmap Downscale(Bitmap source)
    {
        if (source.Width <= MaxWidth) return new Bitmap(source);
        var scale = MaxWidth / (double)source.Width;
        var scaled = new Bitmap(MaxWidth, (int)(source.Height * scale));
        using var graphics = Graphics.FromImage(scaled);
        graphics.DrawImage(source, 0, 0, scaled.Width, scaled.Height);
        return scaled;
    }
}
