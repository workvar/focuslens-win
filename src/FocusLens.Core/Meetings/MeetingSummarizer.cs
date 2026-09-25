using FocusLens.Core.Ai;
using FocusLens.Core.Repositories;

namespace FocusLens.Core.Meetings;

/// <summary>Turns a stored transcript into structured notes, honouring the cloud opt-in policy.</summary>
public sealed class MeetingSummarizer
{
    private readonly StreamingAiClient _client;
    private readonly MeetingStore _store;
    private readonly Func<bool> _allowCloud;

    public MeetingSummarizer(StreamingAiClient client, MeetingStore store, Func<bool> allowCloud)
    {
        _client = client;
        _store = store;
        _allowCloud = allowCloud;
    }

    /// <summary>Returns the summary block text, or null when nothing was summarised.</summary>
    public async Task<string?> SummarizeAsync(string meetingId, string title, CancellationToken ct = default)
    {
        var transcript = await _store.TranscriptTextAsync(meetingId);
        if (string.IsNullOrWhiteSpace(transcript))
        {
            await _store.UpsertNotesAsync(new[]
            {
                MeetingNote.Make(meetingId, NoteBlockType.Summary, "_No speech was captured for this meeting._"),
            });
            return null;
        }

        var decision = MeetingSummaryPolicy.Decide(_client.Provider, _allowCloud());
        if (decision != SummaryDecision.Allowed)
        {
            await _store.UpsertNotesAsync(new[]
            {
                MeetingNote.Make(meetingId, NoteBlockType.Summary, MeetingSummaryPolicy.Explanation(decision) ?? ""),
            });
            return null;
        }

        var markdown = await _client.CompleteAsync(Prompt(title, transcript), ct);
        var parsed = MeetingSummaryParser.Parse(markdown);
        await PersistAsync(parsed, meetingId, markdown);
        return parsed.Blocks.GetValueOrDefault(NoteBlockType.Summary);
    }

    private async Task PersistAsync(MeetingSummaryParser.Result parsed, string meetingId, string fallback)
    {
        var notes = parsed.Blocks.Count == 0
            ? new List<MeetingNote> { MeetingNote.Make(meetingId, NoteBlockType.Summary, fallback) }
            : parsed.Blocks.Select(kv => MeetingNote.Make(meetingId, kv.Key, kv.Value)).ToList();
        await _store.UpsertNotesAsync(notes);
        await _store.ReplaceActionItemsAsync(
            parsed.ActionItems.Select(a => ActionItem.Make(meetingId, a.Owner, a.Text)).ToList(), meetingId);
    }

    private static string Prompt(string title, string transcript) => $"""
        You are summarizing a meeting transcript. "You" is the person whose microphone was recorded; "Others" is everyone else in the call.

        Meeting title: {title}

        Write the summary using exactly these four markdown headings, in this order, and nothing else. No preamble, no closing remarks.

        ## Summary
        Three to five sentences of what the meeting was about and where it landed.

        ## Decisions
        Bullet list of decisions actually made. Omit the section's bullets entirely if none were.

        ## Action items
        Bullet list, one per line, formatted "Owner: task". Use "You" as the owner when the speaker committed to it themselves. Omit if none.

        ## Open questions
        Bullet list of questions raised but not answered. Omit if none.

        Do not invent anything that is not in the transcript.

        Transcript:
        {transcript}
        """;
}
