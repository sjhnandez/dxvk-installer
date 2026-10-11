# DXVK Installer

![CI](https://github.com/sjhnandez/dxvk-installer/actions/workflows/ci.yml/badge.svg)

A small WinUI 3 app that installs the latest [DXVK](https://github.com/doitsujin/dxvk) or [DXVK-GPLAsync](https://gitlab.com/Ph42oN/dxvk-gplasync) release next to a game's executable, with automatic Direct3D API detection.

> **Disclaimer:** Manipulating Direct3D libraries in multiplayer games can be treated as cheating and can get your account banned. Use at your own risk. Prefer upstream DXVK in anything with anticheat, and no DXVK at all when in doubt.

## Features

- Pick a game's `.exe` with the Windows file picker.
- The executable's PE import table (including delay-load imports) is scanned to detect Direct3D 8/9/10/11. Anything else (D3D12, OpenGL, Vulkan, no Direct3D usage) is blocked with an explanation.
- One toggle switches between **DXVK (upstream)** and **DXVK-GPLAsync**. The latest release of the selected edition is fetched live from GitHub / GitLab (with download cache).
- Copies only the DLLs the detected API needs, for the right architecture (x86/x64), into the executable's folder.
- Two editable `dxvk.conf` profiles (one per edition), edited in a dialog with a Save button. An empty profile means no `dxvk.conf` is created. An empty profile never touches an existing `dxvk.conf`.
- If DXVK files already exist in the folder, a dialog offers Overwrite, Uninstall, or Cancel.
- Warns (without blocking) when known anticheat files (EAC, BattlEye, ...) are found in the game folder.
- Records every installed file in `%LOCALAPPDATA%\DxvkInstaller\installs.json` (your uninstall list).

## Detected API to files

| Detected API | Files installed | Release folder |
| --- | --- | --- |
| Direct3D 8 | `d3d8.dll`, `d3d9.dll` | `x32` / `x64` |
| Direct3D 9 | `d3d9.dll` | `x32` / `x64` |
| Direct3D 10 | `dxgi.dll`, `d3d10core.dll`, `d3d11.dll` | `x32` / `x64` |
| Direct3D 11 | `dxgi.dll`, `d3d11.dll` | `x32` / `x64` |

The highest detected API wins (a game importing both `d3d9.dll` and `d3d11.dll` is a Direct3D 11 install). If the selected executable shows no Direct3D imports at all, sibling DLLs in the same folder are scanned before giving up (covers engines like Unity that import through a companion DLL).

## Requirements

- Windows 10 version 1809 (build 17763) or newer, x64.
- Released builds are self-contained: no .NET or Windows App SDK runtime install needed.
- Building from source: .NET SDK 8.0.302 (pinned via `global.json`).

## Usage

1. Download the latest `dxvk-installer-<version>-win-x64.zip` from [Releases](https://github.com/sjhnandez/dxvk-installer/releases) and extract it.
2. Run `DxvkInstaller.exe`, click **Browse…** and pick the game's rendering executable (not a launcher).
3. Check the detection result (API and architecture).
4. Pick the edition and optionally edit the `dxvk.conf` profiles.
5. Click **Install DXVK**. If the folder requires admin rights, the app offers to relaunch elevated.

To uninstall per game: delete the files listed in `installs.json` (or shown in the result), plus `dxvk.conf` if you created one.

## Build from source

```powershell
dotnet build src/DxvkInstaller/DxvkInstaller.csproj -c Release -p:Platform=x64
```

Run the produced `DxvkInstaller.exe` from `src/DxvkInstaller/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/`.

Self-contained publish:

```powershell
dotnet publish src/DxvkInstaller/DxvkInstaller.csproj -c Release -p:Platform=x64 -o publish
```

(`win-x64` and self-contained settings live in the app's csproj; do not pass `-r`/`--self-contained` on the command line, as those leak into project references during restore.)

Tests:

```powershell
dotnet test tests/DxvkInstaller.Tests/DxvkInstaller.Tests.csproj
```

## Releases

Push a tag like `v0.1.0` (or run the **Release** workflow manually and provide a version) to build and attach the self-contained zip to a GitHub Release.

## Credits

- [DXVK](https://github.com/doitsujin/dxvk) by Philip Rebohle (doitsujin) and contributors.
- [DXVK-GPLAsync](https://gitlab.com/Ph42oN/dxvk-gplasync) by Ph42oN.

This tool downloads those projects' official release archives at runtime and bundles none of their code.

## License

MIT. See [LICENSE](LICENSE).
