using DxvkInstaller.Models;
using DxvkInstaller.Services;
using Xunit;

namespace DxvkInstaller.Tests;

public class ReleaseParserTests
{
    [Fact]
    public void Parses_github_latest_and_skips_native_assets()
    {
        const string json = """
        {
          "tag_name": "v3.1.1",
          "assets": [
            { "name": "dxvk-native-3.1.1-steamrt-sniper.tar.gz", "browser_download_url": "https://example.com/native.tar.gz" },
            { "name": "dxvk-3.1.1.tar.gz", "browser_download_url": "https://github.com/doitsujin/dxvk/releases/download/v3.1.1/dxvk-3.1.1.tar.gz" }
          ]
        }
        """;

        var release = ReleaseParser.ParseGitHubLatest(json);

        Assert.Equal(DxvkEdition.Upstream, release.Edition);
        Assert.Equal("v3.1.1", release.Tag);
        Assert.Equal("dxvk-3.1.1.tar.gz", release.AssetName);
        Assert.EndsWith("dxvk-3.1.1.tar.gz", release.DownloadUrl);
    }

    [Fact]
    public void Prefers_the_tar_gz_release_link_on_gitlab()
    {
        const string json = """
        [
          {
            "tag_name": "v3.1.1-1",
            "assets": {
              "links": [
                { "name": "dxvk-gplasync-v3.1.1-1.zip", "url": "https://gitlab.com/Ph42oN/dxvk-gplasync/-/jobs/1/artifacts/download?file_type=archive" },
                { "name": "dxvk-gplasync-v3.1.1-1.tar.gz", "url": "https://gitlab.com/Ph42oN/dxvk-gplasync/-/raw/main/releases/dxvk-gplasync-v3.1.1-1.tar.gz?ref_type=heads" }
              ]
            }
          }
        ]
        """;

        var release = ReleaseParser.ParseGitLabLatest(json);

        Assert.Equal(DxvkEdition.GplAsync, release.Edition);
        Assert.Equal("v3.1.1-1", release.Tag);
        Assert.Equal("dxvk-gplasync-v3.1.1-1.tar.gz", release.AssetName);
        Assert.Contains("/raw/main/releases/dxvk-gplasync-v3.1.1-1.tar.gz", release.DownloadUrl);
    }

    [Fact]
    public void Constructs_the_download_url_when_no_links_exist()
    {
        const string json = """
        [ { "tag_name": "v2.0-1", "assets": { "links": [] } } ]
        """;

        var release = ReleaseParser.ParseGitLabLatest(json);

        Assert.Equal("dxvk-gplasync-v2.0-1.tar.gz", release.AssetName);
        Assert.Equal("https://gitlab.com/Ph42oN/dxvk-gplasync/-/raw/main/releases/dxvk-gplasync-v2.0-1.tar.gz", release.DownloadUrl);
    }

    [Fact]
    public void Parses_latest_tag_from_atom_feed()
    {
        const string atom = """
        <feed>
          <entry>
            <link href="https://gitlab.com/Ph42oN/dxvk-gplasync/-/releases/v3.1.1-1"/>
          </entry>
          <entry>
            <link href="https://gitlab.com/Ph42oN/dxvk-gplasync/-/releases/v3.0-1"/>
          </entry>
        </feed>
        """;

        Assert.Equal("v3.1.1-1", ReleaseParser.ParseLatestTagFromAtom(atom));
    }
}
