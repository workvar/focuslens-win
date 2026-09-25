using System.Drawing;
using System.Windows.Forms;

namespace FocusLens.App.Services;

/// <summary>Notification-area icon with a small menu and balloon notifications.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _updateItem;
    private readonly ToolStripMenuItem _focusItem;
    private Action? _balloonAction;
    private bool _paused;
    private string? _focusText;

    public event Action? OpenRequested;
    public event Action? PauseToggled;
    public event Action? QuitRequested;
    public event Action? UpdateRequested;
    public event Action? EndFocusRequested;

    public TrayIconService()
    {
        _pauseItem = new ToolStripMenuItem("Pause tracking", null, (_, _) => PauseToggled?.Invoke());

        _updateItem = new ToolStripMenuItem("Check for updates", null, (_, _) => UpdateRequested?.Invoke());

        // Shown only while a focus session runs, with the time left in its label.
        _focusItem = new ToolStripMenuItem("End focus session", null, (_, _) => EndFocusRequested?.Invoke()) { Visible = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Open FocusLens", null, (_, _) => OpenRequested?.Invoke()));
        menu.Items.Add(_pauseItem);
        menu.Items.Add(_focusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_updateItem);
        menu.Items.Add(new ToolStripMenuItem("Quit", null, (_, _) => QuitRequested?.Invoke()));

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "FocusLens",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        _icon.BalloonTipClicked += (_, _) => { var action = _balloonAction; _balloonAction = null; action?.Invoke(); };
    }

    public void SetPaused(bool paused)
    {
        _pauseItem.Text = paused ? "Resume tracking" : "Pause tracking";
        _paused = paused;
        UpdateTooltip();
    }

    public void SetUpdateItem(string text) => _updateItem.Text = text;

    /// <summary>Shows the "End focus session" item with <paramref name="text"/>, or hides it when null.</summary>
    public void SetFocusItem(string? text)
    {
        _focusItem.Visible = text is not null;
        if (text is not null) _focusItem.Text = text;
    }

    public void SetTooltip(string text) => _icon.Text = text.Length > 63 ? text[..63] : text;

    /// <summary>"42m" while a focus session runs and the tray timer is on, otherwise null.</summary>
    public void SetFocusTime(string? text)
    {
        _focusText = text;
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        var text = _paused ? "FocusLens (paused)" : "FocusLens";
        if (_focusText is not null) text += $", focus: {_focusText} left";
        SetTooltip(text);
    }

    /// <summary>Shows a balloon; clicking it runs <paramref name="onClick"/>.</summary>
    public void Notify(string title, string message, Action? onClick = null)
    {
        _balloonAction = onClick;
        _icon.ShowBalloonTip(6000, title, message, ToolTipIcon.Info);
    }

    private static Icon LoadIcon()
    {
        try
        {
            var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/focuslens.ico"))?.Stream;
            if (stream is not null) return new Icon(stream);
        }
        catch
        {
            // Fall back to the default icon below.
        }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
