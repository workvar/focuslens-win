namespace FocusLens.Core.Guide;

/// <summary>
/// The one-off question that produces the route. Deliberately short: it is asked before anything is
/// on the line, and a small local model has to be able to answer it. It never asks for control
/// labels, only for the shape of the task.
/// </summary>
public static class GuideRoutePrompt
{
    /// <summary>Fewer lines than the step prompt: the route only needs to know roughly where the user is.</summary>
    public const int MaxContextLines = 25;

    public static string Route(string request, GuideSystem system, IReadOnlyList<GuideOpenApp> apps,
        IReadOnlyList<GuideElement> screen, IReadOnlyList<GuideSearchResult>? notes = null) => $$"""
        A person asked for help with one task on their own computer: "{{request}}"

        {{GuidePrompt.SystemSection(system)}}

        Before any steps, say what the task is and the few stages it passes through.

        Reply with JSON only, no prose:
        {"goal":"turn Bluetooth on and pair the headphones","app":"Settings","browser":false,"milestones":["open the Bluetooth page","turn the switch on","pick the headphones from the list"]}

        - "goal" restates their task in one line, in your own words.
        - "app" names the one app the task mostly happens in. Use an app from "Already open" when one fits, otherwise one from "Apps installed". Never name an app this computer does not have. Leave it empty if you are not sure.
        - "browser" is true only when the task needs a web page.
        - "milestones" is 2 to {{GuideRoute.MaxMilestones}} stages in plain words, in order. No button names, no menu names, no URLs. A stage is something the user would recognise as progress, not a single click.
        - Do not include a stage for opening an app that is already listed under "Already open".

        {{GuidePrompt.AppsSection(apps)}}On screen now:
        {{GuidePrompt.ScreenSummary(screen, MaxContextLines)}}{{GuidePrompt.WebNotes(notes)}}
        """;
}
