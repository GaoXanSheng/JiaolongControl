using System.IO;
using System.Reflection;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JiaoLongControl.Server.Core.Models;

public static class ConfigSerializer
{
    private const string FileName = "config.yaml";
    private const string BackupExt = ".bak";
    private const string TempExt = ".tmp";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static string ConfigPath => Path.Combine(ConfigDir, FileName);
    private static string TempPath => ConfigPath + TempExt;
    private static string BackupPath => ConfigPath + BackupExt;

    public static string ConfigDir { get; set; } = Path.Combine(AppContext.BaseDirectory, "config");

    public static JiaoLongConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new JiaoLongConfig();

        try
        {
            var yaml = File.ReadAllText(ConfigPath);
            var config = Deserializer.Deserialize<JiaoLongConfig>(yaml);
            return config ?? new JiaoLongConfig();
        }
        catch (Exception)
        {
            if (File.Exists(BackupPath))
            {
                try
                {
                    var yaml = File.ReadAllText(BackupPath);
                    return Deserializer.Deserialize<JiaoLongConfig>(yaml) ?? new JiaoLongConfig();
                }
                catch
                {
                    // both primary and backup failed
                }
            }
            return new JiaoLongConfig();
        }
    }

    public static void Save(JiaoLongConfig config)
    {
        Directory.CreateDirectory(ConfigDir);

        var yaml = SerializeWithComments(config);

        // backup existing
        if (File.Exists(ConfigPath))
            File.Copy(ConfigPath, BackupPath, overwrite: true);

        // atomic write: write to temp, then rename
        File.WriteAllText(TempPath, yaml);
        File.Move(TempPath, ConfigPath, overwrite: true);

        // clean up temp if rename failed
        if (File.Exists(TempPath))
            File.Delete(TempPath);
    }

    public static void Initialize()
    {
        if (!File.Exists(ConfigPath))
        {
            var config = new JiaoLongConfig();
            Save(config);
        }
    }

    private static string SerializeWithComments(JiaoLongConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"version: {config.Version}");
        sb.AppendLine();

        WriteSection(sb, "app", config.App, 0);
        WriteSection(sb, "cpu", config.Cpu, 0);
        WriteSection(sb, "gpu", config.Gpu, 0);
        WriteSection(sb, "fan", config.Fan, 0);
        WriteSection(sb, "smu", config.Smu, 0);

        return sb.ToString();
    }

    private static void WriteSection(StringBuilder sb, string key, object obj, int indent)
    {
        var prefix = new string(' ', indent * 2);
        sb.AppendLine($"{prefix}{key}:");

        var type = obj.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var comment = prop.GetCustomAttribute<ConfigCommentAttribute>();
            var range = prop.GetCustomAttribute<ConfigRangeAttribute>();

            if (comment != null)
            {
                sb.AppendLine($"{prefix}  # {comment.Comment}");
            }
            if (range != null)
            {
                sb.AppendLine($"{prefix}  # 范围: {range.Min} ~ {range.Max}");
            }

            var value = prop.GetValue(obj);

            if (value is List<FanPoint> fanPoints)
            {
                sb.AppendLine($"{prefix}  {prop.Name}:");
                foreach (var fp in fanPoints)
                {
                    sb.AppendLine($"{prefix}    - temp: {fp.temp}");
                    sb.AppendLine($"{prefix}      speed: {fp.speed}");
                }
            }
            else if (IsNestedObject(prop.PropertyType))
            {
                WriteSection(sb, prop.Name, value!, indent + 2);
            }
            else
            {
                var yamlValue = FormatYamlValue(value);
                sb.AppendLine($"{prefix}  {prop.Name}: {yamlValue}");
            }
        }
    }

    private static bool IsNestedObject(Type type)
    {
        return type.IsClass && type != typeof(string) && !type.IsGenericType;
    }

    private static string FormatYamlValue(object? value)
    {
        return value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            string s => $"\"{s}\"",
            _ => value.ToString() ?? "null"
        };
    }
}
