namespace DxvkInstaller.Services;

public sealed record InstallOutcome(IReadOnlyList<string> CopiedFiles, bool ConfWritten);

public sealed class InstallService
{
    public static readonly string[] DxvkFileSet = { "d3d8.dll", "d3d9.dll", "d3d10core.dll", "d3d11.dll", "dxgi.dll" };

    public IReadOnlyList<string> GetConflicts(string targetDirectory, IReadOnlyList<string> filesToInstall)
    {
        var conflicts = new List<string>();
        foreach (var fileName in filesToInstall)
        {
            if (File.Exists(Path.Combine(targetDirectory, fileName)))
            {
                conflicts.Add(fileName);
            }
        }

        if (File.Exists(Path.Combine(targetDirectory, "dxvk.conf")) && !conflicts.Contains("dxvk.conf"))
        {
            conflicts.Add("dxvk.conf");
        }

        return conflicts;
    }

    public InstallOutcome OverwriteInstall(string targetDirectory, string archDirectory, IReadOnlyList<string> filesToInstall, string? confText)
    {
        var copiedFiles = new List<string>();

        foreach (var fileName in filesToInstall)
        {
            var source = Path.Combine(archDirectory, fileName);
            if (!File.Exists(source))
            {
                throw new FileNotFoundException($"The downloaded release does not contain {fileName}.", source);
            }

            File.Copy(source, Path.Combine(targetDirectory, fileName), overwrite: true);
            copiedFiles.Add(fileName);
        }

        var confWritten = false;
        if (!string.IsNullOrWhiteSpace(confText))
        {
            File.WriteAllText(Path.Combine(targetDirectory, "dxvk.conf"), NormalizeConfText(confText));
            copiedFiles.Add("dxvk.conf");
            confWritten = true;
        }

        return new InstallOutcome(copiedFiles, confWritten);
    }

    public IReadOnlyList<string> UninstallDxvkFiles(string targetDirectory)
    {
        var removedFiles = new List<string>();
        foreach (var fileName in DxvkFileSet.Append("dxvk.conf"))
        {
            var path = Path.Combine(targetDirectory, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
                removedFiles.Add(fileName);
            }
        }

        return removedFiles;
    }

    public static string NormalizeConfText(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        return normalized.Length == 0 ? string.Empty : normalized + Environment.NewLine;
    }
}
