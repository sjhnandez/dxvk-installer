namespace DxvkInstaller.Models;

public sealed record ReleaseInfo(
    DxvkEdition Edition,
    string Tag,
    string AssetName,
    string DownloadUrl);
