using System.Text;
using FocusLens.Core.Ai;
using FocusLens.Core.Models;

namespace FocusLens.Core.Focus.Classification;

public interface IFocusClassifier
{
    /// <summary>Answers without the model (rules, keywords, cache), or null when only the model can tell.</summary>
    FocusVerdict? QuickVerdict(string goal, FocusContext context);
    /// <summary>The quick checks, then the model.</summary>
    Task<FocusVerdict> VerdictAsync(string goal, FocusContext context);
    void Reset();
}

/// <summary>
/// Decides whether the foreground window is on topic. Order of checks, cheapest first:
/// never-a-distraction rules, goal keywords, cache, then the model. Uses the same provider as
/// chat (Ollama by default), so the title and URL never leave the PC unless the user chose a
/// cloud model.
///
/// Cost control: a local model runs on the same CPU and GPU the user works on, so every call
/// uses <see cref="AiRequestOptions.Classification"/> (a few tokens, no reasoning, model kept
/// warm) and each page is asked about once (see <see cref="FocusVerdictCache"/>).
/// </summary>
public sealed class LlmFocusClassifier : IFocusClassifier
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(12);
    /// <summary>Safety net for providers that ignore the token limit.</summary>
    private const int MaxCharacters = 200;

    private readonly StreamingAiClient _client;
    private readonly FocusSettings _settings;
    private readonly Action<string>? _log;
    private readonly FocusVerdictCache _cache = new();
    private readonly object _gate = new();

    public LlmFocusClassifier(StreamingAiClient client, FocusSettings settings, Action<string>? log = null)
    {
        _client = client;
        _settings = settings;
        _log = log;
    }

    public void Reset()
    {
        lock (_gate) _cache.Clear();
    }

    public FocusVerdict? QuickVerdict(string goal, FocusContext context)
    {
        if (FocusRules.IsAllowed(context, _settings.AllowlistEntries)) return FocusVerdict.OnTopic;
        if (FocusGoalKeywords.Matches(goal, new[] { context.AppName, context.WindowTitle, context.Url ?? "" }))
            return FocusVerdict.OnTopic;

        // Nothing to judge. Never ask, never cache: a blank reading would be called on topic.
        if (!context.IsJudgeable) return FocusVerdict.Unknown;

        lock (_gate) return _cache.Lookup(FocusVerdictCache.Key(goal, context), DateTime.UtcNow);
    }

    public async Task<FocusVerdict> VerdictAsync(string goal, FocusContext context)
    {
        if (QuickVerdict(goal, context) is { } quick) return quick;

        var result = await AskAsync(goal, context).ConfigureAwait(false);
        lock (_gate) _cache.Store(result, FocusVerdictCache.Key(goal, context), DateTime.UtcNow);
        _log?.Invoke($"focus model: {result} for {context.Key}");
        return result;
    }

    private async Task<FocusVerdict> AskAsync(string goal, FocusContext context)
    {
        var options = _settings.ClassificationOptions(_client.Provider);
        using var cts = new CancellationTokenSource(Timeout);
        var text = new StringBuilder();
        try
        {
            var prompt = FocusPrompt.Build(goal, context);
            await foreach (var delta in _client.StreamAsync(prompt, Array.Empty<Message>(), options, cts.Token).ConfigureAwait(false))
            {
                text.Append(delta);
                if (text.Length > MaxCharacters) break;
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"focus model call failed: {ex.Message}");
            return FocusVerdict.Unknown;
        }

        var verdict = FocusPrompt.Parse(text.ToString());
        if (verdict == FocusVerdict.Unknown)
            _log?.Invoke($"focus model reply not understood: {text.ToString()[..Math.Min(80, text.Length)]}");
        return verdict;
    }
}
