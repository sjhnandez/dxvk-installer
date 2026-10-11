using System.Diagnostics;
using DxvkInstaller.Models;

namespace DxvkInstaller.Services;

public static class ElevationHelper
{
    public static bool CanWriteTo(string directory)
    {
        try
        {
            var probePath = Path.Combine(directory, $".dxvk-installer-write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "test");
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void RelaunchElevated(string exePath, DxvkEdition edition)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine the current executable path.");

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = true,
            Verb = "runas",
            Arguments = $"--resume \"{exePath}\" --edition {edition}"
        };

        Process.Start(startInfo);
    }
}
