using FocusLens.Core.Ai;

namespace FocusLens.Core.Meetings;

public enum SummaryDecision
{
    Allowed,
    BlockedNeedsOptIn,
    NoProvider,
}

/// <summary>Decides whether a transcript may be sent to the active model.</summary>
public static class MeetingSummaryPolicy
{
    public static SummaryDecision Decide(AiProvider provider, bool allowCloudSummary) => provider switch
    {
        AiProvider.Ollama o => string.IsNullOrEmpty(o.Host) || string.IsNullOrEmpty(o.Model)
            ? SummaryDecision.NoProvider : SummaryDecision.Allowed,
        AiProvider.Claude c => Cloud(c.ApiKey, allowCloudSummary),
        AiProvider.OpenAi o => Cloud(o.ApiKey, allowCloudSummary),
        _ => SummaryDecision.NoProvider,
    };

    private static SummaryDecision Cloud(string key, bool allow) =>
        string.IsNullOrWhiteSpace(key) ? SummaryDecision.NoProvider
        : allow ? SummaryDecision.Allowed : SummaryDecision.BlockedNeedsOptIn;

    public static string? Explanation(SummaryDecision decision) => decision switch
    {
        SummaryDecision.BlockedNeedsOptIn =>
            "_Summary skipped. Summarizing sends the full transcript to a cloud model. Turn on " +
            "**Settings, Meetings, Allow cloud summaries**, or configure a local Ollama model to " +
            "summarize entirely on this PC._",
        SummaryDecision.NoProvider => "_Summary skipped: no AI provider is configured._",
        _ => null,
    };
}
