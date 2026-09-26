using FocusLens.Core.Ai;

namespace FocusLens.Core.Guide;

/// <summary>
/// Which model failures are worth trying again, and how long to wait first.
///
/// A guide used to end the moment one call failed. Cloud providers drop a stream, answer 429, or
/// return a 5xx from an upstream node several times an hour; on a task that makes a dozen calls
/// that is close to a coin flip, which is why a good model still looked unreliable. A wrong API
/// key is different: trying again just wastes the user's time, so it stops at once.
/// </summary>
public static class GuideRetry
{
    /// <summary>Tries per model call, including the first.</summary>
    public const int Attempts = 3;

    private static readonly double[] BackoffSeconds = { 0.6, 1.5, 3.0 };

    public static TimeSpan Backoff(int attempt) =>
        TimeSpan.FromSeconds(BackoffSeconds[Math.Clamp(attempt - 1, 0, BackoffSeconds.Length - 1)]);

    /// <summary>True when the same request has a real chance of working next time.</summary>
    public static bool IsRetryable(Exception error) => error switch
    {
        GuideUnreadablePlanException => true,
        AiException ai => ai.Kind is AiErrorKind.Network or AiErrorKind.RateLimited
            or AiErrorKind.ServerError or AiErrorKind.StreamInterrupted or AiErrorKind.Parse
            or AiErrorKind.Api,
        OperationCanceledException => false,
        _ => true,
    };

    /// <summary>What to tell the user when every try failed. Specific, so it is never a mystery.</summary>
    public static string Message(Exception error) => error switch
    {
        GuideUnreadablePlanException =>
            "The model kept answering with something I could not read as a plan. Try a larger model in Settings > Guide.",
        AiException ai => ai.Message,
        _ => $"I could not plan that: {error.Message}",
    };

    /// <summary>Runs <paramref name="work"/>, retrying the failures above. Cancellation is never retried.</summary>
    public static async Task<T> RunAsync<T>(Func<int, Task<T>> work, CancellationToken ct,
        int attempts = Attempts, Action<int, Exception>? onRetry = null)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= Math.Max(attempts, 1); attempt++)
        {
            try { return await work(attempt); }
            catch (Exception ex)
            {
                ct.ThrowIfCancellationRequested();
                if (!IsRetryable(ex) || attempt >= attempts) throw;
                last = ex;
                onRetry?.Invoke(attempt, ex);
                await Task.Delay(Backoff(attempt), ct);
            }
        }
        throw last ?? new AiException(AiErrorKind.Parse);
    }
}

/// <summary>The model answered, but not with a plan Guide can use.</summary>
public sealed class GuideUnreadablePlanException : Exception
{
    public string Reply { get; }

    public GuideUnreadablePlanException(string reply) : base("The reply was not a usable plan") => Reply = reply;
}
