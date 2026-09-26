using System.Text;
using System.Text.RegularExpressions;

namespace FocusLens.Core.HoldFill;

/// <summary>What the suggestion is based on. Built by the app from recent activity and Focus Mode.</summary>
/// <param name="RecentTitles">Window titles from the last few minutes, newest first.</param>
/// <param name="FocusGoal">The goal of the running Focus session, if there is one.</param>
/// <param name="GuideText">Text a running Guide step asks the user to type. Used as is, without the model.</param>
public sealed record HoldFillContext(
    IReadOnlyList<string> RecentTitles,
    string? FocusGoal = null,
    string? GuideText = null);

/// <summary>The one-line request for a search suggestion, and the cleanup of the model's reply.</summary>
public static partial class HoldFillPrompt
{
    public const int MaxTitles = 15;
    public const int MaxLength = 80;

    public static string Build(SearchField field, HoldFillContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Guess what a person is about to type into a search box. Reply with the search text only.");
        sb.AppendLine();
        sb.AppendLine($"Search box: \"{Clip(field.Label, 60)}\" in {Clip(field.AppName, 40)}, window \"{Clip(field.WindowTitle, 90)}\".");
        if (!string.IsNullOrWhiteSpace(context.FocusGoal))
            sb.AppendLine($"They are in a focus session working on: {Clip(context.FocusGoal, 80)}");

        var titles = context.RecentTitles
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => Clip(t, 90))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTitles)
            .ToList();
        if (titles.Count > 0)
        {
            sb.AppendLine("What they looked at in the last 20 minutes, newest first:");
            foreach (var title in titles) sb.AppendLine("- " + title);
        }

        sb.AppendLine();
        sb.AppendLine("Rules: 2 to 6 words. No quotes, no explanation, no punctuation at the end. " +
                      "Fit the place: on a video site a video to watch, in a shop a product, in docs or code an API, " +
                      "error or topic, in a file or settings search the item's name. " +
                      "Prefer what they were just working on. If nothing fits, reply NONE.");
        return sb.ToString();
    }

    /// <summary>The model's reply as a query, or null when it gave nothing usable.</summary>
    public static string? Clean(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply)) return null;
        var text = ThinkBlock().Replace(reply, "");
        var line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        if (line is null) return null;

        line = Prefix().Replace(line, "");
        line = line.Trim().Trim('"', '\'', '`', '“', '”', '‘', '’', '*').Trim();
        line = line.TrimEnd('.', '!', ',', ';', ':').Trim();
        if (line.Length == 0 || line.Equals("NONE", StringComparison.OrdinalIgnoreCase)) return null;
        if (line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 12) return null;
        return line.Length > MaxLength ? line[..MaxLength].TrimEnd() : line;
    }

    private static string Clip(string? text, int max)
    {
        var t = (text ?? "").Replace('\n', ' ').Trim();
        return t.Length > max ? t[..max] : t;
    }

    [GeneratedRegex(@"<think>.*?(</think>|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkBlock();

    [GeneratedRegex(@"^\s*([-*•]\s*|\d+[.)]\s*|(search( text| query)?|query|answer)\s*:\s*)+", RegexOptions.IgnoreCase)]
    private static partial Regex Prefix();
}
