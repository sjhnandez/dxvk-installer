using DxvkInstaller.Models;
using DxvkInstaller.Services;
using Xunit;

namespace DxvkInstaller.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _root;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dxvk-installer-tests", Guid.NewGuid().ToString("N"));
        _service = new SettingsService(_root);
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

    [Fact]
    public void Load_returns_seeded_defaults()
    {
        var settings = _service.Load();

        Assert.Equal(DxvkEdition.GplAsync, settings.SelectedEdition);
        Assert.Contains("dxvk.allowFse = True", settings.UpstreamConfText);
        Assert.Contains("dxvk.enableAsync = True", settings.GplAsyncConfText);
    }

    [Fact]
    public void Save_and_Load_round_trip()
    {
        var settings = _service.Load();
        settings.SelectedEdition = DxvkEdition.Upstream;
        settings.UpstreamConfText = "dxvk.hud = fps";
        settings.GplAsyncConfText = "";
        _service.Save(settings);

        var reloaded = _service.Load();

        Assert.Equal(DxvkEdition.Upstream, reloaded.SelectedEdition);
        Assert.Equal("dxvk.hud = fps", reloaded.UpstreamConfText);
        Assert.Equal("", reloaded.GplAsyncConfText);
    }

    [Fact]
    public void AppendInstallRecord_creates_and_appends()
    {
        var record = new InstallRecord(
            DateTimeOffset.UtcNow,
            "C:\\game\\game.exe",
            "C:\\game",
            "GplAsync",
            "v3.1.1-1",
            new[] { "dxgi.dll", "d3d11.dll" },
            true);

        _service.AppendInstallRecord(record);
        _service.AppendInstallRecord(record with { Version = "v3.2-1" });

        Assert.True(File.Exists(_service.InstallLogFilePath));
        var json = File.ReadAllText(_service.InstallLogFilePath);
        Assert.Contains("v3.1.1-1", json);
        Assert.Contains("v3.2-1", json);
    }
}
