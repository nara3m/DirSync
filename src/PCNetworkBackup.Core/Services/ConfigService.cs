using System.Text.Json;
using PCNetworkBackup.Core.Models;

namespace PCNetworkBackup.Core.Services;

public static class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCNetworkBackup");

    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public static AppConfig Load() => LoadFrom(ConfigPath);

    public static void Save(AppConfig config) => SaveTo(ConfigPath, config);

    /// <summary>Exposed with an explicit path so unit tests never touch the real user profile.</summary>
    public static AppConfig LoadFrom(string path)
    {
        if (!File.Exists(path))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch (Exception)
        {
            // A corrupted/malformed config file must NEVER crash the scheduled
            // background sync. Fail safe: come back disabled and let the user
            // re-open the GUI and reconfigure.
            return new AppConfig { Enabled = false };
        }
    }

    public static void SaveTo(string path, AppConfig config)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, JsonOptions);

        // Write-then-move instead of writing the live file directly, so a crash
        // mid-write never leaves a half-written, unparseable config.json behind.
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Copy(tempPath, path, overwrite: true);
        File.Delete(tempPath);
    }
}
