using JiaoLongControl.Server.Core.Controllers;
using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.Diagnostics;

/// <summary>读取 SMN SVI 遥测寄存器并按 SVI3 换算 VCore/SoC 电压（--dump-svi）。</summary>
internal static class SviTelemetryDumper
{
    private const uint SviBase = 0x0005A000;

    public static void Dump(RyzenSmuController ctrl)
    {
        ConsoleLog.Log(">>> 读取 SMN SVI 遥测寄存器 ...");
        try
        {
            uint tfn = (uint)ctrl.Execute("ioctl_read_smu_register", [SviBase + 0x8], 1)[0];
            uint plane0 = (uint)ctrl.Execute("ioctl_read_smu_register", [SviBase + 0x10], 1)[0];
            uint plane1 = (uint)ctrl.Execute("ioctl_read_smu_register", [SviBase + 0xC], 1)[0];

            double vcore = 1.550 - 0.00625 * ((plane0 >> 16) & 0xff);
            double vsoc = 1.550 - 0.00625 * ((plane1 >> 16) & 0xff);

            ConsoleLog.Log($"    SVI0_TFN   : 0x{tfn:X8}");
            ConsoleLog.Log($"    Plane0     : 0x{plane0:X8}  VCore = {vcore:F3} V");
            ConsoleLog.Log($"    Plane1     : 0x{plane1:X8}  SoC   = {vsoc:F3} V");
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"    SVI 遥测读取失败: {ex.Message}");
        }
        ConsoleLog.Log("");
    }
}
