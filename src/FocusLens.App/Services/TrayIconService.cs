using System.Drawing;
using System.Windows.Forms;

namespace FocusLens.App.Services;

/// <summary>Notification-area icon with a small menu and balloon notifications.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _updateItem;
    private Action? _balloonAction;

    public event Action? OpenRequested;
    public event Action? PauseToggled;
    public event Action? QuitRequested;
    public event Action? UpdateRequested;

    public TrayIconService()
    {
        _pauseItem = new ToolStripMenuItem("Pause tracking", null, (_, _) => PauseToggled?.Invoke());

        _updateItem = new ToolStripMenuItem("Check for updates", null, (_, _) => UpdateRequested?.Invoke());

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Open FocusLens", null, (_, _) => OpenRequested?.Invoke()));
        menu.Items.Add(_pauseItem);
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
        _icon.Text = paused ? "FocusLens (paused)" : "FocusLens";
    }

    public void SetUpdateReady(string version) => _updateItem.Text = $"Restart to update (v{version})";

    public void SetTooltip(string text) => _icon.Text = text.Length > 63 ? text[..63] : text;

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
