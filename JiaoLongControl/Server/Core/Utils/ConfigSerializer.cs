using System.IO;
using JiaoLongControl.Server.Core.Models;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.ObjectGraphVisitors;

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
        var yaml = Serializer.Serialize(config);
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