namespace DxvkInstaller.Models;

public enum DxvkEdition
{
    Upstream,
    GplAsync
}

public static class DxvkEditionExtensions
{
    public static string DisplayName(this DxvkEdition edition) =>
        edition == DxvkEdition.Upstream ? "DXVK (upstream)" : "DXVK-GPLAsync";
}
