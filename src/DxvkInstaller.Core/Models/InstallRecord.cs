namespace DxvkInstaller.Models;

public sealed record InstallRecord(
    DateTimeOffset TimestampUtc,
    string ExePath,
    string TargetDirectory,
    string Edition,
    string Version,
    IReadOnlyList<string> Files,
    bool ConfWritten);
