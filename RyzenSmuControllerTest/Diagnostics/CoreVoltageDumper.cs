using JiaoLongControl.Server.Core.Controllers;
using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.Diagnostics;

/// <summary>走主控制器 GetCoreVoltage() 采样核心电压，验证产品路径（--core-voltage）。</summary>
internal static class CoreVoltageDumper
{
    public static void Dump(RyzenSmuController ctrl)
    {
        ConsoleLog.Log(">>> 通过主项目 GetCoreVoltage() 读取核心电压 (8 次采样) ...");
        for (int i = 0; i < 8; i++)
        {
            var v = ctrl.GetCoreVoltage();
            ConsoleLog.Log($"    {i + 1}: {(v.HasValue ? $"{v.Value:F3} V" : "null")}");
            Thread.Sleep(250);
        }
        ConsoleLog.Log("");
    }
}
