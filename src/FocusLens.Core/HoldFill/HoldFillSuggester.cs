using System.Text;
using FocusLens.Core.Ai;
using FocusLens.Core.Guide;

namespace FocusLens.Core.HoldFill;

public interface IHoldFillSuggester
{
    Task<string?> SuggestAsync(SearchField field, HoldFillContext context, CancellationToken ct);
}

/// <summary>
/// Picks the text to offer for a search field, in this order:
///   1. the text a running Guide step asks for (no model call)
///   2. a recent answer for the same box (kept 2 minutes, so hovering again is instant)
///   3. a short answer from the model the user chose, through the same client as chat
///   4. the Focus goal, when the model is unavailable or had nothing
/// Returns null when there is nothing worth offering; the ring then never appears.
/// </summary>
public sealed class LlmHoldFillSuggester : IHoldFillSuggester
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(2);

    private readonly StreamingAiClient _client;
    private readonly GuideSettings _settings;
    private readonly Dictionary<string, (string? Text, DateTime At)> _cache = new();

    public LlmHoldFillSuggester(StreamingAiClient client, GuideSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    private AiRequestOptions Options => new()
    {
        MaxTokens = 24,
        Temperature = 0.2,
        DisableThinking = true,
        KeepAlive = "10m",
        OllamaModel = _settings.PlannerModel,
    };

    public async Task<string?> SuggestAsync(SearchField field, HoldFillContext context, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(context.GuideText)) return context.GuideText.Trim();

        var key = $"{field.AppName}|{field.WindowTitle}|{field.Label}";
        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.At < CacheFor) return hit.Text;
        }

        var text = await AskAsync(HoldFillPrompt.Build(field, context), ct) ?? Fallback(context);
        lock (_cache) _cache[key] = (text, DateTime.UtcNow);
        return text;
    }

    private async Task<string?> AskAsync(string prompt, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            var reply = new StringBuilder();
            await foreach (var delta in _client.StreamAsync(prompt, Array.Empty<Models.Message>(), Options, timeout.Token))
                reply.Append(delta);
            return HoldFillPrompt.Clean(reply.ToString());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;    // too slow: fall back rather than keep the user waiting
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;    // model not reachable, no key: the fallback still works
        }
    }

    private static string? Fallback(HoldFillContext context) =>
        string.IsNullOrWhiteSpace(context.FocusGoal) ? null : context.FocusGoal.Trim();
}
