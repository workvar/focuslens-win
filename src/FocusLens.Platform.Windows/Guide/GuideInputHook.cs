using System.Runtime.InteropServices;

namespace FocusLens.Platform.Windows.Guide;

/// <summary>
/// Watches mouse clicks and the Return and Escape keys while a guide is running. Read-only: every
/// event is passed on with CallNextHookEx, so the app underneath behaves as if Guide were not
/// there. Installed only for the length of a guide, never left running. Must be created on the UI
/// thread, which already pumps messages.
/// </summary>
public sealed class GuideInputHook : IDisposable
{
    private IntPtr _mouse, _keyboard;
    // Held in fields: a delegate the garbage collector reclaims would crash the hook.
    private readonly GuideNative.HookProc _mouseProc;
    private readonly GuideNative.HookProc _keyboardProc;

    /// <summary>Left button released, in screen pixels.</summary>
    public event Action<int, int>? Click;
    public event Action? ReturnKey;
    public event Action? EscapeKey;

    public GuideInputHook()
    {
        _mouseProc = OnMouse;
        _keyboardProc = OnKeyboard;
    }

    public void Start()
    {
        if (_mouse != IntPtr.Zero) return;
        var module = GuideNative.GetModuleHandleW(null);
        _mouse = GuideNative.SetWindowsHookExW(GuideNative.WH_MOUSE_LL, _mouseProc, module, 0);
        _keyboard = GuideNative.SetWindowsHookExW(GuideNative.WH_KEYBOARD_LL, _keyboardProc, module, 0);
    }

    public void Stop()
    {
        if (_mouse != IntPtr.Zero) GuideNative.UnhookWindowsHookEx(_mouse);
        if (_keyboard != IntPtr.Zero) GuideNative.UnhookWindowsHookEx(_keyboard);
        _mouse = _keyboard = IntPtr.Zero;
    }

    private IntPtr OnMouse(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() == GuideNative.WM_LBUTTONUP)
        {
            var data = Marshal.PtrToStructure<GuideNative.MSLLHOOKSTRUCT>(lParam);
            Click?.Invoke(data.Pt.X, data.Pt.Y);
        }
        return GuideNative.CallNextHookEx(_mouse, code, wParam, lParam);
    }

    private IntPtr OnKeyboard(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() == GuideNative.WM_KEYDOWN)
        {
            var key = Marshal.PtrToStructure<GuideNative.KBDLLHOOKSTRUCT>(lParam).VkCode;
            if (key == 0x0D) ReturnKey?.Invoke();
            else if (key == 0x1B) EscapeKey?.Invoke();
        }
        return GuideNative.CallNextHookEx(_keyboard, code, wParam, lParam);
    }

    public void Dispose() => Stop();
}
