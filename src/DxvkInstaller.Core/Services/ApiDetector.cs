using DxvkInstaller.Models;

namespace DxvkInstaller.Services;

public static class ApiDetector
{
    private static readonly string[] D3D11Files = { "dxgi.dll", "d3d11.dll" };
    private static readonly string[] D3D10Files = { "dxgi.dll", "d3d10core.dll", "d3d11.dll" };
    private static readonly string[] D3D9Files = { "d3d9.dll" };
    private static readonly string[] D3D8Files = { "d3d8.dll", "d3d9.dll" };
    private static readonly string[] D3D10Markers = { "d3d10.dll", "d3d10_1.dll", "d3d10core.dll" };

    public static ApiVerdict Classify(IReadOnlyCollection<string> importedModules, PeArchitecture architecture)
        => BuildVerdict(Match(importedModules), architecture);

    /// <summary>
    /// Classifies from the two import tables separately: a hard (startup) import is stronger
    /// evidence than a delay-load import, so the delay-load table is only consulted when the
    /// hard table yields no match. UE3 titles such as Gears of War 2 "Hollow" hard-import
    /// d3d9.dll but only delay-load d3d10.dll/dxgi.dll; they must classify as Direct3D 9.
    /// </summary>
    public static ApiVerdict Classify(IReadOnlyCollection<string> hardImports, IReadOnlyCollection<string> delayImports, PeArchitecture architecture)
    {
        var api = Match(hardImports);
        if (api == GameApi.None)
        {
            api = Match(delayImports);
        }

        return BuildVerdict(api, architecture);
    }

    private static ApiVerdict BuildVerdict(GameApi api, PeArchitecture architecture)
    {
        var files = api switch
        {
            GameApi.Direct3D11 => D3D11Files,
            GameApi.Direct3D10 => D3D10Files,
            GameApi.Direct3D9 => D3D9Files,
            GameApi.Direct3D8 => D3D8Files,
            _ => Array.Empty<string>()
        };

        var reason = api switch
        {
            GameApi.Direct3D11 => "Direct3D 11 detected.",
            GameApi.Direct3D10 => "Direct3D 10 detected.",
            GameApi.Direct3D9 => "Direct3D 9 detected.",
            GameApi.Direct3D8 => "Direct3D 8 detected.",
            GameApi.Direct3D12 => "Direct3D 12 game. DXVK does not support D3D12 on Windows.",
            GameApi.OpenGL => "OpenGL game. DXVK cannot help because nothing loads the d3d*/dxgi names.",
            GameApi.Vulkan => "Vulkan game. There is nothing to translate.",
            _ => "No Direct3D 8/9/10/11 usage found. Make sure you selected the game's rendering executable and not a launcher."
        };

        var canInstall = files.Length > 0;
        if (canInstall && architecture is not (PeArchitecture.X64 or PeArchitecture.X86))
        {
            canInstall = false;
            reason = $"A supported Direct3D API was detected, but the executable is {ArchLabel(architecture)}. DXVK ships x86/x64 builds only.";
        }

        return new ApiVerdict(api, architecture, null, reason, files, canInstall);
    }

    public static async Task<ApiVerdict> AnalyzeAsync(string exePath, CancellationToken cancellationToken = default)
    {
        var scan = await Task.Run(() => PeImportScanner.Scan(exePath), cancellationToken).ConfigureAwait(false);
        var verdict = Classify(scan.HardImports, scan.DelayImports, scan.Architecture);

        if (verdict.Api == GameApi.None)
        {
            // Engines such as Unity import Direct3D through a companion DLL,
            // so scan the sibling DLLs before declaring the game unsupported.
            var siblingVerdict = await Task.Run(() => ScanSiblingDlls(exePath, scan.Architecture, cancellationToken), cancellationToken).ConfigureAwait(false);
            verdict = siblingVerdict ?? verdict;
        }

        return verdict;
    }

    private static ApiVerdict? ScanSiblingDlls(string exePath, PeArchitecture architecture, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var union = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var perFile = new List<(string FileName, IReadOnlyList<string> Modules)>();

        try
        {
            foreach (var dll in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var (_, modules, _, _) = PeImportScanner.Scan(dll);
                    if (modules.Count == 0)
                    {
                        continue;
                    }

                    perFile.Add((Path.GetFileName(dll), modules));
                    foreach (var module in modules)
                    {
                        union.Add(module);
                    }
                }
                catch
                {
                    // Not a readable PE file: skip it.
                }
            }
        }
        catch
        {
            return null;
        }

        var unionVerdict = Classify(union, architecture);
        if (unionVerdict.Api == GameApi.None)
        {
            return null;
        }

        var marker = unionVerdict.Api switch
        {
            GameApi.Direct3D11 => "d3d11.dll",
            GameApi.Direct3D10 => D3D10Markers.FirstOrDefault(union.Contains),
            GameApi.Direct3D9 => "d3d9.dll",
            GameApi.Direct3D8 => "d3d8.dll",
            _ => null
        };
        var via = marker is null ? null : perFile.FirstOrDefault(entry => entry.Modules.Contains(marker!)).FileName;
        return unionVerdict with { DetectedVia = via };
    }

    private static GameApi Match(IReadOnlyCollection<string> modules)
    {
        if (modules.Contains("d3d11.dll")) return GameApi.Direct3D11;

        foreach (var marker in D3D10Markers)
        {
            if (modules.Contains(marker)) return GameApi.Direct3D10;
        }

        // D3D8 comes before D3D9 on purpose: a D3D8 install also needs d3d9.dll.
        if (modules.Contains("d3d8.dll")) return GameApi.Direct3D8;
        if (modules.Contains("d3d9.dll")) return GameApi.Direct3D9;

        if (modules.Contains("d3d12.dll") || modules.Contains("d3d12core.dll")) return GameApi.Direct3D12;
        if (modules.Contains("opengl32.dll")) return GameApi.OpenGL;
        if (modules.Contains("vulkan-1.dll")) return GameApi.Vulkan;
        return GameApi.None;
    }

    private static string ArchLabel(PeArchitecture architecture) => architecture switch
    {
        PeArchitecture.Arm64 => "ARM64",
        PeArchitecture.X64 => "x64",
        PeArchitecture.X86 => "x86",
        _ => "an unknown architecture"
    };
}
