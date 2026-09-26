using System.Windows;
using System.Windows.Threading;
using FocusLens.App.Views.Guide;
using FocusLens.Core.Ai;
using FocusLens.Core.Guide;
using FocusLens.Platform.Windows.Guide;

namespace FocusLens.App.Services.Guide;

/// <summary>
/// Wires Guide together: the shortcut, the prompt box and the session. AppServices owns one and
/// starts it once the main window exists.
///
///   shortcut, nothing running   open the prompt box next to the pointer
///   shortcut, guide running     skip the current step
///   Esc, guide running          stop the guide (handled by the input hook)
/// </summary>
public sealed class GuideCoordinator : IDisposable
{
    private readonly GuideSettings _settings;
    private readonly GuideHotkey _hotkey = new();
    private readonly UiaWalker _walker = new();
    private GuidePromptWindow? _prompt;

    public GuideSessionController Session { get; }

    public GuideCoordinator(StreamingAiClient client, GuideSettings settings, Dispatcher dispatcher)
    {
        _settings = settings;
        var cursor = new GuideCursorController(settings, dispatcher);
        Session = new GuideSessionController(new LlmGuidePlanner(client, settings), _walker, cursor);
        _hotkey.Pressed += OnHotkey;
        settings.Changed += Apply;
    }

    /// <summary>False when another app already owns the shortcut; the Settings tab says so.</summary>
    public bool HotkeyRegistered { get; private set; }

    public void Start() => Apply();

    private void Apply()
    {
        if (!_settings.Enabled)
        {
            _hotkey.Unregister();
            HotkeyRegistered = false;
            return;
        }
        HotkeyRegistered = _hotkey.Register(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey);
    }

    private void OnHotkey()
    {
        if (Session.IsActive) { Session.Skip(); return; }
        if (_prompt is not null) { _prompt.CloseOnce(); return; }

        // Remember the window the user was in: the prompt box is about to take focus from it.
        var working = GuideOverlayStyle.ForegroundWindow();
        var (px, py) = GuideOverlayStyle.CursorPixels();
        var dpi = VisualTreeHelperDpi();

        _prompt = new GuidePromptWindow();
        _prompt.Closed += (_, _) => _prompt = null;
        _prompt.Submitted += request => Session.Begin(request, working);
        _prompt.ShowNear(px / dpi, py / dpi);
    }

    private static double VisualTreeHelperDpi() =>
        Application.Current.MainWindow is { } w ? System.Windows.Media.VisualTreeHelper.GetDpi(w).DpiScaleX : 1;

    public void Dispose()
    {
        _settings.Changed -= Apply;
        _hotkey.Dispose();
        Session.Stop();
    }
}
