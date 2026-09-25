using FocusLens.Core.Meetings;

namespace FocusLens.App.Services;

/// <summary>Meeting lifecycle notifications shown as tray balloons.</summary>
public sealed class MeetingNotifier : IMeetingNotifier
{
    private readonly TrayIconService _tray;
    private readonly Action<string> _openMeeting;

    public MeetingNotifier(TrayIconService tray, Action<string> openMeeting)
    {
        _tray = tray;
        _openMeeting = openMeeting;
    }

    public Task NotifyStartedAsync(string meetingId, string title)
    {
        Ui(() => _tray.Notify("Taking notes", $"Recording \"{title}\""));
        return Task.CompletedTask;
    }

    public Task NotifyCompleteAsync(string meetingId, string title, string? summary)
    {
        var body = string.IsNullOrWhiteSpace(summary)
            ? $"Notes for \"{title}\" are ready."
            : summary.Length > 180 ? summary[..180] + "..." : summary;
        Ui(() => _tray.Notify("Meeting notes ready", body, () => _openMeeting(meetingId)));
        return Task.CompletedTask;
    }

    public Task NotifyFailedAsync(string meetingId, MeetingError error)
    {
        Ui(() => _tray.Notify("Meeting notes failed", error.UserMessage(), () => _openMeeting(meetingId)));
        return Task.CompletedTask;
    }

    private static void Ui(Action action) => System.Windows.Application.Current.Dispatcher.Invoke(action);
}
