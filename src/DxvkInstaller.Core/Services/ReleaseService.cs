using DxvkInstaller.Models;

namespace DxvkInstaller.Services;

public sealed class ReleaseService
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DxvkInstaller/0.1 (+https://github.com/sjhnandez/dxvk-installer)");
        return client;
    }

    public async Task<ReleaseInfo> GetLatestAsync(DxvkEdition edition, CancellationToken cancellationToken = default)
    {
        if (edition == DxvkEdition.Upstream)
        {
            const string url = "https://api.github.com/repos/doitsujin/dxvk/releases/latest";
            var json = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            return ReleaseParser.ParseGitHubLatest(json);
        }

        try
        {
            const string url = "https://gitlab.com/api/v4/projects/Ph42oN%2Fdxvk-gplasync/releases?per_page=1";
            var json = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            return ReleaseParser.ParseGitLabLatest(json);
        }
        catch
        {
            // Fallback chain: releases Atom feed, then the asset URL by convention.
            var feed = await GetStringAsync("https://gitlab.com/Ph42oN/dxvk-gplasync/-/releases.atom", cancellationToken).ConfigureAwait(false);
            var tag = ReleaseParser.ParseLatestTagFromAtom(feed)
                ?? throw new InvalidOperationException(
                    "Could not determine the latest DXVK-GPLAsync release. See https://gitlab.com/Ph42oN/dxvk-gplasync/-/releases");
            return new ReleaseInfo(DxvkEdition.GplAsync, tag, $"dxvk-gplasync-{tag}.tar.gz", ReleaseParser.BuildGplAsyncDownloadUrl(tag));
        }
    }

    public async Task<string> DownloadAsync(ReleaseInfo release, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var cacheDirectory = Path.Combine(AppPaths.CacheRoot, EditionFolder(release.Edition), SanitizeFileName(release.Tag));
        Directory.CreateDirectory(cacheDirectory);

        var targetPath = Path.Combine(cacheDirectory, SanitizeFileName(release.AssetName));
        if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
        {
            progress?.Report(1.0);
            return targetPath;
        }

        var temporaryPath = targetPath + ".part";
        using (var response = await Http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var destination = File.Create(temporaryPath);

            var buffer = new byte[81920];
            long readBytes = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                readBytes += read;
                if (totalBytes is > 0)
                {
                    progress?.Report((double)readBytes / totalBytes.Value);
                }
            }
        }

        File.Move(temporaryPath, targetPath, overwrite: true);
        progress?.Report(1.0);
        return targetPath;
    }

    private static string EditionFolder(DxvkEdition edition) =>
        edition == DxvkEdition.Upstream ? "upstream" : "gplasync";

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }

    private static async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}
