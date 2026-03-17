using System.IO;
using System.Text.Json;

namespace XmegaAudio.Settings;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string DefaultSettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XmegaAudio", "settings.json");

    public static AppSettings? LoadDefault()
    {
        return Load(DefaultSettingsPath);
    }

    public static AppSettings? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static bool SaveDefault(AppSettings settings)
    {
        return Save(settings, DefaultSettingsPath);
    }

    public static bool Save(AppSettings settings, string path)
    {
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(path, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
