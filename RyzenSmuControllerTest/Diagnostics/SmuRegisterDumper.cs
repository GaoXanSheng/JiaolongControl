using JiaoLongControl.Server.Core.Controllers;
using RyzenSmuControllerTest.Infrastructure;

namespace RyzenSmuControllerTest.Diagnostics;

/// <summary>对任意 SMU/SMN 寄存器地址做 3 次采样读取（--smu-read）。</summary>
internal static class SmuRegisterDumper
{
    public static void Dump(RyzenSmuController ctrl, uint addr)
    {
        ConsoleLog.Log($">>> 读取 SMU 寄存器 0x{addr:X6} (3 次采样) ...");
        try
        {
            for (int i = 0; i < 3; i++)
            {
                uint val = (uint)ctrl.Execute("ioctl_read_smu_register", [addr], 1)[0];
                ConsoleLog.Log($"    0x{addr:X6} = 0x{val:X8} ({val})");
                Thread.Sleep(200);
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"    读取失败: {ex.Message}");
        }
        ConsoleLog.Log("");
    }
}
