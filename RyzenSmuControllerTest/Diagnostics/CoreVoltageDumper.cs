using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Services;
using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.Diagnostics;

/// <summary>走主项目 HardwareMonitorService 采样核心电压，验证产品路径（--core-voltage）。</summary>
internal static class CoreVoltageDumper
{
    public static bool Dump()
    {
        ConsoleLog.Log(">>> 通过主项目读取核心电压 (8 次采样) ...");
        bool anySucceeded = false;
        for (int i = 0; i < 8; i++)
        {
            double v = HardwareMonitorService.ReadCpu().Voltage;
            if (v > 0)
                anySucceeded = true;
            ConsoleLog.Log($"    {i + 1}: {(v > 0 ? $"{v:F3} V" : "null")}");
            Thread.Sleep(400);
        }
        ConsoleLog.Log("");
        return anySucceeded;
    }
}
