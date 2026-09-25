namespace FocusLens.Core.Summary;

/// <summary>
/// focusScore = deepWorkSeconds / totalActiveSeconds * 100, capped at 100.
/// Only Deep Work counts in the numerator: communication and meetings are necessary
/// work, just not focus work.
/// </summary>
public static class FocusScore
{
    public const string DeepWorkCategory = "Deep Work";

    public static double Compute(int deepWorkSeconds, int totalActiveSeconds) =>
        totalActiveSeconds <= 0
            ? 0.0
            : Math.Min(100.0, deepWorkSeconds / (double)totalActiveSeconds * 100.0);
}
