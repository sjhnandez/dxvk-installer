using DxvkInstaller.Services;
using Xunit;

namespace DxvkInstaller.Tests;

public sealed class InstallServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _gameDir;
    private readonly string _archDir;
    private readonly InstallService _service = new();

    public InstallServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dxvk-installer-tests", Guid.NewGuid().ToString("N"));
        _gameDir = Path.Combine(_root, "game");
        _archDir = Path.Combine(_root, "x64");
        Directory.CreateDirectory(_gameDir);
        Directory.CreateDirectory(_archDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }

    private void CreateReleaseDll(string name) => File.WriteAllText(Path.Combine(_archDir, name), "dll");

    [Fact]
    public void GetConflicts_returns_empty_for_clean_folder()
    {
        Assert.Empty(_service.GetConflicts(_gameDir, new[] { "dxgi.dll", "d3d11.dll" }));
    }

    [Fact]
    public void GetConflicts_finds_existing_files_and_conf()
    {
        File.WriteAllText(Path.Combine(_gameDir, "dxgi.dll"), "old");
        File.WriteAllText(Path.Combine(_gameDir, "dxvk.conf"), "old conf");

        var conflicts = _service.GetConflicts(_gameDir, new[] { "dxgi.dll", "d3d11.dll" });

        Assert.Contains("dxgi.dll", conflicts);
        Assert.Contains("dxvk.conf", conflicts);
        Assert.DoesNotContain("d3d11.dll", conflicts);
    }

    [Fact]
    public void OverwriteInstall_copies_dlls_and_writes_non_empty_conf()
    {
        CreateReleaseDll("dxgi.dll");
        CreateReleaseDll("d3d11.dll");

        var outcome = _service.OverwriteInstall(_gameDir, _archDir, new[] { "dxgi.dll", "d3d11.dll" }, "dxvk.allowFse = True");

        Assert.True(outcome.ConfWritten);
        Assert.Equal(new[] { "dxgi.dll", "d3d11.dll", "dxvk.conf" }, outcome.CopiedFiles);
        Assert.True(File.Exists(Path.Combine(_gameDir, "dxgi.dll")));
        var conf = File.ReadAllText(Path.Combine(_gameDir, "dxvk.conf"));
        Assert.Contains("dxvk.allowFse = True", conf);
    }

    [Fact]
    public void OverwriteInstall_skips_conf_when_profile_is_empty()
    {
        CreateReleaseDll("dxgi.dll");
        CreateReleaseDll("d3d11.dll");

        var outcome = _service.OverwriteInstall(_gameDir, _archDir, new[] { "dxgi.dll", "d3d11.dll" }, "   \r\n  ");

        Assert.False(outcome.ConfWritten);
        Assert.False(File.Exists(Path.Combine(_gameDir, "dxvk.conf")));
        Assert.Equal(new[] { "dxgi.dll", "d3d11.dll" }, outcome.CopiedFiles);
    }

    [Fact]
    public void OverwriteInstall_leaves_existing_conf_untouched_when_profile_is_empty()
    {
        CreateReleaseDll("dxgi.dll");
        CreateReleaseDll("d3d11.dll");
        var confPath = Path.Combine(_gameDir, "dxvk.conf");
        File.WriteAllText(confPath, "existing");

        _service.OverwriteInstall(_gameDir, _archDir, new[] { "dxgi.dll", "d3d11.dll" }, "");

        Assert.Equal("existing", File.ReadAllText(confPath));
    }

    [Fact]
    public void OverwriteInstall_throws_when_release_file_is_missing()
    {
        CreateReleaseDll("dxgi.dll");

        Assert.Throws<FileNotFoundException>(() =>
            _service.OverwriteInstall(_gameDir, _archDir, new[] { "dxgi.dll", "d3d11.dll" }, null));
    }

    [Fact]
    public void Uninstall_removes_only_dxvk_files()
    {
        foreach (var name in InstallService.DxvkFileSet.Append("dxvk.conf"))
        {
            File.WriteAllText(Path.Combine(_gameDir, name), "x");
        }

        File.WriteAllText(Path.Combine(_gameDir, "game.exe"), "exe");
        File.WriteAllText(Path.Combine(_gameDir, "readme.txt"), "txt");

        var removed = _service.UninstallDxvkFiles(_gameDir);

        Assert.Equal(6, removed.Count);
        Assert.True(File.Exists(Path.Combine(_gameDir, "game.exe")));
        Assert.True(File.Exists(Path.Combine(_gameDir, "readme.txt")));
        Assert.False(File.Exists(Path.Combine(_gameDir, "dxgi.dll")));
        Assert.False(File.Exists(Path.Combine(_gameDir, "dxvk.conf")));
    }
}
