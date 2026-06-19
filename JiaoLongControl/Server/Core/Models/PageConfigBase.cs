using System.IO;
using System.Text.Json;

namespace JiaoLongControl.Server.Core.Models;

public abstract class PageConfigBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static T Load<T>(string fileName) where T : new()
    {
        string path = Path.Combine(ConfigDir, fileName);
        if (!File.Exists(path)) return new T();
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    public void Save(string fileName)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(Path.Combine(ConfigDir, fileName),
            JsonSerializer.Serialize(this, GetType(), JsonOptions));
    }

    public static void InitializeConfigs()
    {
        InitializeConfig<AppPageConfig>("app.json");
        InitializeConfig<CpuPageConfig>("cpu.json");
        InitializeConfig<GpuPageConfig>("gpu.json");
        InitializeConfig<FanPageConfig>("fan.json");
        InitializeConfig<SmuPageConfig>("smu.json");
    }

    private static void InitializeConfig<T>(string fileName) where T : PageConfigBase, new()
    {
        string path = Path.Combine(ConfigDir, fileName);
        if (!File.Exists(path))
        {
            var config = new T();
            config.Save(fileName);
        }
    }

    public static string ConfigDir { get; set; } = Path.Combine(AppContext.BaseDirectory, "config");
}
