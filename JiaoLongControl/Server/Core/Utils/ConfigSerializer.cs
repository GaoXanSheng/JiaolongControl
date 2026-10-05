using System.IO;
using JiaoLongControl.Server.Core.Models;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectGraphVisitors;
using Version = System.Version;

namespace JiaoLongControl.Server.Core.Utils;

public static class ConfigSerializer
{
    private const string FileName = "config.yaml";
    private const string BackupExt = ".bak";
    private const string TempExt = ".tmp";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithEmissionPhaseObjectGraphVisitor(args => new CommentsObjectGraphVisitor(args.InnerVisitor))
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
        .Build();

    // 窗口尺寸记忆自该版本起统一为物理像素语义 (默认 1300×820 px, 不随 DPI/缩放放大)。
    // 更早版本固化的记忆值 (旧 DIP 字段 WindowWidth/Height, 或按 DIP 默认×缩放倍数
    // 算出的像素值, 如 150% 屏上的 1824×1026) 与真实用户调整无法区分, 升级时清零:
    // 首次启动回到新默认, 之后的手动调整正常记忆。
    private static readonly Version PixelSizeSemanticsVersion = new("10.19.31");

    public static string ConfigDir { get; set; } = Path.Combine(AppContext.BaseDirectory, "config");
    public static string ConfigPath => Path.Combine(ConfigDir, FileName);
    private static string TempPath => ConfigPath + TempExt;
    private static string BackupPath => ConfigPath + BackupExt;

    public static string Serialize<T>(T config)
    {
        return Serializer.Serialize(config);
    }

    public static string? ReadFileContent()
    {
        if (!File.Exists(ConfigPath))
            return null;
        return File.ReadAllText(ConfigPath);
    }

    public static JiaoLongConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new JiaoLongConfig();

        try
        {
            var yaml = File.ReadAllText(ConfigPath);
            return Deserializer.Deserialize<JiaoLongConfig>(yaml);
        }
        catch (Exception)
        {
            if (File.Exists(BackupPath))
            {
                try
                {
                    var yaml = File.ReadAllText(BackupPath);
                    return Deserializer.Deserialize<JiaoLongConfig>(yaml);
                }
                catch
                {
                    // 原始文件和备份文件均读取失败
                }
            }

            return new JiaoLongConfig();
        }
    }

    public static void Save(JiaoLongConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        var yaml = Serialize(config);
        if (File.Exists(ConfigPath))
            File.Copy(ConfigPath, BackupPath, overwrite: true);
        File.WriteAllText(TempPath, yaml);
        File.Move(TempPath, ConfigPath, overwrite: true);
        if (File.Exists(TempPath))
            File.Delete(TempPath);
    }

    public static void Initialize(string version)
    {
        // 如果配置文件不存在
        if (!File.Exists(ConfigPath))
        {
            Save(new JiaoLongConfig { Version = version });
        }

        var existing = Load();
        // 如果配置文件版本字段不存在 删除重建
        if (string.IsNullOrWhiteSpace(existing.Version))
        {
            if (File.Exists(ConfigPath))
            {
                File.Delete(ConfigPath);
            }
            Save(new JiaoLongConfig { Version = version });
        }
        // 如果配置文件存在但版本不一致
        if (existing.Version != version )
        {
            Update(existing, version);
        }
    }

    public static void Update(JiaoLongConfig LowConfig, string version)
    {
        // 版本号缺失或解析失败按旧版本处理
        if (!Version.TryParse(LowConfig.Version, out var oldVersion) ||
            oldVersion < PixelSizeSemanticsVersion)
        {
            LowConfig.App.WindowPixelWidth = 0;
            LowConfig.App.WindowPixelHeight = 0;
            LowConfig.App.WindowWidth = 0;
            LowConfig.App.WindowHeight = 0;
        }

        // 默认无损迁移
        LowConfig.Version = version;
        Save(LowConfig);
    }

    private class CommentsObjectGraphVisitor : ChainedObjectGraphVisitor
    {
        public CommentsObjectGraphVisitor(IObjectGraphVisitor<IEmitter> nextVisitor)
            : base(nextVisitor)
        {
        }

        public override bool EnterMapping(IPropertyDescriptor key, IObjectDescriptor value, IEmitter context,
            ObjectSerializer serializer)
        {
            var commentAttr = key.GetCustomAttribute<ConfigCommentAttribute>();
            if (commentAttr != null)
            {
                context.Emit(new Comment(commentAttr.Comment, isInline: false));
            }

            var rangeAttr = key.GetCustomAttribute<ConfigRangeAttribute>();
            if (rangeAttr != null)
            {
                context.Emit(new Comment($"范围: {rangeAttr.Min} ~ {rangeAttr.Max}", isInline: false));
            }

            return base.EnterMapping(key, value, context, serializer);
        }
    }
}