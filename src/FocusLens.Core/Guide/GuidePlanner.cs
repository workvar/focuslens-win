using System.Text;
using FocusLens.Core.Ai;

namespace FocusLens.Core.Guide;

public interface IGuidePlanner
{
    Task<IReadOnlyList<GuideStep>> PlanAsync(string request, IReadOnlyList<GuideElement> screen, CancellationToken ct);

    Task<IReadOnlyList<GuideStep>> ReplanAsync(string request, IReadOnlyList<GuideStep> done, GuideStep failed,
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

    public async Task<IReadOnlyList<GuideStep>> PlanAsync(string request, IReadOnlyList<GuideElement> screen, CancellationToken ct) =>
        await AskAsync(GuidePrompt.Plan(request, screen, Os, await NotesAsync(request, ct)), ct);

    public async Task<IReadOnlyList<GuideStep>> ReplanAsync(string request, IReadOnlyList<GuideStep> done, GuideStep failed,
        IReadOnlyList<GuideElement> screen, CancellationToken ct) =>
        await AskAsync(GuidePrompt.Replan(request, done, failed, screen, Os, await NotesAsync(request, ct)), ct);

    /// <summary>
    /// Web hints, only when the user turned search on. Only the request and the OS name are sent, never
    /// anything read from the screen. A failed search means no notes, not a failed guide.
    /// </summary>
    private async Task<IReadOnlyList<GuideSearchResult>> NotesAsync(string request, CancellationToken ct)
    {
        if (!_settings.WebSearch) return Array.Empty<GuideSearchResult>();
        try { return await _search().SearchAsync($"{request} {OsName}", ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Array.Empty<GuideSearchResult>(); }
    }

    private static string OsName => "Windows 11";

    private async Task<IReadOnlyList<GuideStep>> AskAsync(string prompt, CancellationToken ct)
    {
        var reply = new StringBuilder();
        await foreach (var delta in _client.StreamAsync(prompt, Array.Empty<FocusLens.Core.Models.Message>(), Options, ct))
            reply.Append(delta);
        return GuidePlanParser.Parse(reply.ToString());
    }
}
