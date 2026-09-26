using System.Windows.Interop;

namespace FocusLens.Platform.Windows.Guide;

/// <summary>
/// The global shortcut. RegisterHotKey is the right tool on Windows: it needs no permission and
/// no hook, and the system tells us only when the exact combination is pressed. A hidden
/// message-only window receives the notification.
/// </summary>
public sealed class GuideHotkey : IDisposable
{
    private const int Id = 0x4755; // "GU"
    private HwndSource? _source;

    public event Action? Pressed;

    /// <summary>Registers the combination. False when another app already owns it.</summary>
    public bool Register(int modifiers, int virtualKey)
    {
        Unregister();
        _source = new HwndSource(new HwndSourceParameters("FocusLensGuideHotkey")
        {
            ParentWindow = GuideNative.HWND_MESSAGE,
            Width = 0,
            Height = 0,
        });
        _source.AddHook(WndProc);
        // MOD_NOREPEAT (0x4000): holding the keys must not fire a stream of presses.
        return GuideNative.RegisterHotKey(_source.Handle, Id, (uint)modifiers | 0x4000, (uint)virtualKey);
    }

    public void Unregister()
    {
        if (_source is null) return;
        GuideNative.UnregisterHotKey(_source.Handle, Id);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == GuideNative.WM_HOTKEY && wParam.ToInt32() == Id)
        {
            handled = true;
            Pressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();
}
