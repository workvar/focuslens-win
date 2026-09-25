using System.Runtime.InteropServices;
using FocusLens.Platform.Windows.Native;

namespace FocusLens.Platform.Windows.Capture;

public readonly record struct InputCounts(uint Keys, uint Clicks, uint Scrolls);

/// <summary>
/// Counts key presses, clicks and scrolls with low-level hooks. Only counters are kept:
/// key codes and coordinates are never read or stored. Runs its own message-pump thread.
/// </summary>
public sealed class InputCounter : IDisposable
{
    private readonly User32.HookProc _keyboardProc;
    private readonly User32.HookProc _mouseProc;
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;

    private long _keys;
    private long _clicks;
    private long _scrolls;

    public InputCounter()
    {
        // Keep delegate references alive for the lifetime of the hooks.
        _keyboardProc = KeyboardCallback;
        _mouseProc = MouseCallback;
    }

    public InputCounts Snapshot() => new(
        (uint)Interlocked.Read(ref _keys), (uint)Interlocked.Read(ref _clicks), (uint)Interlocked.Read(ref _scrolls));

    public void Start()
    {
        if (_thread is not null) return;
        var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Pump(ready)) { IsBackground = true, Name = "FocusLens.InputCounter" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(3));
    }

    private void Pump(ManualResetEventSlim ready)
    {
        _threadId = Kernel32.GetCurrentThreadId();
        var module = Kernel32.GetModuleHandleW(null);
        _keyboardHook = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _keyboardProc, module, 0);
        _mouseHook = User32.SetWindowsHookEx(User32.WH_MOUSE_LL, _mouseProc, module, 0);
        ready.Set();

        while (User32.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            User32.TranslateMessage(ref msg);
            User32.DispatchMessage(ref msg);
        }

        if (_keyboardHook != IntPtr.Zero) User32.UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero) User32.UnhookWindowsHookEx(_mouseHook);
    }

    private IntPtr KeyboardCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            if (message is User32.WM_KEYDOWN or User32.WM_SYSKEYDOWN) Interlocked.Increment(ref _keys);
        }
        return User32.CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private IntPtr MouseCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            switch (wParam.ToInt32())
            {
                case User32.WM_LBUTTONDOWN or User32.WM_RBUTTONDOWN or User32.WM_MBUTTONDOWN:
                    Interlocked.Increment(ref _clicks);
                    break;
                case User32.WM_MOUSEWHEEL:
                    Interlocked.Increment(ref _scrolls);
                    break;
            }
        }
        return User32.CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_threadId != 0) User32.PostThreadMessage(_threadId, User32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }
}
