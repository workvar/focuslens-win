using System.Text;
using FocusLens.Core.Ai;

namespace FocusLens.Core.Guide;

public interface IGuideRouter
{
    /// <summary>The route for this task, or null. Null is normal: a guide with no route just steers one screen at a time.</summary>
    Task<GuideRoute?> RouteAsync(string request, GuideSystem system, IReadOnlyList<GuideOpenApp> apps,
        IReadOnlyList<GuideElement> screen, CancellationToken ct);
}

/// <summary>
/// Makes the route, once, before the first step. Never fatal: a guide with no route behaves exactly
/// as it did before, one screen at a time. That matters, because the route is an improvement to
/// steering, not a thing the user asked for, and it must not become a new way for the guide to fail.
/// </summary>
public sealed class LlmGuideRoutePlanner : IGuideRouter
{
    private readonly StreamingAiClient _client;
    private readonly GuideSettings _settings;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<GuideSearchResult>>> _notes;

    public LlmGuideRoutePlanner(StreamingAiClient client, GuideSettings settings,
        Func<string, CancellationToken, Task<IReadOnlyList<GuideSearchResult>>>? notes = null)
    {
        _client = client;
        _settings = settings;
        _notes = notes ?? ((_, _) => Task.FromResult<IReadOnlyList<GuideSearchResult>>(Array.Empty<GuideSearchResult>()));
    }

    /// <summary>Small and cheap: a route is three short lines of text.</summary>
    private AiRequestOptions Options => _settings.ForRequest(_client.Provider, 320, 0);

    public async Task<GuideRoute?> RouteAsync(string request, GuideSystem system,
        IReadOnlyList<GuideOpenApp> apps, IReadOnlyList<GuideElement> screen, CancellationToken ct)
    {
        try
        {
            var hints = await _notes(request, ct);
            var prompt = GuideRoutePrompt.Route(request, system, apps, screen, hints);
            // Two tries only. The route is a nicety; the user should not wait on it.
            var route = await GuideRetry.RunAsync(async _ =>
            {
                var reply = new StringBuilder();
                await foreach (var delta in _client.StreamAsync(prompt, Array.Empty<Models.Message>(), Options, ct))
                    reply.Append(delta);
                return GuideRouteParser.Parse(reply.ToString())
                    ?? throw new GuideUnreadablePlanException(reply.ToString());
            }, ct, attempts: 2);
            return route.IsUsable ? route : null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return null;
        }
    }
}
