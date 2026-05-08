using System.IO;
using System.Text.Json;
using CeeThreeDeeCursors.Models;

namespace CeeThreeDeeCursors.Services;

public static class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CeeThreeDeeCursors");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static CrosshairSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<CrosshairSettings>(json, JsonOptions)
                       ?? new CrosshairSettings();
            }
        }
        catch
        {
            // Return defaults if anything goes wrong
        }
        return new CrosshairSettings();
    }

    public static void Save(CrosshairSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFile, json);
    }
}
