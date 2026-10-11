using System.Text.Json;
using System.Text.Json.Serialization;
using DxvkInstaller.Models;

namespace DxvkInstaller.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _rootDirectory;

    public SettingsService(string? rootDirectory = null)
    {
        _rootDirectory = rootDirectory ?? AppPaths.Root;
    }

    public string SettingsFilePath => Path.Combine(_rootDirectory, "settings.json");

    public string InstallLogFilePath => Path.Combine(_rootDirectory, "installs.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFilePath), JsonOptions);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // Corrupt settings file: fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_rootDirectory);
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public void AppendInstallRecord(InstallRecord record)
    {
        try
        {
            Directory.CreateDirectory(_rootDirectory);

            var records = new List<InstallRecord>();
            if (File.Exists(InstallLogFilePath))
            {
                try
                {
                    records = JsonSerializer.Deserialize<List<InstallRecord>>(File.ReadAllText(InstallLogFilePath), JsonOptions) ?? records;
                }
                catch
                {
                    // Start a fresh log if the old one is unreadable.
                }
            }

            records.Add(record);
            File.WriteAllText(InstallLogFilePath, JsonSerializer.Serialize(records, JsonOptions));
        }
        catch
        {
            // The install log is best-effort; never fail an install over it.
        }
    }
}
