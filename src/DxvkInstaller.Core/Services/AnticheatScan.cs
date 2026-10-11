namespace DxvkInstaller.Services;

public static class AnticheatScan
{
    private static readonly string[] KnownMarkers =
    {
        "easyanticheat",
        "battleye",
        "beservice",
        "beclient",
        "eac_launcher",
        "anticheat"
    };

    public static IReadOnlyList<string> Scan(string gameDirectory)
    {
        var hits = new List<string>();

        try
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(gameDirectory))
            {
                var name = Path.GetFileName(path);
                if (KnownMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                {
                    hits.Add(name);
                }
            }
        }
        catch
        {
            // Unreadable folder: report nothing rather than failing detection.
        }

        return hits;
    }
}
