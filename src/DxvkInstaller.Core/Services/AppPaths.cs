namespace DxvkInstaller.Services;

public static class AppPaths
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DxvkInstaller");

    public static string CacheRoot => Path.Combine(Root, "cache");

    public static string SettingsFile => Path.Combine(Root, "settings.json");

    public static string InstallLogFile => Path.Combine(Root, "installs.json");
}
