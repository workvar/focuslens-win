using System.Text;

namespace FocusLens.Core.Ai.Chat;

/// <summary>Builds the analyst prompt from a QueryContext.</summary>
public static class PromptBuilder
{
    public static string BuildStreamingPrompt(string question, QueryContext context)
    {
        var categoryLines = Lines(context.CategoryTotals.OrderByDescending(kv => kv.Value)
            .Select(kv => $"  - {kv.Key}: {FormatSeconds(kv.Value)}"));
        var appLines = Lines(context.TopApps.Take(8).Select(a => $"  - {a.Name}: {FormatSeconds(a.Seconds)}"));
        var scoreLines = Lines(context.DailyFocusScores.Select(s => $"  {s.Date}: {s.Score:0}%"));

        var askedApp = MatchedApp(question, context);
        var mode = askedApp is not null
            ? $"The user is asking specifically about {askedApp}. Write a DETAILED report of what they did inside it: " +
              "group the window titles/URLs into the tasks, files, directories, projects, or sites they represent, " +
              "and give the time spent on each. Where a title looks like a shell command, working directory, or " +
              "tool invocation, call it out explicitly. Use a short intro line then a markdown list. You may go up to 300 words."
            : "Keep the answer under 150 words.";

        var prompt = new StringBuilder();
        prompt.AppendLine("You are FocusLens, a personal productivity analyst. Answer the user's question about their " +
                          "Windows PC usage using ONLY the data below. Reply in clear markdown (you may use **bold**, " +
                          "lists, and short fenced code blocks). Use specific numbers (hours, minutes, percentages). " +
                          "Do not invent activity that is not in the data. Use SESSION CONTEXT to say what the user " +
                          "read, wrote, or discussed, and give the exact URL when they ask for a link. " +
                          "Answer ONLY the latest question and never repeat or restate an earlier answer. " +
                          "If the data does not contain what was asked, say so in one or two sentences, mention what was " +
                          "searched (see SEARCH NOTE) and what could be missing, for example that the app's text is not " +
                          "being captured. Never fall back to a generic list of apps or a focus score unless asked. " + mode);
        prompt.AppendLine();
        prompt.AppendLine($"TIME PERIOD: {context.PeriodLabel} ({context.SummaryCount} days of data)");
        prompt.AppendLine();
        prompt.AppendLine("CATEGORY BREAKDOWN:").AppendLine(OrNone(categoryLines, "  No data available")).AppendLine();
        prompt.AppendLine("TOP APPS:").AppendLine(OrNone(appLines, "  No data available")).AppendLine();
        prompt.AppendLine("DAILY FOCUS SCORES:").AppendLine(OrNone(scoreLines, "  No data available")).AppendLine();
        prompt.AppendLine("ACTIVITY DETAIL (window titles / sites, with time spent):")
            .AppendLine(OrNone(BuildDetailBlock(context), "  No detailed activity recorded")).AppendLine();
        prompt.AppendLine("SEARCH NOTE (what was looked up for this question):")
            .AppendLine(OrNone(context.RetrievalNote, "  No search performed")).AppendLine();
        prompt.AppendLine("SESSION CONTEXT (text that appeared on screen, grouped by session, oldest first; each session " +
                          "lists only text that newly appeared during it; sessions marked background were visible but " +
                          "not focused, so they do not count as focused time):")
            .AppendLine(OrNone(context.SessionContext, "  No on-screen text recorded")).AppendLine();
        prompt.AppendLine("OTHER SIGNALS (input counts, open files, copy counts, lock and sleep events):")
            .AppendLine(OrNone(context.Signals, "  No signals recorded")).AppendLine();
        prompt.AppendLine("OPEN BROWSER TABS (every tab seen open, including background tabs):")
            .AppendLine(OrNone(context.OpenTabs, "  No tabs recorded")).AppendLine();
        prompt.Append($"USER'S QUESTION: {question}");
        return prompt.ToString();
    }

    public static string FormatSeconds(int seconds) =>
        seconds >= 3600 ? $"{seconds / 3600.0:0.0} hrs" : $"{seconds / 60} min";

    private static string BuildDetailBlock(QueryContext context) =>
        string.Join("\n", context.AppDetails.Select(app =>
        {
            var lines = new List<string> { $"  {app.Name} - {FormatSeconds(app.TotalSeconds)}:" };
            lines.AddRange(app.Titles.Take(8).Select(i => $"    - {Truncate(i.Label)} ({FormatSeconds(i.Seconds)})"));
            lines.AddRange(app.Urls.Take(6).Select(i => $"    - {Truncate(i.Label)} ({FormatSeconds(i.Seconds)})"));
            return string.Join("\n", lines);
        }));

    private static string? MatchedApp(string question, QueryContext context)
    {
        var q = question.ToLowerInvariant();
        var names = context.AppDetails.Select(a => a.Name).Concat(context.TopApps.Select(a => a.Name));
        foreach (var name in names.OrderByDescending(n => n.Length))
        {
            var key = name.ToLowerInvariant();
            if (key.Length > 0 && q.Contains(key)) return name;
        }
        return null;
    }

    private static string Truncate(string text, int max = 90) => text.Length <= max ? text : text[..max] + "...";
    private static string Lines(IEnumerable<string> lines) => string.Join("\n", lines);
    private static string OrNone(string text, string none) => string.IsNullOrEmpty(text) ? none : text;
}
