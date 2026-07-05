namespace JiaoLongControl.Server.Core.Models;

[AttributeUsage(AttributeTargets.Property)]
public class ConfigCommentAttribute : Attribute
{
    public string Comment { get; }
    public ConfigCommentAttribute(string comment) => Comment = comment;
}

[AttributeUsage(AttributeTargets.Property)]
public class ConfigRangeAttribute : Attribute
{
    public double Min { get; }
    public double Max { get; }
    public ConfigRangeAttribute(double min, double max) { Min = min; Max = max; }
}
