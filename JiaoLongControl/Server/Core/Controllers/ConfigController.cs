using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Utils;

namespace JiaoLongControl.Server.Core.Controllers;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class ConfigController
{
    public static Config Config { get; private set; } = new();

    private static readonly string ConfigDir =
        Path.Combine(AppContext.BaseDirectory, "config");

    private static readonly string ConfigPath =
        Path.Combine(ConfigDir, "config.json");

    public CommandResult GetConfig()
    {
        return new CommandResult(true, "成功", Config);
    }

    public CommandResult SetConfig(string json)
    {
        Config = JsonSerializer.Deserialize<Config>(json) ?? new Config();
        Save();
        return new CommandResult(true, "配置已成功更新.");
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