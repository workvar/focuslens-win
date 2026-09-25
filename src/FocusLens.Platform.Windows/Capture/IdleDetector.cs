using FocusLens.Platform.Windows.Native;

namespace FocusLens.Platform.Windows.Capture;

/// <summary>Seconds since the last keyboard or mouse input, system-wide.</summary>
public sealed class IdleDetector
{
    public TimeSpan TimeSinceLastInput()
    {
        var info = new User32.LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<User32.LASTINPUTINFO>() };
        if (!User32.GetLastInputInfo(ref info)) return TimeSpan.Zero;

        // dwTime is a 32-bit tick count that wraps; unchecked subtraction handles the wrap.
        var now = unchecked((uint)Environment.TickCount);
        var elapsedMs = unchecked(now - info.dwTime);
        return TimeSpan.FromMilliseconds(elapsedMs);
    }

    public bool IsIdle(TimeSpan threshold) => TimeSinceLastInput() >= threshold;
}
