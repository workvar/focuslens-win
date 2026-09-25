using FocusLens.Core.Meetings;

namespace FocusLens.App.ViewModels.Meetings;

/// <summary>A row in the meetings list.</summary>
public sealed record MeetingListItem(string Id, string Title, string Provider, string When, string Duration, string Phase)
{
    public static MeetingListItem From(Meeting meeting)
    {
        var started = DateTimeOffset.FromUnixTimeSeconds(meeting.StartedAt).ToLocalTime();
        var provider = MeetingProviderExtensions.FromId(meeting.Provider).DisplayName();
        var duration = meeting.DurationS > 0 ? MeetingClock.Format(TimeSpan.FromSeconds(meeting.DurationS)) : "";
        var phase = meeting.Phase switch
        {
            "complete" => "",
            "failed" => "Failed",
            "interrupted" => "Interrupted",
            "recording" => "Recording",
            _ => "Processing",
        };
        return new MeetingListItem(meeting.Id, meeting.Title, provider, started.ToString("ddd MMM d, h:mm tt"), duration, phase);
    }
}
