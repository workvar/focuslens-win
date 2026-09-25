using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace FocusLens.App.Services.Auth;

/// <summary>PKCE helpers and a one-shot loopback listener that captures the OAuth redirect.</summary>
public static class PkceFlow
{
    public static (string Verifier, string Challenge) CreatePair()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    /// <summary>Picks a free loopback port and returns the redirect URI to register with the provider.</summary>
    public static (HttpListener Listener, string RedirectUri) StartListener()
    {
        var port = FreePort();
        var prefix = $"http://127.0.0.1:{port}/callback/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        return (listener, prefix.TrimEnd('/'));
    }

    /// <summary>Waits for the browser redirect and returns its query string values.</summary>
    public static async Task<IReadOnlyDictionary<string, string>> WaitForCallbackAsync(
        HttpListener listener, TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        using var registration = timeoutCts.Token.Register(listener.Stop);

        try
        {
            var context = await listener.GetContextAsync();
            var query = System.Web.HttpUtility.ParseQueryString(context.Request.Url?.Query ?? "");
            var values = query.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => query[k] ?? "");

            var page = Encoding.UTF8.GetBytes(
                "<html><body style=\"font-family:Segoe UI,sans-serif;text-align:center;margin-top:20vh\">" +
                "<h2>You're signed in to FocusLens</h2><p>You can close this tab.</p></body></html>");
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = page.Length;
            await context.Response.OutputStream.WriteAsync(page, ct);
            context.Response.Close();
            return values;
        }
        catch (Exception) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException("Sign-in timed out.");
        }
        finally
        {
            listener.Close();
        }
    }

    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
