using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using JiaoLongControl.Server.Core.Models;
using JiaoLongControl.Server.Core.Utils;
using log4net;
using Microsoft.Extensions.Configuration;

namespace JiaoLongControl.Server.Core.Controllers;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class ConfigController
{
    private static readonly ILog _logger = LogManager.GetLogger(typeof(ConfigController));
    public static Config Config { get; private set; } = new();

    private static readonly string ConfigDir = Path.Combine(AppContext.BaseDirectory, "config");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static IConfigurationRoot? _configuration;

    static ConfigController()
    {
        Load();
    }

    public CommandResult GetConfig()
    {
        return new CommandResult(true, "成功", Config);
    }

    public CommandResult SetConfig(Config newConfig)
    {
        try
        {
            if (newConfig != null)
            {
                Config = newConfig;
                Save();
            }
            else
            {
                return new CommandResult(false, "Received a null config object.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[ConfigController] Error saving config: {ex.Message}", ex);
            return new CommandResult(false, $"Error updating config: {ex.Message}");
        }
        return new CommandResult(true, "配置已成功更新.");
    }

    public static void Reload()
    {
        _configuration?.Reload();
        Bind();
    }

    private static void Bind()
    {
        var newConfig = new Config();
        newConfig.AdvancedFanControlSystemConfig.CpuFan.Clear();
        newConfig.AdvancedFanControlSystemConfig.GpuFan.Clear();
        _configuration?.Bind(newConfig);
        if (newConfig.AdvancedFanControlSystemConfig.CpuFan.Count == 0)
        {
            var defaultConfig = new AdvancedFanControlSystemConfig();
            newConfig.AdvancedFanControlSystemConfig.CpuFan = defaultConfig.CpuFan;
        }
        if (newConfig.AdvancedFanControlSystemConfig.GpuFan.Count == 0)
        {
            var defaultConfig = new AdvancedFanControlSystemConfig();
            newConfig.AdvancedFanControlSystemConfig.GpuFan = defaultConfig.GpuFan;
        }
        Config = newConfig; 
    }

    public static void Load()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);

        if (!File.Exists(ConfigPath))
        {
            Save();
        }
        else
        {
            MigrateOldConfig(); 
        }

        _configuration = new ConfigurationBuilder()
            .SetBasePath(ConfigDir)
            .AddJsonFile("config.json", optional: true, reloadOnChange: true)
            .Build();

        Bind();
    }

    public static void Save()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);

        var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
    
    // 处理老版本配置文件兼容性
    private static void MigrateOldConfig()
    {
        try
        {
            string jsonString = File.ReadAllText(ConfigPath);
            var jsonNode = JsonNode.Parse(jsonString);

            if (jsonNode is JsonObject jsonObj)
            {
                bool isMigrated = false;
                if (jsonObj.TryGetPropertyValue("AdvancedFanControlSystemConfig", out var fanConfigNode))
                {
                    if (fanConfigNode is JsonArray oldArray)
                    {
                        var oldPoints = JsonSerializer.Deserialize<List<FanPoint>>(oldArray.ToJsonString());
                        var newFanConfig = new AdvancedFanControlSystemConfig();
                        if (oldPoints != null && oldPoints.Count > 0)
                        {
                            newFanConfig.CpuFan = oldPoints;
                        }
                        jsonObj["AdvancedFanControlSystemConfig"] = JsonSerializer.SerializeToNode(newFanConfig);
                        isMigrated = true;
                    }
                }
                
                if (isMigrated)
                {
                    File.WriteAllText(ConfigPath, jsonObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                }
            }
        }
        catch
        {
        }
    }
}