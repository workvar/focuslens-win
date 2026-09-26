using System.Text;
using FocusLens.Core.Ai;

namespace FocusLens.Core.Guide;

public interface IGuidePlanner
{
    /// <summary>The next few actions for the screen in front of the user.</summary>
    Task<GuidePlan> PlanAsync(GuidePromptContext context, CancellationToken ct);

    /// <summary>Web hints for this request, so the route planner and the step planner share one search.</summary>
    Task<IReadOnlyList<GuideSearchResult>> NotesAsync(string request, CancellationToken ct);
}

/// <summary>
/// Asks the model for steps through the same StreamingAiClient as chat and Focus, so a local Ollama
/// model keeps the screen contents on the PC and the same provider setting applies.
///
/// Every call is retried. A dropped stream, a rate limit, or a 5xx from a cloud provider used to end
/// the whole guide, which is why a good model still felt unreliable over a task that makes a dozen
/// calls. A reply that is not usable JSON gets one blunter attempt before the guide gives up on it.
/// </summary>
public sealed class LlmGuidePlanner : IGuidePlanner
{
    private readonly StreamingAiClient _client;
    private readonly GuideSettings _settings;
    private readonly Func<IGuideWebSearch> _search;
    private string? _notesRequest;
    private IReadOnlyList<GuideSearchResult>? _notes;

    public LlmGuidePlanner(StreamingAiClient client, GuideSettings settings, Func<IGuideWebSearch>? search = null)
    {
        _client = client;
        _settings = settings;
        _search = search ?? (() => GuideSearchFactory.Create(settings));
    }

    /// <summary>Enough tokens for a few steps.</summary>
    private AiRequestOptions Options => _settings.ForRequest(_client.Provider, 700, 0);

    /// <summary>
    /// The repair attempt is allowed more room: a model that rambled once often rambles a little
    /// before the JSON, and a cut-off reply is not an improvement.
    /// </summary>
    private AiRequestOptions RepairOptions => _settings.ForRequest(_client.Provider, 900, 0);

    public async Task<GuidePlan> PlanAsync(GuidePromptContext context, CancellationToken ct)
    {
        try
        {
            return await GuideRetry.RunAsync(_ => AskAsync(GuidePrompt.Plan(context), Options, ct), ct);
        }
        catch (GuideUnreadablePlanException unreadable)
        {
            var repaired = context with { UnreadableReply = unreadable.Reply };
            return await AskAsync(GuidePrompt.Repair(repaired), RepairOptions, ct);
        }
    }

    /// <summary>
    /// Web hints, only when the user turned search on. Only the request and the OS name are sent, never
    /// anything read from the screen. A failed search means no notes, not a failed guide. The result is
    /// kept for the rest of this request so a mid-task replan does not search again.
    /// </summary>
    public async Task<IReadOnlyList<GuideSearchResult>> NotesAsync(string request, CancellationToken ct)
    {
        if (_notesRequest == request && _notes is { } cached) return cached;
        IReadOnlyList<GuideSearchResult> loaded;
        if (!_settings.WebSearch) loaded = Array.Empty<GuideSearchResult>();
        else
        {
            try { loaded = await _search().SearchAsync($"{request} {OsName}", ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { loaded = Array.Empty<GuideSearchResult>(); }
        }
        _notesRequest = request;
        _notes = loaded;
        return loaded;
    }

    private static string OsName => "Windows 11";

    private async Task<GuidePlan> AskAsync(string prompt, AiRequestOptions options, CancellationToken ct)
    {
        var reply = new StringBuilder();
        await foreach (var delta in _client.StreamAsync(prompt, Array.Empty<Models.Message>(), options, ct))
            reply.Append(delta);
        var text = reply.ToString();
        try { return GuidePlanParser.Parse(text); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GuideUnreadablePlanException(text);
        }
    }
}
