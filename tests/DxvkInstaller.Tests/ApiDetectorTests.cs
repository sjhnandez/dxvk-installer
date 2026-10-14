using DxvkInstaller.Models;
using DxvkInstaller.Services;
using Xunit;

namespace DxvkInstaller.Tests;

public class ApiDetectorTests
{
    [Fact]
    public void D3D11_wins_over_lower_and_blocked_apis()
    {
        // MCC-style import set: d3d9 + dxgi + d3d11 + delay-imported d3d12 + opengl32.
        var verdict = ApiDetector.Classify(
            new[] { "d3d9.dll", "dxgi.dll", "d3d11.dll", "d3d12.dll", "opengl32.dll" },
            PeArchitecture.X64);

        Assert.Equal(GameApi.Direct3D11, verdict.Api);
        Assert.True(verdict.CanInstall);
        Assert.Equal(new[] { "dxgi.dll", "d3d11.dll" }, verdict.FilesToInstall);
        Assert.Equal("x64", verdict.ReleaseArchFolder);
    }

    [Fact]
    public void D3D10_maps_to_dxgi_d3d10core_d3d11()
    {
        var verdict = ApiDetector.Classify(new[] { "d3d10.dll", "dxgi.dll" }, PeArchitecture.X64);

        Assert.Equal(GameApi.Direct3D10, verdict.Api);
        Assert.True(verdict.CanInstall);
        Assert.Equal(new[] { "dxgi.dll", "d3d10core.dll", "d3d11.dll" }, verdict.FilesToInstall);
    }

    [Fact]
    public void D3D10_1_variant_is_detected()
    {
        var verdict = ApiDetector.Classify(new[] { "d3d10_1.dll" }, PeArchitecture.X64);

        Assert.Equal(GameApi.Direct3D10, verdict.Api);
    }

    [Fact]
    public void D3D9_only_copies_d3d9()
    {
        var verdict = ApiDetector.Classify(new[] { "d3d9.dll" }, PeArchitecture.X86);

        Assert.Equal(GameApi.Direct3D9, verdict.Api);
        Assert.Equal(new[] { "d3d9.dll" }, verdict.FilesToInstall);
        Assert.Equal("x32", verdict.ReleaseArchFolder);
    }

    [Fact]
    public void D3D8_wins_over_d3d9_and_copies_both()
    {
        var verdict = ApiDetector.Classify(new[] { "d3d8.dll", "d3d9.dll" }, PeArchitecture.X64);

        Assert.Equal(GameApi.Direct3D8, verdict.Api);
        Assert.Equal(new[] { "d3d8.dll", "d3d9.dll" }, verdict.FilesToInstall);
    }

    [Fact]
    public void Dxgi_alone_is_not_enough()
    {
        var verdict = ApiDetector.Classify(new[] { "dxgi.dll", "kernel32.dll" }, PeArchitecture.X64);

        Assert.Equal(GameApi.None, verdict.Api);
        Assert.False(verdict.CanInstall);
        Assert.Empty(verdict.FilesToInstall);
    }

    [Fact]
    public void D3D12_is_blocked()
    {
        var verdict = ApiDetector.Classify(new[] { "d3d12.dll" }, PeArchitecture.X64);

        Assert.Equal(GameApi.Direct3D12, verdict.Api);
        Assert.False(verdict.CanInstall);
    }

    [Fact]
    public void OpenGL_is_blocked()
    {
        var verdict = ApiDetector.Classify(new[] { "opengl32.dll" }, PeArchitecture.X64);

        Assert.Equal(GameApi.OpenGL, verdict.Api);
        Assert.False(verdict.CanInstall);
    }

    [Fact]
    public void Vulkan_is_blocked()
    {
        var verdict = ApiDetector.Classify(new[] { "vulkan-1.dll" }, PeArchitecture.X64);

        Assert.Equal(GameApi.Vulkan, verdict.Api);
        Assert.False(verdict.CanInstall);
    }

    [Fact]
    public void No_markers_is_blocked()
    {
        var verdict = ApiDetector.Classify(new[] { "kernel32.dll", "user32.dll" }, PeArchitecture.X64);

        Assert.Equal(GameApi.None, verdict.Api);
        Assert.False(verdict.CanInstall);
    }

    [Fact]
    public void Arm64_is_blocked_even_with_d3d11()
    {
        var verdict = ApiDetector.Classify(new[] { "d3d11.dll", "dxgi.dll" }, PeArchitecture.Arm64);

        Assert.Equal(GameApi.Direct3D11, verdict.Api);
        Assert.False(verdict.CanInstall);
        Assert.Contains("ARM64", verdict.Reason);
    }

    [Fact]
    public void Hard_d3d9_wins_over_delay_d3d10_gow2_shape()
    {
        // Gears of War 2 - Hollow (UE3, x86): d3d9 is hard-imported while
        // d3d10/dxgi are only delay-loaded. Must stay on the Direct3D 9 path.
        var verdict = ApiDetector.Classify(
            new[] { "d3d9.dll", "d3dx9_43.dll", "d3dcompiler_43.dll", "kernel32.dll", "steam_api.dll" },
            new[] { "d3d10.dll", "dxgi.dll", "nvtt.dll", "physxloader.dll" },
            PeArchitecture.X86);

        Assert.Equal(GameApi.Direct3D9, verdict.Api);
        Assert.True(verdict.CanInstall);
        Assert.Equal(new[] { "d3d9.dll" }, verdict.FilesToInstall);
        Assert.Equal("x32", verdict.ReleaseArchFolder);
    }

    [Fact]
    public void Delay_only_d3d11_still_classifies()
    {
        var verdict = ApiDetector.Classify(
            new[] { "kernel32.dll" },
            new[] { "d3d11.dll", "dxgi.dll" },
            PeArchitecture.X64);

        Assert.Equal(GameApi.Direct3D11, verdict.Api);
        Assert.True(verdict.CanInstall);
        Assert.Equal(new[] { "dxgi.dll", "d3d11.dll" }, verdict.FilesToInstall);
    }

    [Fact]
    public void Hard_d3d11_wins_over_delay_d3d9()
    {
        var verdict = ApiDetector.Classify(
            new[] { "d3d11.dll", "dxgi.dll" },
            new[] { "d3d9.dll" },
            PeArchitecture.X64);

        Assert.Equal(GameApi.Direct3D11, verdict.Api);
    }

    [Fact]
    public void Hard_d3d9_wins_over_delay_d3d11()
    {
        var verdict = ApiDetector.Classify(
            new[] { "d3d9.dll" },
            new[] { "d3d11.dll", "dxgi.dll" },
            PeArchitecture.X86);

        Assert.Equal(GameApi.Direct3D9, verdict.Api);
        Assert.Equal(new[] { "d3d9.dll" }, verdict.FilesToInstall);
    }

    [Fact]
    public void Hard_table_precedence_is_unchanged_d3d10_over_d3d9()
    {
        // Documents that priority within a single table is untouched.
        var verdict = ApiDetector.Classify(
            new[] { "d3d9.dll", "d3d10.dll" },
            Array.Empty<string>(),
            PeArchitecture.X64);

        Assert.Equal(GameApi.Direct3D10, verdict.Api);
    }
}
