using System.Text;
using FocusLens.Core.Ai;

namespace FocusLens.Core.Guide;

public interface IGuidePlanner
{
    /// <summary>
    /// The next few actions for the screen in front of the user. <paramref name="done"/> is what already
    /// happened; <paramref name="missed"/> is set when the previous step's control was not on screen.
    /// </summary>
    Task<GuidePlan> PlanAsync(string request, IReadOnlyList<GuideStep> done, GuideStep? missed,
        IReadOnlyList<GuideElement> screen, CancellationToken ct);
}

/// <summary>
/// Asks the model for steps through the same StreamingAiClient as chat and Focus, so a local
/// Ollama model keeps the screen contents on the PC and the same provider setting applies.
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

    private static string Os => "Windows " + Environment.OSVersion.Version;

    private AiRequestOptions Options => new()
    {
        MaxTokens = 700,
        Temperature = 0,
        DisableThinking = true,
        KeepAlive = "10m",
        OllamaModel = _settings.PlannerModel,
    };

    public async Task<GuidePlan> PlanAsync(string request, IReadOnlyList<GuideStep> done, GuideStep? missed,
        IReadOnlyList<GuideElement> screen, CancellationToken ct) =>
        await AskAsync(GuidePrompt.Plan(request, done, missed, screen, Os, await NotesAsync(request, ct)), ct);

    /// <summary>
    /// Web hints, only when the user turned search on. Only the request and the OS name are sent, never
    /// anything read from the screen. A failed search means no notes, not a failed guide. The result is
    /// kept for the rest of this request so a mid-task replan does not search again.
    /// </summary>
    private async Task<IReadOnlyList<GuideSearchResult>> NotesAsync(string request, CancellationToken ct)
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

    private async Task<GuidePlan> AskAsync(string prompt, CancellationToken ct)
    {
        var reply = new StringBuilder();
        await foreach (var delta in _client.StreamAsync(prompt, Array.Empty<FocusLens.Core.Models.Message>(), Options, ct))
            reply.Append(delta);
        return GuidePlanParser.Parse(reply.ToString());
    }
}
