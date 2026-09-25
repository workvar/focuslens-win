using System.Net;

namespace FocusLens.App.Services;

/// <summary>Turns an update failure into a message that says what actually went wrong.</summary>
internal static class UpdateErrors
{
    public static string Describe(Exception ex)
    {
        var status = FindStatus(ex);
        return status switch
        {
            HttpStatusCode.NotFound =>
                "GitHub answered, but found no release feed. The repository may be private or the latest release has no update files.",
            HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests =>
                "GitHub is limiting requests from this network. Try again in a few minutes.",
            not null => $"GitHub returned an error ({(int)status}). Try again later.",
            _ => "Could not reach GitHub. Check your connection and try again.",
        };
    }

    private static HttpStatusCode? FindStatus(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
            if (ex is HttpRequestException { StatusCode: { } code }) return code;
        return null;
    }
}
