using System.Formats.Tar;
using System.IO.Compression;

namespace DxvkInstaller.Services;

public static class ArchiveExtractor
{
    private const string CompletionMarker = ".extract-complete";

    public static async Task<string> ExtractAsync(string archivePath, string targetDirectory, CancellationToken cancellationToken = default)
    {
        var markerPath = Path.Combine(targetDirectory, CompletionMarker);
        if (File.Exists(markerPath))
        {
            return targetDirectory;
        }

        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }

        Directory.CreateDirectory(targetDirectory);
        var targetRoot = Path.GetFullPath(targetDirectory);

        await using (var fileStream = File.OpenRead(archivePath))
        await using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
        using (var tarReader = new TarReader(gzipStream))
        {
            while (await tarReader.GetNextEntryAsync(cancellationToken: cancellationToken).ConfigureAwait(false) is { } entry)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var destinationPath = Path.GetFullPath(Path.Combine(targetRoot, entry.Name));
                if (!destinationPath.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // refuse entries that would escape the target directory
                }

                switch (entry.EntryType)
                {
                    case TarEntryType.Directory:
                        Directory.CreateDirectory(destinationPath);
                        break;

                    case TarEntryType.RegularFile:
                    case TarEntryType.V7RegularFile:
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                        await entry.ExtractToFileAsync(destinationPath, true, cancellationToken).ConfigureAwait(false);
                        break;

                    default:
                        break; // links and other entry types are not used by DXVK archives
                }
            }
        }

        File.WriteAllText(markerPath, string.Empty);
        return targetDirectory;
    }

    public static string? FindArchDirectory(string extractedRoot, string archFolderName)
    {
        if (!Directory.Exists(extractedRoot))
        {
            return null;
        }

        foreach (var directory in Directory.EnumerateDirectories(extractedRoot, archFolderName, SearchOption.AllDirectories))
        {
            if (Directory.EnumerateFiles(directory).Any(file =>
                    Path.GetFileName(file).Equals("d3d11.dll", StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(file).Equals("d3d9.dll", StringComparison.OrdinalIgnoreCase)))
            {
                return directory;
            }
        }

        return null;
    }
}
