namespace DxvkInstaller.Models;

public enum GameApi
{
    None,
    Direct3D8,
    Direct3D9,
    Direct3D10,
    Direct3D11,
    Direct3D12,
    OpenGL,
    Vulkan
}

public enum PeArchitecture
{
    X64,
    X86,
    Arm64,
    Unknown
}

public sealed record ApiVerdict(
    GameApi Api,
    PeArchitecture Architecture,
    string? DetectedVia,
    string Reason,
    IReadOnlyList<string> FilesToInstall,
    bool CanInstall)
{
    public string ArchLabel => Architecture switch
    {
        PeArchitecture.X64 => "x64",
        PeArchitecture.X86 => "x86 (32-bit)",
        PeArchitecture.Arm64 => "ARM64",
        _ => "unknown architecture"
    };

    /// <summary>Folder name inside the DXVK release archive for this architecture.</summary>
    public string ReleaseArchFolder => Architecture == PeArchitecture.X86 ? "x32" : "x64";
}
