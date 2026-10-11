using System.Text.Json;
using System.Text.RegularExpressions;
using DxvkInstaller.Models;

namespace DxvkInstaller.Services;

public static class ReleaseParser
{
    public static ReleaseInfo ParseGitHubLatest(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString()
            ?? throw new InvalidDataException("GitHub release JSON has no tag_name.");

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            if (string.IsNullOrEmpty(name)
                || !name.StartsWith("dxvk-", StringComparison.OrdinalIgnoreCase)
                || !name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                || name.Contains("native", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = asset.GetProperty("browser_download_url").GetString()
                ?? throw new InvalidDataException("GitHub release asset has no browser_download_url.");
            return new ReleaseInfo(DxvkEdition.Upstream, tag, name, url);
        }

        throw new InvalidDataException("No dxvk-*.tar.gz asset found in the latest GitHub release.");
    }

    public static ReleaseInfo ParseGitLabLatest(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            throw new InvalidDataException("GitLab releases JSON is empty.");
        }

        var release = root[0];
        var tag = release.GetProperty("tag_name").GetString()
            ?? throw new InvalidDataException("GitLab release JSON has no tag_name.");

        string? assetName = null;
        string? downloadUrl = null;

        if (release.TryGetProperty("assets", out var assets)
            && assets.TryGetProperty("links", out var links)
            && links.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in links.EnumerateArray())
            {
                var name = link.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                var url = link.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(url)
                    && name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                {
                    assetName = name;
                    downloadUrl = url;
                    break;
                }
            }
        }

        // The fork serves the archive from its repository when no release link is present.
        assetName ??= $"dxvk-gplasync-{tag}.tar.gz";
        downloadUrl ??= BuildGplAsyncDownloadUrl(tag);

        return new ReleaseInfo(DxvkEdition.GplAsync, tag, assetName, downloadUrl);
    }

    public static string BuildGplAsyncDownloadUrl(string tag) =>
        $"https://gitlab.com/Ph42oN/dxvk-gplasync/-/raw/main/releases/dxvk-gplasync-{tag}.tar.gz";

    public static string? ParseLatestTagFromAtom(string atomXml)
    {
        var match = Regex.Match(atomXml, @"/-/releases/([^""/<\s]+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}
