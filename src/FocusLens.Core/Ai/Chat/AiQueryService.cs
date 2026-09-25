using System.Runtime.CompilerServices;
using FocusLens.Core.Models;
using FocusLens.Core.Repositories;

namespace FocusLens.Core.Ai.Chat;

/// <summary>
/// Answers a question about the user's activity: builds context, streams the prose answer,
/// then asks the chart probe whether a chart would help.
/// </summary>
public sealed class AiQueryService
{
    private readonly ConversationStore _store;
    private readonly ContextBuilder _contextBuilder;
    private readonly StreamingAiClient _client;
    private readonly ChartProbe _chartProbe;

    public AiQueryService(
        ConversationStore store, ActivityRepository repository, StreamingAiClient client, ChartProbe? chartProbe = null)
    {
        _store = store;
        _contextBuilder = new ContextBuilder(repository);
        _client = client;
        _chartProbe = chartProbe ?? ChartProbes.Default(client);
    }

    public async IAsyncEnumerable<StreamEvent> SendAsync(
        string userMessage, Conversation conversation, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var context = await _contextBuilder.BuildAsync(userMessage);
        var prompt = PromptBuilder.BuildStreamingPrompt(userMessage, context);
        var recent = HistoryTrimmer.Trim(
            await _store.RecentMessagesForContextAsync(conversation.Id, budgetTokens: 6000), userMessage);

        var prose = new System.Text.StringBuilder();
        var stream = _client.StreamAsync(prompt, recent, ct).GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                string? delta = null;
                Exception? failure = null;
                try
                {
                    if (await stream.MoveNextAsync()) delta = stream.Current;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failure = ex;
                }

                if (failure is not null)
                {
                    yield return new StreamEvent.Error(failure);
                    yield break;
                }
                if (delta is null) break;
                prose.Append(delta);
                yield return new StreamEvent.Token(delta);
            }
        }
        finally
        {
            await stream.DisposeAsync();
        }

        ChartPayload? chart = null;
        if (prose.Length > 0)
        {
            try { chart = await _chartProbe(userMessage, prose.ToString(), context, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { chart = null; }
        }
        yield return new StreamEvent.Done(chart);
    }
}
