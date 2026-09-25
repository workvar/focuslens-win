namespace FocusLens.Core.Setup;

/// <summary>Streams a file to disk with progress. Only HTTPS URLs are accepted.</summary>
public static class ToolDownloader
{
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task DownloadAsync(string url, string destination, string label, IProgress<SetupProgress> progress, CancellationToken ct)
    {
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new SetupException("Refusing to download over an insecure connection.");

        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1;

            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = File.Create(destination);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                progress.Report(new SetupProgress(
                    total > 0 ? (double)done / total : -1,
                    total > 0 ? $"Downloading {label}: {done / 1_048_576} of {total / 1_048_576} MB" : $"Downloading {label}: {done / 1_048_576} MB"));
            }
        }
        catch (HttpRequestException ex)
        {
            TryDelete(destination);
            throw new SetupException($"Could not download {label}. Check your internet connection and try again.", ex);
        }
        catch (OperationCanceledException)
        {
            TryDelete(destination);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* a partial download in temp is harmless */ }
    }
}
