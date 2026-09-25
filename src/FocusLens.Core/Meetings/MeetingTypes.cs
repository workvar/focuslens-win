namespace FocusLens.Core.Meetings;

public enum MeetingProvider
{
    Zoom, Meet, Teams, Discord, Slack, Facetime, Webex, Whereby, Jitsi, Unknown,
}

public static class MeetingProviderExtensions
{
    public static string DisplayName(this MeetingProvider provider) => provider switch
    {
        MeetingProvider.Zoom => "Zoom",
        MeetingProvider.Meet => "Google Meet",
        MeetingProvider.Teams => "Microsoft Teams",
        MeetingProvider.Discord => "Discord",
        MeetingProvider.Slack => "Slack huddle",
        MeetingProvider.Facetime => "FaceTime",
        MeetingProvider.Webex => "Webex",
        MeetingProvider.Whereby => "Whereby",
        MeetingProvider.Jitsi => "Jitsi",
        _ => "Meeting",
    };

    public static string Id(this MeetingProvider provider) => provider.ToString().ToLowerInvariant();

    public static MeetingProvider FromId(string id) =>
        Enum.TryParse<MeetingProvider>(id, ignoreCase: true, out var p) ? p : MeetingProvider.Unknown;
}

public enum MeetingSource
{
    /// <summary>The user pressed Start.</summary>
    Manual,
    /// <summary>Audio-session detection.</summary>
    Detected,
    /// <summary>Detection corroborated by a calendar event.</summary>
    Calendar,
}

public sealed record MeetingCandidate
{
    public string? Title { get; init; }
    public MeetingProvider Provider { get; init; } = MeetingProvider.Unknown;
    public MeetingSource Source { get; init; } = MeetingSource.Manual;
    public string? AppId { get; init; }
    public string? ConferencingUrl { get; init; }
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

    public string ResolvedTitle =>
        !string.IsNullOrWhiteSpace(Title) ? Title! : Provider.DisplayName();
}

public enum MeetingError
{
    MicrophoneDenied,
    AudioCaptureFailed,
    TranscriptionFailed,
    SummarizationFailed,
    DiskFull,
    Interrupted,
}

public static class MeetingErrorExtensions
{
    public static string UserMessage(this MeetingError error) => error switch
    {
        MeetingError.MicrophoneDenied => "FocusLens needs microphone access to take notes. Enable it in Windows Settings, Privacy, Microphone.",
        MeetingError.AudioCaptureFailed => "Audio capture stopped unexpectedly.",
        MeetingError.TranscriptionFailed => "The transcript could not be generated.",
        MeetingError.SummarizationFailed => "Notes were saved, but the summary could not be generated.",
        MeetingError.DiskFull => "Not enough disk space to record this meeting.",
        _ => "Note-taking was interrupted.",
    };

    public static bool IsRetryable(this MeetingError error) => error switch
    {
        MeetingError.MicrophoneDenied or MeetingError.DiskFull => false,
        _ => true,
    };
}
