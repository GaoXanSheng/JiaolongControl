using System.IO;
using System.Text.Json;
using JiaoLongControl.Server.Core.Models;

namespace JiaoLongControl.Server.Core.Controllers;

[System.Runtime.InteropServices.ComVisible(true)]
public class ConfigController
{
    public static Config Config { get; private set; } = new();

    private static readonly string ConfigDir =
        Path.Combine(AppContext.BaseDirectory, "config");

    private static readonly string ConfigPath =
        Path.Combine(ConfigDir, "config.json");

    public string GetConfig()
    {
        return JsonSerializer.Serialize(Config);
    }

    public void SetConfig(string json)
    {
        Config = JsonSerializer.Deserialize<Config>(json) ?? new Config();
        Save();
    }
    public static void Reload()
    {
        Load();
    }

    public static void Save()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);

        File.WriteAllText(
            ConfigPath,
            JsonSerializer.Serialize(Config, new JsonSerializerOptions
            {
                WriteIndented = true
            })
        );
    }

    public static void Load()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);

        if (!File.Exists(ConfigPath))
        {
            Save();
            return;
        }

        var json = File.ReadAllText(ConfigPath);
        Config = JsonSerializer.Deserialize<Config>(json) ?? new Config();
    }
}