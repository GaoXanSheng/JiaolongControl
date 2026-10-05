using JiaoLongControl.Server.Core.Utils;
using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.Diagnostics;

/// <summary>反射遍历遥测对象的公开属性并按名称推断单位打印（遥测类型随主项目演化，刻意不做强类型耦合）。</summary>
internal static class TelemetryPrinter
{
    public static void Print(CommandResult telemetryResult)
    {
        if (!telemetryResult.Success)
        {
            ConsoleLog.Error($"  遥测读取失败: {telemetryResult.Message}");
            return;
        }

        var data = telemetryResult.Data;
        if (data == null)
        {
            ConsoleLog.Log("  遥测数据: null");
            return;
        }

        var type = data.GetType();
        foreach (var prop in type.GetProperties())
        {
            var value = prop.GetValue(data);
            var unit = prop.Name switch
            {
                "Ppt" or "Tdc" or "Edc" => "W",
                "Temp" => "°C",
                "FreqMhz" => "MHz",
                "Usage" => "%",
                _ => ""
            };
            ConsoleLog.Log($"    {prop.Name,-10}: {value,8} {unit}");
        }
    }
}
