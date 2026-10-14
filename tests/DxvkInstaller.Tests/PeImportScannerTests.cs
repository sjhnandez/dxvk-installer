using DxvkInstaller.Models;
using DxvkInstaller.Services;
using Xunit;

namespace DxvkInstaller.Tests;

public class PeImportScannerTests
{
    [Fact]
    public void Scan_splits_hard_and_delay_tables_and_normalizes_module_names()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "kernel32.dll");
        if (!File.Exists(path))
        {
            return; // not a Windows machine: nothing to scan
        }

        var scan = PeImportScanner.Scan(path);

        // kernel32.dll is a native DLL with a real (hard) import table.
        Assert.True(scan.HardImports.Count > 0);

        // Modules is the case-insensitive union of both tables.
        var union = new HashSet<string>(scan.HardImports, StringComparer.OrdinalIgnoreCase);
        union.UnionWith(scan.DelayImports);
        Assert.True(union.SetEquals(scan.Modules));

        // Every entry of either table shows up in Modules.
        Assert.All(scan.HardImports, m => Assert.Contains(m, scan.Modules));
        Assert.All(scan.DelayImports, m => Assert.Contains(m, scan.Modules));

        // Normalization contract: emitted names are lowercase and trimmed.
        Assert.All(scan.HardImports, m => Assert.True(m == m.Trim().ToLowerInvariant()));
        Assert.All(scan.Modules, m => Assert.True(m == m.Trim().ToLowerInvariant()));
    }
}
