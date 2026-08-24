using Newtonsoft.Json;

namespace Nekoframe;

// Persists user settings in %AppData%\Nekoframe\config.json.
// Edit the file and restart Nekoframe to apply changes.
public class AppConfig
{
    private static readonly string ConfigPath = Path.Combine(Logger.LogDir, "config.json");

    public int WebSocketPort { get; set; } = 3069;

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { }

        var defaults = new AppConfig();
        defaults.Save(); // write config.json on first run so the user can find and edit it
        return defaults;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Logger.LogDir);
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        catch { }
    }
}
