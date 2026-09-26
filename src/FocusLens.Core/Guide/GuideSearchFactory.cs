namespace FocusLens.Core.Guide;

/// <summary>Builds the search chain from settings: the user's SearXNG first if they set one, then DuckDuckGo.</summary>
public static class GuideSearchFactory
{
    public static IGuideWebSearch Create(GuideSettings settings)
    {
        var sources = new List<IGuideWebSearch>();
        if (!string.IsNullOrWhiteSpace(settings.SearxUrl)) sources.Add(new SearxSearch(settings.SearxUrl));
        sources.Add(new DuckDuckGoSearch());
        return new GuideSearchChain(sources);
    }
}
